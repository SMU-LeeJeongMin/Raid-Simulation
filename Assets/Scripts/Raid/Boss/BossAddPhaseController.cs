// 슬라임 소환 패턴
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Health))]
public class BossAddPhaseController : MonoBehaviour
{
    [Header("References")]
    public Health bossHealth;
    public Animator animator;
    public BossBasicPatternController basicPatternController;
    public BossSkillPatternController skillPatternController;

    [Header("Trigger Thresholds")]
    public float[] healthRatioThresholds = new float[] { 0.8f, 0.6f, 0.4f, 0.2f };

    [Header("Boss Lock State")]
    public bool makeBossDamageImmune = true;
    public bool disableBossPatternControllers = true;

    [Header("Pre Summon Buff")]
    public AnimationClip preSummonBuffClip;
    public string preSummonBuffStateName = "Buff";
    [Min(0f)] public float preSummonBuffDuration = 2f;
    public bool useDirectPreSummonBuffClipPlayback = true;
    public bool loopPreSummonBuff = true;
    public GameObject preSummonBuffVFXPrefab;
    public Vector3 preSummonBuffVFXOffset = new Vector3(0f, 1.5f, 0f);
    public float preSummonBuffVFXDestroyDelay = 2.5f;

    [Header("Sit / Wait Loop After Summon")]
    public AnimationClip loopClip;
    public string loopStateName = "Sit";
    public bool useDirectLoopClipPlayback = true;

    [Header("Slime Summon")]
    public GameObject slimePrefab;
    public Transform[] summonPoints;
    public int slimeCount = 3;
    public float summonRadius = 6f;
    public float summonHeightOffset = 0.05f;
    public LayerMask groundMask = ~0;
    public float groundRayStartHeight = 8f;
    public float groundRayDistance = 30f;
    public string slimeLayerName = "Boss";

    [Header("Slime Settings")]
    public float slimeMaxHealth = 250f;
    public float slimeExplodeAfterSeconds = 15f;
    public float slimeExplosionDamage = 25f;
    public float slimeExplosionRadius = 2.5f;
    public float slimeDotRadius = 2.2f;
    public float slimeDotTickDamage = 8f;
    public float slimeDotTickInterval = 0.5f;
    public float slimeDotDuration = -1f;
    public bool slimeChaseParty = true;
    public float slimeMoveSpeed = 1.5f;
    public float slimeRetargetInterval = 2.5f;
    public float slimeAttackDistance = 1.2f;
    public float slimeMeleeDamage = 5f;
    public float slimeMeleeInterval = 1.5f;
    public AnimationClip slimeWalkClip;
    public string slimeWalkStateName = "Walk";
    public AnimationClip slimeIdleClip;
    public string slimeIdleStateName = "Idle";

    [Header("Slime Death Animation")]
    public AnimationClip slimeKnockdownClip;
    public string slimeKnockdownStateName = "Knockdown";
    public bool slimeUseDirectKnockdownClipPlayback = true;
    [Min(0f)] public float slimeKnockdownDurationOverride = 0f;
    [Min(0f)] public float slimeDestroyDelayAfterKnockdown = 0.1f;

    [Header("VFX")]
    public GameObject summonVFXPrefab;
    public GameObject slimeExplosionVFXPrefab;
    public GameObject slimeDotZoneVFXPrefab;

    [Header("Debug")]
    public bool logState = true;

    private bool[] triggered;
    private bool addPhaseActive;
    private readonly List<SlimeAddEnemy> activeSlimes = new List<SlimeAddEnemy>();
    private Coroutine addPhaseRoutine;

    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable playable;
    private AnimationClip currentLoopClip;
    private bool looping;

    private bool previousBasicEnabled;
    private bool previousSkillEnabled;

    public bool AddPhaseActive => addPhaseActive;

    private void Awake()
    {
        if (bossHealth == null)
            bossHealth = GetComponent<Health>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (basicPatternController == null)
            basicPatternController = GetComponent<BossBasicPatternController>();
        if (skillPatternController == null)
            skillPatternController = GetComponent<BossSkillPatternController>();

        triggered = new bool[healthRatioThresholds != null ? healthRatioThresholds.Length : 0];
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.EnsureEvents();
            bossHealth.onHealthChanged.AddListener(OnBossHealthChanged);
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null && bossHealth.onHealthChanged != null)
            bossHealth.onHealthChanged.RemoveListener(OnBossHealthChanged);
    }

    private void Update()
    {
        KeepLoopClipPlaying();
    }

    private void OnBossHealthChanged(Health health, float current, float max, float normalized)
    {
        if (health == null || health.IsDead || addPhaseActive)
            return;

        if (triggered == null || triggered.Length != (healthRatioThresholds != null ? healthRatioThresholds.Length : 0))
            triggered = new bool[healthRatioThresholds != null ? healthRatioThresholds.Length : 0];

        for (int i = 0; i < healthRatioThresholds.Length; i++)
        {
            if (triggered[i])
                continue;

            if (normalized <= healthRatioThresholds[i])
            {
                triggered[i] = true;
                addPhaseRoutine = StartCoroutine(AddPhaseRoutine(healthRatioThresholds[i]));
                break;
            }
        }
    }

    private IEnumerator AddPhaseRoutine(float threshold)
    {
        addPhaseActive = true;
        LockBossControllers(true);
        if (bossHealth != null && makeBossDamageImmune)
            bossHealth.SetDamageImmune(true);

        if (preSummonBuffVFXPrefab != null)
            Destroy(Instantiate(preSummonBuffVFXPrefab, transform.position + preSummonBuffVFXOffset, Quaternion.identity), Mathf.Max(preSummonBuffVFXDestroyDelay, preSummonBuffDuration + 0.2f));

        if (preSummonBuffDuration > 0f)
            yield return PlayTimedAnimation(preSummonBuffClip, preSummonBuffStateName, preSummonBuffDuration, useDirectPreSummonBuffClipPlayback, loopPreSummonBuff);

        SpawnSlimes();
        PlayLoopAnimation();

        while (HasActiveSlimes())
            yield return null;

        StopLoopAnimation();

        if (bossHealth != null && makeBossDamageImmune)
            bossHealth.SetDamageImmune(false);

        LockBossControllers(false);
        addPhaseActive = false;
    }

    private void LockBossControllers(bool locked)
    {
        if (locked)
        {
            if (basicPatternController != null)
            {
                previousBasicEnabled = basicPatternController.enabled;
                basicPatternController.ForceStopCurrentAction(false);
                basicPatternController.SetSpecialPatternLock(true);
                if (disableBossPatternControllers)
                    basicPatternController.enabled = false;
            }

            if (skillPatternController != null)
            {
                previousSkillEnabled = skillPatternController.enabled;
                if (disableBossPatternControllers)
                    skillPatternController.enabled = false;
            }
        }
        else
        {
            if (basicPatternController != null)
            {
                basicPatternController.enabled = previousBasicEnabled;
                basicPatternController.SetSpecialPatternLock(false);
            }

            if (skillPatternController != null)
                skillPatternController.enabled = previousSkillEnabled;
        }
    }

    private void SpawnSlimes()
    {
        activeSlimes.Clear();
        int count = Mathf.Max(1, slimeCount);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetSummonPosition(i, count);
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            GameObject slimeObject = CreateSlimeObject(position, rotation);

            SetLayerRecursively(slimeObject, LayerMask.NameToLayer(slimeLayerName));

            SlimeAddEnemy slime = slimeObject.GetComponent<SlimeAddEnemy>();
            if (slime == null)
                slime = slimeObject.AddComponent<SlimeAddEnemy>();

            slime.Configure(
                slimeMaxHealth,
                slimeExplodeAfterSeconds,
                slimeExplosionDamage,
                slimeExplosionRadius,
                slimeDotRadius,
                slimeDotTickDamage,
                slimeDotTickInterval,
                slimeDotDuration,
                slimeExplosionVFXPrefab,
                slimeDotZoneVFXPrefab,
                slimeChaseParty,
                slimeMoveSpeed,
                slimeRetargetInterval,
                slimeAttackDistance,
                slimeMeleeDamage,
                slimeMeleeInterval,
                slimeWalkClip,
                slimeWalkStateName,
                slimeIdleClip,
                slimeIdleStateName,
                slimeKnockdownClip,
                slimeKnockdownStateName,
                slimeUseDirectKnockdownClipPlayback,
                slimeKnockdownDurationOverride,
                slimeDestroyDelayAfterKnockdown);

            activeSlimes.Add(slime);

            if (summonVFXPrefab != null)
                Destroy(Instantiate(summonVFXPrefab, position, Quaternion.identity), 3f);
        }
    }

    private GameObject CreateSlimeObject(Vector3 position, Quaternion rotation)
    {
        GameObject slimeObject;
        if (slimePrefab != null)
        {
            slimeObject = Instantiate(slimePrefab, position, rotation);
        }
        else
        {
            slimeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            slimeObject.transform.position = position;
            slimeObject.transform.rotation = rotation;
            slimeObject.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
            Renderer renderer = slimeObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                Material mat = new Material(shader);
                mat.color = new Color(0.2f, 0.9f, 0.25f, 1f);
                renderer.sharedMaterial = mat;
            }
        }

        slimeObject.name = "Add_Slime_ExplodingOrb";
        RemoveLegacyInputDemoScripts(slimeObject);

        Health h = slimeObject.GetComponent<Health>();
        if (h == null)
            h = slimeObject.AddComponent<Health>();
        h.EnsureEvents();

        DamageReceiver receiver = slimeObject.GetComponent<DamageReceiver>();
        if (receiver == null)
            receiver = slimeObject.AddComponent<DamageReceiver>();
        receiver.targetHealth = h;

        WorldHealthBarUI hpUI = slimeObject.GetComponent<WorldHealthBarUI>();
        if (hpUI == null)
            hpUI = slimeObject.AddComponent<WorldHealthBarUI>();
        hpUI.targetHealth = h;

        Collider col = slimeObject.GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = slimeObject.AddComponent<SphereCollider>();
            sphere.radius = 0.6f;
        }

        return slimeObject;
    }


    private void RemoveLegacyInputDemoScripts(GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            string fullName = behaviour.GetType().FullName ?? string.Empty;
            if (typeName.Contains("SimpleAnimationPlayer") || fullName.Contains("SimpleAnimationPlayer"))
                Destroy(behaviour);
        }
    }

    private Vector3 GetSummonPosition(int index, int count)
    {
        if (summonPoints != null && index < summonPoints.Length && summonPoints[index] != null)
            return SnapToGround(summonPoints[index].position);

        float angle = (Mathf.PI * 2f) * index / Mathf.Max(1, count);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * summonRadius;
        return SnapToGround(transform.position + offset);
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * groundRayStartHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * summonHeightOffset;

        return position + Vector3.up * summonHeightOffset;
    }

    private bool HasActiveSlimes()
    {
        for (int i = activeSlimes.Count - 1; i >= 0; i--)
        {
            SlimeAddEnemy slime = activeSlimes[i];
            if (slime == null || slime.IsResolved)
            {
                activeSlimes.RemoveAt(i);
                continue;
            }
        }

        return activeSlimes.Count > 0;
    }

    private IEnumerator PlayTimedAnimation(AnimationClip clip, string stateName, float duration, bool useDirectClip, bool loop)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f || animator == null)
            yield break;

        if (useDirectClip && clip != null)
        {
            CreateGraphIfNeeded();
            if (playable.IsValid())
                playable.Destroy();

            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetTime(0d);
            playable.SetSpeed(1d);
            output.SetSourcePlayable(playable);
            graph.Play();

            float elapsed = 0f;
            double length = Mathf.Max(0.01f, clip.length);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (loop && playable.IsValid() && playable.GetTime() >= length)
                    playable.SetTime(0d);
                yield return null;
            }

            if (playable.IsValid())
                playable.Destroy();
            if (graph.IsValid())
                graph.Stop();
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            animator.enabled = true;
            animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            animator.Update(0f);
        }

        yield return new WaitForSeconds(duration);
    }

    private void PlayLoopAnimation()
    {
        if (animator == null)
            return;

        animator.enabled = true;

        if (useDirectLoopClipPlayback && loopClip != null)
        {
            CreateGraphIfNeeded();
            if (playable.IsValid())
                playable.Destroy();

            currentLoopClip = loopClip;
            looping = true;
            playable = AnimationClipPlayable.Create(graph, loopClip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetTime(0d);
            playable.SetSpeed(1d);
            output.SetSourcePlayable(playable);
            graph.Play();
            return;
        }

        if (!string.IsNullOrWhiteSpace(loopStateName))
            animator.CrossFadeInFixedTime(loopStateName, 0.08f, 0, 0f);
    }

    private void KeepLoopClipPlaying()
    {
        if (!addPhaseActive || !looping || !playable.IsValid() || currentLoopClip == null)
            return;

        double length = Mathf.Max(0.01f, currentLoopClip.length);
        if (playable.GetTime() >= length)
            playable.SetTime(0d);
    }

    private void StopLoopAnimation()
    {
        looping = false;
        currentLoopClip = null;

        if (playable.IsValid())
            playable.Destroy();

        if (graph.IsValid())
            graph.Stop();
    }

    private void CreateGraphIfNeeded()
    {
        if (graph.IsValid())
            return;

        graph = PlayableGraph.Create(name + "_BossAddPhaseGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "BossAddPhase", animator);
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            children[i].gameObject.layer = layer;
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
