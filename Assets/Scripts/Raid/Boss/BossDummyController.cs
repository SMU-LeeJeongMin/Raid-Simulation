// 보스 상태 스크립트
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossDummyController : MonoBehaviour
{
    [Header("Boss Info")]
    public string bossName = "Dragon Boss";

    [Header("References")]
    public Health health;
    public Animator animator;

    [Header("Animator")]
    public bool disableRootMotion = true;
    public string idleStateName = "";
    public string hitTriggerName = "";

    [Header("Death Animation")]
    public AnimationClip deathClip;
    public string deathStateName = "Death";
    public string deathTriggerName = "";
    public bool useDirectDeathClipPlayback = true;
    public float deathDurationOverride = 0f;
    public bool holdDeathPose = true;

    [Header("Physics")]
    public bool makeRigidbodiesKinematic = true;
    public bool disableRigidbodyGravity = true;

    [Header("Death")]
    public bool disableCollidersOnDeath = true;
    public bool keepTriggerCollidersOnDeath = false;
    public bool disablePatternControllersOnDeath = true;
    public bool setIgnoreRaycastLayerOnDeath = true;
    public string deathLayerName = "Ignore Raycast";

    private Collider[] cachedColliders;
    private Rigidbody[] cachedRigidbodies;
    private bool deathHandled;

    // 사망 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer deathClipPlayer;
    private SingleClipPlayer DeathClipPlayer => deathClipPlayer ??= new SingleClipPlayer(name + "_DeathGraph", "Death");

    private void Awake()
    {
        CombatRegistry.Register(this);

        if (health == null)
            health = GetComponent<Health>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);

        ConfigureAnimator();
        ConfigureRigidbodies();
        EnsureDamageReceivers();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null)
        {
            health.onDamaged.AddListener(OnDamaged);
            health.onDeath.AddListener(OnDeath);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onDamaged.RemoveListener(OnDamaged);
            health.onDeath.RemoveListener(OnDeath);
        }
    }

    private void Start()
    {
        if (!deathHandled && (health == null || !health.IsDead))
            PlayIdleIfConfigured();
    }

    private void OnDamaged(Health damagedHealth, float amount, GameObject source)
    {
        if (deathHandled)
            return;
        TrySetTrigger(hitTriggerName);
    }

    private void OnDeath(Health deadHealth)
    {
        if (deathHandled)
            return;

        deathHandled = true;

        if (health != null)
        {
            health.ignoreDamageAfterDeath = true;
            health.destroyOnDeath = false;
        }

        ForceStopPatternControllersForDeath();

        if (disablePatternControllersOnDeath)
            DisableBossPatternControllers();

        if (setIgnoreRaycastLayerOnDeath)
            SetLayerRecursive(deathLayerName);

        if (disableCollidersOnDeath)
            SetCollidersEnabled(false);

        PlayDeathAnimation();
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
            return;

        if (disableRootMotion)
            animator.applyRootMotion = false;
    }

    private void ConfigureRigidbodies()
    {
        if (cachedRigidbodies == null)
            return;

        foreach (Rigidbody body in cachedRigidbodies)
        {
            if (body == null)
                continue;

            if (makeRigidbodiesKinematic)
                body.isKinematic = true;

            if (disableRigidbodyGravity)
                body.useGravity = false;
        }
    }

    private void EnsureDamageReceivers()
    {
        if (cachedColliders == null)
            return;

        foreach (Collider col in cachedColliders)
        {
            if (col == null)
                continue;

            DamageReceiver receiver = col.GetComponent<DamageReceiver>();
            if (receiver == null)
                receiver = col.gameObject.AddComponent<DamageReceiver>();

            receiver.targetHealth = health;
        }
    }

    private void PlayIdleIfConfigured()
    {
        if (animator == null || string.IsNullOrEmpty(idleStateName))
            return;

        animator.Play(idleStateName, 0, 0f);
    }

    private void PlayDeathAnimation()
    {
        if (animator == null)
            return;

        animator.enabled = true;
        if (disableRootMotion)
            animator.applyRootMotion = false;

        if (useDirectDeathClipPlayback && deathClip != null)
        {
            StartCoroutine(PlayDeathClipRoutine());
            return;
        }

        if (!string.IsNullOrWhiteSpace(deathStateName))
        {
            string resolved = ResolveAnimatorStateName(deathStateName);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                animator.speed = 1f;
                animator.CrossFadeInFixedTime(resolved, 0.05f, 0, 0f);
                if (holdDeathPose)
                    StartCoroutine(FreezeAnimatorAfterDeathState());
                return;
            }
        }

        TrySetTrigger(deathTriggerName);
    }

    private IEnumerator PlayDeathClipRoutine()
    {
        float duration = deathDurationOverride > 0f ? deathDurationOverride : deathClip.length;
        duration = Mathf.Max(0.05f, duration);

        DeathClipPlayer.PlayTimed(animator, deathClip, duration);

        yield return new WaitForSeconds(duration);

        if (holdDeathPose)
            DeathClipPlayer.FreezeAtEnd();
        else
            DeathClipPlayer.Stop();
    }

    private string ResolveAnimatorStateName(string stateName)
    {
        return AnimatorStateUtility.ResolveStateName(animator, stateName);
    }

    private void TrySetTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return;

        animator.SetTrigger(triggerName);
    }

    private IEnumerator FreezeAnimatorAfterDeathState()
    {
        float wait = deathDurationOverride > 0f ? deathDurationOverride : 2.0f;
        yield return new WaitForSeconds(wait);

        if (animator != null && deathHandled)
            animator.speed = 0f;
    }

    private void ForceStopPatternControllersForDeath()
    {
        BossBasicPatternController basic = GetComponent<BossBasicPatternController>();
        if (basic != null)
        {
            basic.SetSpecialPatternLock(true);
            basic.ForceStopCurrentAction(false);
        }
    }

    private void DisableBossPatternControllers()
    {
        BossBasicPatternController basic = GetComponent<BossBasicPatternController>();
        if (basic != null)
            basic.enabled = false;

        BossSkillPatternController skill = GetComponent<BossSkillPatternController>();
        if (skill != null)
            skill.enabled = false;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (cachedColliders == null)
            return;

        foreach (Collider col in cachedColliders)
        {
            if (col == null)
                continue;

            if (keepTriggerCollidersOnDeath && col.isTrigger)
                continue;

            col.enabled = enabled;
        }
    }

    private void SetLayerRecursive(string layerName)
    {
        LayerUtility.SetLayerRecursively(gameObject, layerName);
    }

    private void OnDestroy()
    {
        CombatRegistry.Unregister(this);
        deathClipPlayer?.Dispose();
    }
}
