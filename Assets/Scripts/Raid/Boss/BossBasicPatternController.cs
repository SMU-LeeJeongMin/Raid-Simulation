// 보스 기본 공격
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossBasicPatternController : MonoBehaviour
{
    [System.Serializable]
    public class BossAttackPattern
    {
        [Header("Info")]
        public string displayName = "Attack";

        [Header("Animation")]
        public AnimationClip animationClip;
        public string animatorStateName = "Attack1";
        public float durationOverride = 0f;
        [Range(0f, 1f)] public float impactNormalizedTime = 0.55f;

        [Header("Damage")]
        public float damage = 25f;
        public float range = 5.0f;
        public float hitRadius = 3.0f;
        [Range(1f, 360f)] public float hitAngle = 140f;
        public bool requireTargetInFront = true;

        [Header("Animated Foot Hit Point")]
        public Transform hitPoint;
        public Transform followBone;
        public Vector3 boneLocalOffset = Vector3.zero;
        public bool syncHitPointToBone = true;
        public bool useFollowBoneWhenHitPointMissing = true;

        [Header("Player Hit VFX")]
        public GameObject playerHitVFXPrefab;
        public string playerHitVFXAnchorName = "VFXHitAnchor";
        public Vector3 playerHitVFXOffset = new Vector3(0f, 0.8f, 0f);
        public Vector3 playerHitVFXEuler = Vector3.zero;
        public Vector3 playerHitVFXScale = Vector3.one;
        public bool attachPlayerHitVFXToTarget = false;
        [Min(0.05f)] public float playerHitVFXAutoDestroyDelay = 3f;
    }

    [Header("Target")]
    public PlayerStatus targetPlayer;
    public bool autoFindPlayer = true;
    public float targetRefreshInterval = 0.5f;

    [Header("Engage / Start Condition")]
    public bool waitForBossCameraMode = true;
    public RaidCameraFollow raidCamera;
    public bool autoFindRaidCamera = true;
    public bool stayEngagedAfterStart = true;
    public float fallbackEngageDistance = 12f;

    [Header("Movement")]
    public bool canMove = true;
    public float moveSpeed = 1.8f;
    public float rotationSpeed = 160f;
    public float attackStartDistance = 4.5f;
    public float stopDistance = 3.7f;
    public bool useHorizontalDistance = true;

    [Header("Attack")]
    public float attackCooldown = 1.25f;
    public bool alternateAttacks = true;
    public BossAttackPattern attack1 = new BossAttackPattern
    {
        displayName = "Attack1_RightFoot",
        animatorStateName = "Attack1"
    };
    public BossAttackPattern attack2 = new BossAttackPattern
    {
        displayName = "Attack2_LeftFoot",
        animatorStateName = "Attack2"
    };

    [Header("Animator")]
    public Animator animator;
    public bool useDirectClipPlayback = true;
    public bool disableRootMotion = true;
    public bool loopIdleAndMoveClips = true;
    public AnimationClip idleClip;
    public string idleStateName = "Idle";
    public AnimationClip walkForwardClip;
    public AnimationClip walkForwardLeftClip;
    public AnimationClip walkForwardRightClip;
    public float moveClipSpeed = 1f;
    public float defaultAttackDuration = 1.2f;

    [Header("Debug")]
    public bool drawGizmos = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool engaged;
    [SerializeField] private bool isAttacking;
    [SerializeField] private bool specialPatternLock;
    [SerializeField] private string currentAnimationLabel;
    [SerializeField] private float distanceToPlayer;

    public bool IsAttacking => isAttacking;
    public bool IsSpecialPatternLocked => specialPatternLock;
    public float DistanceToPlayer => distanceToPlayer;

    public void SetSpecialPatternLock(bool locked)
    {
        specialPatternLock = locked;
    }

    // 보스 스킬/페이즈 전환/사망처럼 더 높은 우선순위의 상태가 시작될 때, 현재 행동 즉시정지
    public void ForceStopCurrentAction(bool playIdleAfterStop = false)
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;
        StopClip();

        if (playIdleAfterStop && enabled && gameObject.activeInHierarchy && (health == null || !health.IsDead))
            PlayIdle();
    }

    private Health health;
    private float nextTargetSearchTime;
    private float nextAttackAllowedTime;
    private bool useAttack1Next = true;
    private Coroutine attackRoutine;

    // 패턴 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer clipPlayer;
    private SingleClipPlayer ClipPlayer => clipPlayer ??= new SingleClipPlayer(name + "_BossBasicPatternGraph", "BossBasicPattern");

    private void Awake()
    {
        health = GetComponent<Health>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;

        if (raidCamera == null && autoFindRaidCamera && Camera.main != null)
            raidCamera = Camera.main.GetComponent<RaidCameraFollow>();
    }

    private void Start()
    {
        SyncAllHitPointsToBones();
        PlayIdle();
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        FindPlayerIfNeeded();
        SyncAllHitPointsToBones();
        UpdateEngageState();

        if (!engaged)
        {
            PlayIdle();
            return;
        }

        if (targetPlayer == null || targetPlayer.Health == null || targetPlayer.Health.IsDead)
        {
            PlayIdle();
            return;
        }

        if (specialPatternLock && !isAttacking)
        {
            KeepClipLoopingIfNeeded();
            return;
        }

        if (isAttacking)
        {
            KeepClipLoopingIfNeeded();
            return;
        }

        Vector3 targetPosition = targetPlayer.transform.position;
        distanceToPlayer = GetDistanceToTarget(targetPosition);

        RotateToward(targetPosition);

        if (distanceToPlayer <= attackStartDistance && Time.time >= nextAttackAllowedTime)
        {
            BossAttackPattern pattern = ChooseAttackPattern();
            attackRoutine = StartCoroutine(AttackRoutine(pattern));
            return;
        }

        if (canMove && distanceToPlayer > stopDistance)
        {
            MoveToward(targetPosition);
            PlayMoveAnimation(targetPosition);
        }
        else
        {
            PlayIdle();
        }

        KeepClipLoopingIfNeeded();
    }

    private void FindPlayerIfNeeded()
    {
        if (!autoFindPlayer)
            return;

        if (Time.time < nextTargetSearchTime && PartyTargetUtility.IsValidPlayerTarget(targetPlayer))
            return;

        nextTargetSearchTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
        targetPlayer = FindClosestPlayerTarget(transform.position);
    }

    private PlayerStatus FindClosestPlayerTarget(Vector3 fromPosition)
    {
        IReadOnlyList<PlayerStatus> players = CombatRegistry.PlayerStatuses;
        float bestDistance = float.PositiveInfinity;
        PlayerStatus best = null;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerStatus player = players[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(player))
                continue;

            Vector3 delta = player.transform.position - fromPosition;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                best = player;
            }
        }

        return best;
    }

    private void UpdateEngageState()
    {
        if (engaged && stayEngagedAfterStart)
            return;

        bool shouldEngage = true;

        if (waitForBossCameraMode)
        {
            if (raidCamera == null && autoFindRaidCamera && Camera.main != null)
                raidCamera = Camera.main.GetComponent<RaidCameraFollow>();

            if (raidCamera != null)
            {
                shouldEngage = raidCamera.IsBossCameraActive(0.15f);
            }
            else if (targetPlayer != null)
            {
                shouldEngage = GetDistanceToTarget(targetPlayer.transform.position) <= fallbackEngageDistance;
            }
            else
            {
                shouldEngage = false;
            }
        }
        engaged = shouldEngage;
    }

    public void SetEngaged(bool value)
    {
        engaged = value;
    }

    private BossAttackPattern ChooseAttackPattern()
    {
        if (!alternateAttacks)
            return attack1;

        BossAttackPattern chosen = useAttack1Next ? attack1 : attack2;
        useAttack1Next = !useAttack1Next;
        return chosen;
    }

    private IEnumerator AttackRoutine(BossAttackPattern pattern)
    {
        if (pattern == null || targetPlayer == null)
            yield break;

        isAttacking = true;
        nextAttackAllowedTime = Time.time + attackCooldown;

        RotateToward(targetPlayer.transform.position);
        SyncHitPointToBone(pattern);

        float duration = GetAttackDuration(pattern);
        float impactTime = Mathf.Clamp01(pattern.impactNormalizedTime) * duration;

        PlayAttackAnimation(pattern, duration);

        if (impactTime > 0f)
            yield return new WaitForSeconds(impactTime);

        SyncHitPointToBone(pattern);
        TryApplyAttackDamage(pattern);

        float remaining = Mathf.Max(0f, duration - impactTime);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        PlayIdle();
        isAttacking = false;
        attackRoutine = null;
    }

    private float GetAttackDuration(BossAttackPattern pattern)
    {
        if (pattern.durationOverride > 0f)
            return pattern.durationOverride;

        if (pattern.animationClip != null && pattern.animationClip.length > 0f)
            return pattern.animationClip.length;

        return Mathf.Max(0.05f, defaultAttackDuration);
    }

    private void TryApplyAttackDamage(BossAttackPattern pattern)
    {
        IReadOnlyList<PlayerStatus> targets = CombatRegistry.PlayerStatuses;

        if (targets.Count == 0)
            return;

        Vector3 origin = GetPatternHitOrigin(pattern);

        for (int i = 0; i < targets.Count; i++)
        {
            PlayerStatus player = targets[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(player))
                continue;

            Vector3 target = player.transform.position;
            Vector3 flatDelta = target - origin;
            flatDelta.y = 0f;

            if (flatDelta.magnitude > pattern.hitRadius)
                continue;

            if (pattern.requireTargetInFront)
            {
                Vector3 bossForward = transform.forward;
                bossForward.y = 0f;
                Vector3 toPlayerFromBoss = target - transform.position;
                toPlayerFromBoss.y = 0f;

                if (bossForward.sqrMagnitude > 0.0001f && toPlayerFromBoss.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(bossForward.normalized, toPlayerFromBoss.normalized);
                    if (angle > pattern.hitAngle * 0.5f)
                        continue;
                }
            }

            player.TakeDamage(pattern.damage, gameObject);
            SpawnPlayerHitVFX(pattern, player);
        }
    }

    private void SpawnPlayerHitVFX(BossAttackPattern pattern, PlayerStatus player)
    {
        if (pattern == null || player == null || pattern.playerHitVFXPrefab == null)
            return;

        Transform attachParent;
        Vector3 position = GetPlayerHitVFXPosition(pattern, player, out attachParent);
        Quaternion rotation = Quaternion.Euler(pattern.playerHitVFXEuler);

        GameObject instance = Instantiate(pattern.playerHitVFXPrefab, position, rotation);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, pattern.playerHitVFXScale);

        RestartParticleSystems(instance);

        if (pattern.attachPlayerHitVFXToTarget && attachParent != null)
            instance.transform.SetParent(attachParent, true);

        if (pattern.playerHitVFXAutoDestroyDelay > 0f)
            Destroy(instance, pattern.playerHitVFXAutoDestroyDelay);
    }

    private Vector3 GetPlayerHitVFXPosition(BossAttackPattern pattern, PlayerStatus player, out Transform attachParent)
    {
        attachParent = player != null ? player.transform : null;

        if (player == null)
            return transform.position;

        if (!string.IsNullOrWhiteSpace(pattern.playerHitVFXAnchorName))
        {
            Transform anchor = FindChildByName(player.transform, pattern.playerHitVFXAnchorName);
            if (anchor != null)
            {
                attachParent = anchor;
                return anchor.position + pattern.playerHitVFXOffset;
            }
        }

        Collider[] colliders = player.GetComponentsInChildren<Collider>(true);
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
                return bounds.center + pattern.playerHitVFXOffset;
        }

        return player.transform.position + Vector3.up * 0.8f + pattern.playerHitVFXOffset;
    }

    // 공용 VFX 유틸 위임 (기존 호출부 유지용 래퍼)
    private Transform FindChildByName(Transform root, string childName)
    {
        return VFXUtility.FindChildByName(root, childName);
    }

    private void RestartParticleSystems(GameObject root)
    {
        VFXUtility.RestartParticles(root);
    }

    private Vector3 GetPatternHitOrigin(BossAttackPattern pattern)
    {
        if (pattern == null)
            return transform.position;

        if (pattern.hitPoint != null)
            return pattern.hitPoint.position;

        if (pattern.useFollowBoneWhenHitPointMissing && pattern.followBone != null)
            return pattern.followBone.TransformPoint(pattern.boneLocalOffset);

        return transform.position + transform.forward * Mathf.Min(pattern.range, 2f);
    }

    private void SyncAllHitPointsToBones()
    {
        SyncHitPointToBone(attack1);
        SyncHitPointToBone(attack2);
    }

    private void SyncHitPointToBone(BossAttackPattern pattern)
    {
        if (pattern == null || !pattern.syncHitPointToBone)
            return;

        if (pattern.hitPoint == null || pattern.followBone == null)
            return;

        pattern.hitPoint.position = pattern.followBone.TransformPoint(pattern.boneLocalOffset);
        pattern.hitPoint.rotation = pattern.followBone.rotation;
    }

    private float GetDistanceToTarget(Vector3 targetPosition)
    {
        Vector3 a = transform.position;
        Vector3 b = targetPosition;

        if (useHorizontalDistance)
        {
            a.y = 0f;
            b.y = 0f;
        }

        return Vector3.Distance(a, b);
    }

    private void RotateToward(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void MoveToward(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.position += direction.normalized * moveSpeed * Time.deltaTime;
    }

    private void PlayIdle()
    {
        if (idleClip != null)
            PlayClip(idleClip, 1f, true, "Idle");
        else
            PlayAnimatorState(idleStateName, true, "Idle");
    }

    private void PlayMoveAnimation(Vector3 targetPosition)
    {
        AnimationClip clip = SelectMoveClip(targetPosition);
        if (clip != null)
            PlayClip(clip, moveClipSpeed, true, clip.name);
        else
            PlayIdle();
    }

    private AnimationClip SelectMoveClip(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return walkForwardClip;

        float signedAngle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);

        if (signedAngle > 20f && walkForwardRightClip != null)
            return walkForwardRightClip;

        if (signedAngle < -20f && walkForwardLeftClip != null)
            return walkForwardLeftClip;

        return walkForwardClip != null ? walkForwardClip : idleClip;
    }

    private void PlayAttackAnimation(BossAttackPattern pattern, float duration)
    {
        if (pattern.animationClip != null && useDirectClipPlayback)
        {
            PlayClip(pattern.animationClip, pattern.animationClip.length / Mathf.Max(0.05f, duration), false, pattern.displayName);
            return;
        }

        PlayAnimatorState(pattern.animatorStateName, false, pattern.displayName);
    }

    private void PlayClip(AnimationClip clip, float speed, bool loop, string label)
    {
        if (animator == null || clip == null)
            return;

        // 같은 클립이 같은 루프 설정으로 재생 중이면 재시작하지 않음
        if (ClipPlayer.PlayIfChanged(animator, clip, speed, loop))
            currentAnimationLabel = label;
    }

    private void PlayAnimatorState(string stateName, bool loop, string label)
    {
        StopClip();

        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        animator.enabled = true;
        animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
        currentAnimationLabel = label;
    }

    private void StopClip()
    {
        clipPlayer?.Stop();
    }

    private void KeepClipLoopingIfNeeded()
    {
        if (loopIdleAndMoveClips)
            clipPlayer?.Tick();
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopClip();
        isAttacking = false;
        specialPatternLock = false;
    }

    private void OnDestroy()
    {
        clipPlayer?.Dispose();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackStartDistance);

        DrawAttackGizmo(attack1, Color.yellow);
        DrawAttackGizmo(attack2, Color.cyan);
    }

    private void DrawAttackGizmo(BossAttackPattern pattern, Color color)
    {
        if (pattern == null)
            return;

        Gizmos.color = color;
        Vector3 origin = GetPatternHitOrigin(pattern);
        Gizmos.DrawWireSphere(origin, pattern.hitRadius);
    }
}
