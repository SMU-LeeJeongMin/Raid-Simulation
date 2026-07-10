// 스킬 VFX
using System;
using System.Collections;
using UnityEngine;

public class PlayerSkillVFXPlayer : MonoBehaviour
{
    [Header("References")]
    public Transform castOrigin;
    public PlayerClassInfo classInfo;

    [Header("Fallback")]
    public float fallbackCasterHeight = 0.9f;
    public float fallbackTargetHeight = 1.1f;

    private void Awake()
    {
        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();
    }

    public GameObject PlayCastVFX(PlayerSkillDefinition skill)
    {
        if (skill == null || skill.castVFXPrefab == null)
            return null;

        Transform origin = castOrigin != null ? castOrigin : transform;
        Vector3 position = GetCasterVFXPosition(skill.castVFXOffset);
        Quaternion rotation = ResolveRotation(skill.castVFXRotationMode, skill.castVFXEuler, position, position + origin.forward);
        GameObject instance = SpawnVFX(skill.castVFXPrefab, position, rotation, skill.castVFXScale, skill.castVFXAutoDestroyDelay);

        if (instance != null && skill.attachCastVFXToCaster)
            instance.transform.SetParent(origin, true);
        return instance;
    }

    public GameObject PlayAuraVFX(PlayerSkillDefinition skill)
    {
        if (skill == null || skill.auraVFXPrefab == null)
            return null;

        Transform origin = castOrigin != null ? castOrigin : transform;
        Vector3 position = transform.position + transform.TransformDirection(skill.auraVFXOffset);
        Quaternion rotation = transform.rotation * Quaternion.Euler(skill.auraVFXEuler);
        GameObject instance = SpawnVFX(skill.auraVFXPrefab, position, rotation, skill.auraVFXScale, skill.auraVFXAutoDestroyDelay);

        if (instance != null && skill.attachAuraVFXToCaster)
            instance.transform.SetParent(origin, true);

        return instance;
    }

    // 공격형 스킬의 Target VFX
    public bool PlayTargetVFX(PlayerSkillDefinition skill, DamageReceiver target, Action onProjectileImpact = null)
    {
        if (skill == null)
            return false;

        if (skill.useProjectileVFX && skill.projectileVFXPrefab != null)
        {
            PlayProjectileVFX(skill, target, onProjectileImpact);
            return true;
        }

        if (skill.targetVFXPrefab == null)
            return false;

        Vector3 position = skill.targetVFXAtCaster
            ? GetCasterVFXPosition(skill.targetVFXOffset)
            : GetTargetVFXPosition(target, skill.targetAnchorName, skill.targetVFXOffset);

        Vector3 lookTarget = target != null ? GetTargetVFXPosition(target, skill.targetAnchorName, skill.targetVFXOffset) : position + transform.forward;
        Quaternion rotation = ResolveRotation(skill.targetVFXRotationMode, skill.targetVFXEuler, position, lookTarget);
        SpawnVFX(skill.targetVFXPrefab, position, rotation, skill.targetVFXScale, skill.targetVFXAutoDestroyDelay);
        return false;
    }

    public GameObject PlayPersistentVFX(PlayerSkillDefinition skill, DamageReceiver target)
    {
        if (skill == null || skill.persistentVFXPrefab == null)
            return null;

        Vector3 position = skill.persistentVFXAtCaster
            ? GetCasterVFXPosition(skill.persistentVFXOffset)
            : GetTargetVFXPosition(target, skill.targetAnchorName, skill.persistentVFXOffset);

        Vector3 lookTarget = target != null ? GetTargetVFXPosition(target, skill.targetAnchorName, skill.persistentVFXOffset) : position + transform.forward;
        Quaternion rotation = ResolveRotation(skill.persistentVFXRotationMode, skill.persistentVFXEuler, position, lookTarget);
        return SpawnVFX(skill.persistentVFXPrefab, position, rotation, skill.persistentVFXScale, skill.persistentVFXDuration);
    }

    public GameObject PlayAllyVFX(PlayerSkillDefinition skill, PlayerStatus ally)
    {
        if (skill == null || skill.allyVFXPrefab == null || ally == null)
            return null;

        Transform anchor = FindChildByName(ally.transform, skill.allyVFXAnchorName);
        Vector3 position = anchor != null
            ? anchor.position + skill.allyVFXOffset
            : ally.transform.position + skill.allyVFXOffset;

        Quaternion rotation = Quaternion.Euler(skill.allyVFXEuler);
        return SpawnVFX(skill.allyVFXPrefab, position, rotation, skill.allyVFXScale, skill.allyVFXAutoDestroyDelay);
    }

    private void PlayProjectileVFX(PlayerSkillDefinition skill, DamageReceiver target, Action onProjectileImpact)
    {
        Vector3 targetPosition = GetTargetVFXPosition(target, skill.targetAnchorName, skill.targetVFXOffset);
        Vector3 startPosition = GetProjectileStartPosition(skill, target, targetPosition);

        if (skill.projectileRenderMode == PlayerSkillVFXProjectileRenderMode.StretchLine)
        {
            PlayStretchLineProjectileVFX(skill, target, startPosition, targetPosition, onProjectileImpact);
            return;
        }

        Quaternion rotation = ResolveRotation(skill.projectileVFXRotationMode, skill.projectileVFXEuler, startPosition, targetPosition);

        GameObject projectile = SpawnVFX(skill.projectileVFXPrefab, startPosition, rotation, skill.projectileVFXScale, skill.projectileMaxLifetime + 0.5f);
        if (projectile == null)
        {
            onProjectileImpact?.Invoke();
            return;
        }

        SkillProjectileVFX mover = projectile.GetComponent<SkillProjectileVFX>();
        if (mover == null)
            mover = projectile.AddComponent<SkillProjectileVFX>();

        mover.Initialize(
            targetPosition,
            skill.projectileSpeed,
            skill.projectileMaxLifetime,
            skill.projectileLookAtTarget,
            () => InvokeProjectileImpact(skill, target, onProjectileImpact),
            Quaternion.Euler(skill.projectileVFXEuler));
    }

    private void PlayStretchLineProjectileVFX(PlayerSkillDefinition skill, DamageReceiver target, Vector3 startPosition, Vector3 targetPosition, Action onProjectileImpact)
    {
        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            InvokeProjectileImpact(skill, target, onProjectileImpact);
            return;
        }

        Vector3 midPoint = (startPosition + targetPosition) * 0.5f;
        Quaternion rotation = ResolveRotation(skill.projectileVFXRotationMode, skill.projectileVFXEuler, startPosition, targetPosition);
        Vector3 scale = BuildLineProjectileScale(skill, distance);
        float visibleDuration = Mathf.Max(0.01f, skill.projectileLineVisibleDuration);

        GameObject line = SpawnVFX(skill.projectileVFXPrefab, midPoint, rotation, scale, visibleDuration);
        if (line == null)
        {
            InvokeProjectileImpact(skill, target, onProjectileImpact);
            return;
        }

        float impactDelay = skill.projectileLineImpactDelay >= 0f
            ? skill.projectileLineImpactDelay
            : distance / Mathf.Max(0.1f, skill.projectileSpeed);

        StartCoroutine(InvokeProjectileImpactAfterDelay(skill, target, onProjectileImpact, impactDelay));
    }

    private Vector3 BuildLineProjectileScale(PlayerSkillDefinition skill, float distance)
    {
        Vector3 scale = skill.projectileVFXScale;
        float lengthScale = Mathf.Max(0.001f, distance * skill.projectileLineLengthScale);
        float thickness = Mathf.Max(0.001f, skill.projectileLineThicknessScale);

        switch (skill.projectileLineAxis)
        {
            case PlayerSkillVFXLineAxis.X:
                scale.x *= lengthScale;
                scale.y *= thickness;
                scale.z *= thickness;
                break;

            case PlayerSkillVFXLineAxis.Y:
                scale.x *= thickness;
                scale.y *= lengthScale;
                scale.z *= thickness;
                break;

            case PlayerSkillVFXLineAxis.Z:
            default:
                scale.x *= thickness;
                scale.y *= thickness;
                scale.z *= lengthScale;
                break;
        }

        return scale;
    }

    private IEnumerator InvokeProjectileImpactAfterDelay(PlayerSkillDefinition skill, DamageReceiver target, Action onProjectileImpact, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        InvokeProjectileImpact(skill, target, onProjectileImpact);
    }

    private void InvokeProjectileImpact(PlayerSkillDefinition skill, DamageReceiver target, Action onProjectileImpact)
    {
        if (skill.spawnTargetVFXOnProjectileImpact && skill.targetVFXPrefab != null)
        {
            Vector3 impactPosition = GetTargetVFXPosition(target, skill.targetAnchorName, skill.targetVFXOffset);
            Quaternion impactRotation = ResolveRotation(skill.targetVFXRotationMode, skill.targetVFXEuler, impactPosition, impactPosition + transform.forward);
            SpawnVFX(skill.targetVFXPrefab, impactPosition, impactRotation, skill.targetVFXScale, skill.targetVFXAutoDestroyDelay);
        }

        onProjectileImpact?.Invoke();
    }

    private Vector3 GetProjectileStartPosition(PlayerSkillDefinition skill, DamageReceiver target, Vector3 targetPosition)
    {
        switch (skill.projectileStartPoint)
        {
            case PlayerSkillVFXProjectileStartPoint.AboveTarget:
                return targetPosition + skill.projectileVFXStartOffset;

            case PlayerSkillVFXProjectileStartPoint.TargetOffset:
                return GetTargetVFXPosition(target, skill.targetAnchorName, skill.projectileVFXStartOffset);

            case PlayerSkillVFXProjectileStartPoint.Caster:
            default:
                return GetCasterVFXPosition(skill.projectileVFXStartOffset);
        }
    }

    private Quaternion ResolveRotation(PlayerSkillVFXRotationMode mode, Vector3 euler, Vector3 position, Vector3 lookTarget)
    {
        switch (mode)
        {
            case PlayerSkillVFXRotationMode.CasterForward:
            {
                Transform origin = castOrigin != null ? castOrigin : transform;
                return origin.rotation * Quaternion.Euler(euler);
            }

            case PlayerSkillVFXRotationMode.DirectionToTarget:
            {
                Vector3 direction = lookTarget - position;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = transform.forward;
                return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(euler);
            }

            case PlayerSkillVFXRotationMode.WorldEuler:
            default:
                return Quaternion.Euler(euler);
        }
    }

    private Vector3 GetCasterVFXPosition(Vector3 localOffset)
    {
        Transform origin = castOrigin != null ? castOrigin : transform;

        if (castOrigin != null)
            return origin.TransformPoint(localOffset);

        return transform.position + Vector3.up * fallbackCasterHeight + transform.TransformDirection(localOffset);
    }

    private Vector3 GetTargetVFXPosition(DamageReceiver target, string anchorName, Vector3 offset)
    {
        if (target == null)
            return transform.position + Vector3.up * fallbackTargetHeight + offset;

        Health health = target.targetHealth != null ? target.targetHealth : target.GetComponentInParent<Health>();
        Transform root = health != null ? health.transform : target.transform;

        Transform anchor = FindChildByName(root, anchorName);
        if (anchor != null)
            return anchor.position + offset;

        Collider[] colliders = root != null ? root.GetComponentsInChildren<Collider>(true) : null;
        if (colliders != null && colliders.Length > 0)
        {
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || !col.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (hasBounds)
                return bounds.center + offset;
        }

        return (root != null ? root.position : target.transform.position) + Vector3.up * fallbackTargetHeight + offset;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float destroyDelay)
    {
        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, position, rotation);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);
        RestartParticleSystems(instance);

        if (destroyDelay > 0f)
            Destroy(instance, destroyDelay);

        return instance;
    }

    private void RestartParticleSystems(GameObject root)
    {
        if (root == null)
            return;

        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
    }
}
