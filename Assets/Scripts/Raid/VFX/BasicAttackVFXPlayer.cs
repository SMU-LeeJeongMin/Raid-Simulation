// 기본 공격 Hit VFX
using UnityEngine;
using UnityEngine.Serialization;

public class BasicAttackVFXPlayer : MonoBehaviour
{
    [Header("Database")]
    [FormerlySerializedAs("vfxDatabase")]
    public BasicAttackVFXDatabase VFXDatabase;

    [Header("References")]
    public PlayerClassInfo classInfo;

    [Header("Fallback")]
    public float fallbackTargetHeight = 1.1f;

    private void Awake()
    {
        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();
    }

    public void PlayAttackHit(BasicAttackStats stats, DamageReceiver target)
    {
        BasicAttackVFXEntry entry = ResolveEntry(stats);
        if (entry == null || target == null)
            return;

        if (!entry.spawnHitVFX || entry.hitVFXPrefab == null)
            return;

        Transform attachParent = null;
        Vector3 position = GetTargetVFXPosition(target, entry, out attachParent);
        Quaternion rotation = Quaternion.Euler(entry.hitVFXEuler);

        GameObject instance = SpawnVFX(entry.hitVFXPrefab, position, rotation, entry.hitVFXScale, entry.autoDestroyDelay);
        if (instance != null && entry.attachHitVFXToTarget && attachParent != null)
            instance.transform.SetParent(attachParent, true);
    }

    private BasicAttackVFXEntry ResolveEntry(BasicAttackStats stats)
    {
        if (VFXDatabase == null)
            return null;

        string id = stats != null ? stats.characterId : null;
        if (string.IsNullOrWhiteSpace(id) && classInfo != null)
            id = classInfo.characterId;

        if (string.IsNullOrWhiteSpace(id))
            id = SelectedCharacterMemory.LoadSelectedId("warrior");

        return VFXDatabase.GetEntry(id);
    }

    private Vector3 GetTargetVFXPosition(DamageReceiver target, BasicAttackVFXEntry entry, out Transform attachParent)
    {
        attachParent = null;

        if (target == null)
            return transform.position + Vector3.up * fallbackTargetHeight;

        Health health = target.targetHealth != null ? target.targetHealth : target.GetComponentInParent<Health>();
        Transform root = health != null ? health.transform : target.transform;
        attachParent = root;

        if (!string.IsNullOrWhiteSpace(entry.hitAnchorName) && root != null)
        {
            Transform anchor = FindChildByName(root, entry.hitAnchorName);
            if (anchor != null)
            {
                attachParent = anchor;
                return anchor.position + entry.hitVFXOffset;
            }
        }

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
                return bounds.center + entry.hitVFXOffset;
        }

        return (root != null ? root.position : target.transform.position) + Vector3.up * fallbackTargetHeight + entry.hitVFXOffset;
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
