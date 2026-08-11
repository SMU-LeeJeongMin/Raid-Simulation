// 플레이어/NPC HP/MP/Shield/Ultimate 관리
// Health.cs, Mana.cs, ShieldResource.cs, PlayerUltimateGauge.cs와 연결

using UnityEngine;
using UnityEngine.AI;

public class PlayerStatus : MonoBehaviour
{
    [Header("References")]
    public Health health;
    public Mana mana;
    public ShieldResource shield;
    public PlayerUltimateGauge ultimateGauge;
    public PlayerClassInfo classInfo;

    [Header("Health Setup")]
    public bool createHealthIfMissing = true;
    public bool applyHealthOnAwake = true;
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float startingHealth = 100f;
    public bool startWithFullHealth = true;
    public bool ignoreDamageAfterDeath = true;
    public bool destroyOnDeath = false;
    public bool logDamage = true;

    [Header("Mana Setup")]
    public bool createManaIfMissing = true;
    public bool applyManaOnAwake = true;
    [Min(1f)] public float maxMana = 100f;
    [Min(0f)] public float startingMana = 100f;
    public bool startWithFullMana = true;
    public bool regenerateMana = true;
    [Min(0f)] public float manaRegenPerSecond = 8f;
    [Min(0f)] public float manaRegenDelayAfterUse = 0.5f;
    public bool logManaChanges = true;

    [Header("Shield Setup")]
    public bool createShieldIfMissing = true;
    public bool logShieldChanges = true;

    [Header("Ultimate Setup")]
    public bool createUltimateGaugeIfMissing = true;
    public bool applyUltimateOnAwake = true;
    [Min(1f)] public float maxUltimateGauge = 100f;
    public bool startFullUltimateForTesting = false;
    public bool logUltimateChanges = true;

    [Header("Death")]
    public bool playDeathAnimation = true;
    public AnimationClip deathClip;
    public string deathStateName = "Death";
    public bool useDirectDeathClipPlayback = true;
    public bool freezeOnFinalDeathPose = true;
    public bool disableControlOnDeath = true;
    public bool disableCharacterControllerOnDeath = true;
    public bool disableNavMeshAgentOnDeath = true;
    public bool makeRigidbodiesKinematicOnDeath = true;
    public bool disableCollidersOnDeath = false;
    public bool keepTriggerCollidersOnDeath = true;
    public bool logDeath = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool deathHandled;

    public Health Health => health;
    public Mana Mana => mana;
    public ShieldResource Shield => shield;
    public PlayerUltimateGauge UltimateGauge => ultimateGauge;
    public PlayerClassInfo ClassInfo => classInfo;
    public bool IsDead => health != null && health.IsDead;

    // 보스 더미 여부 캐시 (대상 판정 시 GetComponent 반복 호출 제거)
    private bool bossDummyResolved;
    private bool isBossDummy;
    public bool IsBossDummy
    {
        get
        {
            if (!bossDummyResolved)
            {
                isBossDummy = GetComponent<BossDummyController>() != null;
                bossDummyResolved = true;
            }

            return isBossDummy;
        }
    }

    private Animator animator;
    // 사망 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer deathClipPlayer;
    private SingleClipPlayer DeathClipPlayer => deathClipPlayer ??= new SingleClipPlayer(name + "_DeathGraph", "Death");

    private void Awake()
    {
        CombatRegistry.Register(this);
        ResolveReferences();
        ApplyStatusSettings();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeHealthEvents();
    }

    private void OnDisable()
    {
        UnsubscribeHealthEvents();
    }

    private void Update()
    {
        FreezeDeathClipAtEndIfNeeded();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health == null && createHealthIfMissing)
            health = gameObject.AddComponent<Health>();

        if (mana == null)
            mana = GetComponent<Mana>();

        if (mana == null && createManaIfMissing)
            mana = gameObject.AddComponent<Mana>();

        if (shield == null)
            shield = GetComponent<ShieldResource>();

        if (shield == null && createShieldIfMissing)
            shield = gameObject.AddComponent<ShieldResource>();

        if (ultimateGauge == null)
            ultimateGauge = GetComponent<PlayerUltimateGauge>();

        if (ultimateGauge == null && createUltimateGaugeIfMissing)
            ultimateGauge = gameObject.AddComponent<PlayerUltimateGauge>();

        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    public void ApplyStatusSettings()
    {
        ResolveReferences();

        if (health != null && applyHealthOnAwake)
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            health.maxHealth = maxHealth;
            health.startWithFullHealth = startWithFullHealth;
            health.ignoreDamageAfterDeath = ignoreDamageAfterDeath;
            health.destroyOnDeath = destroyOnDeath;

            if (startWithFullHealth)
                health.ResetHealth();
            else
                health.SetHealth(Mathf.Clamp(startingHealth, 0f, maxHealth));
        }

        if (mana != null && applyManaOnAwake)
        {
            maxMana = Mathf.Max(1f, maxMana);
            mana.maxMana = maxMana;
            mana.startWithFullMana = startWithFullMana;
            mana.regenerate = regenerateMana;
            mana.regenPerSecond = manaRegenPerSecond;
            mana.regenDelayAfterUse = manaRegenDelayAfterUse;

            if (startWithFullMana)
                mana.Refill();
            else
                mana.SetMana(Mathf.Clamp(startingMana, 0f, maxMana));
        }

        if (ultimateGauge != null && applyUltimateOnAwake)
        {
            ultimateGauge.maxGauge = Mathf.Max(1f, maxUltimateGauge);
            ultimateGauge.startFullForTesting = startFullUltimateForTesting;
            ultimateGauge.SetGauge(startFullUltimateForTesting ? ultimateGauge.maxGauge : ultimateGauge.CurrentGauge);
        }

        deathHandled = health != null && health.IsDead;
        SubscribeHealthEvents();
    }

    private void SubscribeHealthEvents()
    {
        if (health == null)
            return;

        health.onDeath.RemoveListener(OnDeath);
        health.onDeath.AddListener(OnDeath);
    }

    private void UnsubscribeHealthEvents()
    {
        if (health == null)
            return;

        health.onDeath.RemoveListener(OnDeath);
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        ResolveReferences();

        if (health != null && health.IsDead)
            return;

        float rawDamage = Mathf.Max(0f, amount);
        if (rawDamage <= 0f)
            return;

        float hpBefore = health != null ? health.CurrentHealth : 0f;
        float remainingDamage = rawDamage;

        if (shield != null)
            remainingDamage = shield.AbsorbDamage(remainingDamage);

        float shieldAbsorbed = Mathf.Max(0f, rawDamage - remainingDamage);

        if (remainingDamage > 0f && health != null)
            health.TakeDamage(remainingDamage, source);

        float hpAfter = health != null ? health.CurrentHealth : hpBefore;
        float hpDamage = Mathf.Max(0f, hpBefore - hpAfter);
        RaidMetricsEvents.ReportPlayerDamageResolved(this, rawDamage, shieldAbsorbed, hpDamage, hpBefore, hpAfter, source);
    }

    public void Heal(float amount, GameObject source = null)
    {
        ResolveReferences();
        if (health == null)
            return;

        float requestedHeal = Mathf.Max(0f, amount);
        if (requestedHeal <= 0f)
            return;

        float hpBefore = health.CurrentHealth;
        health.Heal(requestedHeal, source);
        float hpAfter = health.CurrentHealth;
        float actualHeal = Mathf.Max(0f, hpAfter - hpBefore);
        RaidMetricsEvents.ReportPlayerHealed(this, requestedHeal, actualHeal, hpBefore, hpAfter, source);
    }

    public bool ConsumeMana(float amount)
    {
        ResolveReferences();
        return mana != null && mana.Consume(amount);
    }

    public void RestoreMana(float amount)
    {
        ResolveReferences();
        if (mana != null)
            mana.Restore(amount);
    }

    public void AddShield(float amount, float duration)
    {
        AddShield(amount, duration, null);
    }

    public void AddShield(float amount, float duration, GameObject source)
    {
        ResolveReferences();
        if (shield == null)
            return;

        shield.AddShield(amount, duration);
        RaidMetricsEvents.ReportShieldAdded(this, Mathf.Max(0f, amount), Mathf.Max(0f, duration), source);
    }

    public void GainUltimate(float amount)
    {
        ResolveReferences();
        if (ultimateGauge != null)
            ultimateGauge.Gain(amount);
    }

    public bool ConsumeUltimate(float amount)
    {
        ResolveReferences();
        return ultimateGauge != null && ultimateGauge.Consume(amount);
    }

    private void OnDeath(Health deadHealth)
    {
        if (deathHandled)
            return;

        deathHandled = true;

        if (logDeath)
            Debug.Log($"[PlayerStatus] {name} dead.", this);

        if (disableControlOnDeath)
            DisableControlComponents();

        if (makeRigidbodiesKinematicOnDeath)
            MakeRigidbodiesKinematic();

        if (disableCharacterControllerOnDeath)
            DisableComponent<CharacterController>();

        if (disableNavMeshAgentOnDeath)
            DisableComponent<NavMeshAgent>();

        if (disableCollidersOnDeath)
            SetCollidersEnabled(false);

        if (playDeathAnimation)
            PlayDeathAnimation();
    }

    private void DisableControlComponents()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        PlayerBasicAttack basicAttack = GetComponent<PlayerBasicAttack>();
        if (basicAttack != null)
            basicAttack.enabled = false;

        PlayerSkillController skillController = GetComponent<PlayerSkillController>();
        if (skillController != null)
            skillController.enabled = false;

        NPCSimpleFSMController npc = GetComponent<NPCSimpleFSMController>();
        if (npc != null)
            npc.enabled = false;
    }

    private void DisableComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
            return;

        if (component is Behaviour behaviour)
        {
            behaviour.enabled = false;
            return;
        }

        if (component is Collider collider)
        {
            collider.enabled = false;
            return;
        }

        if (component is Renderer renderer)
        {
            renderer.enabled = false;
            return;
        }
    }

    private void MakeRigidbodiesKinematic()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
                continue;

            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            if (keepTriggerCollidersOnDeath && col.isTrigger)
                continue;

            col.enabled = enabled;
        }
    }

    private void PlayDeathAnimation()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            return;

        animator.enabled = true;
        animator.applyRootMotion = false;

        if (useDirectDeathClipPlayback && deathClip != null)
        {
            DeathClipPlayer.Play(animator, deathClip, 1f, false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(deathStateName))
            animator.CrossFadeInFixedTime(deathStateName, 0.05f, 0, 0f);
    }

    private void FreezeDeathClipAtEndIfNeeded()
    {
        if (!freezeOnFinalDeathPose)
            return;

        deathClipPlayer?.FreezeAtEndIfFinished();
    }

    private void OnDestroy()
    {
        CombatRegistry.Unregister(this);
        deathClipPlayer?.Dispose();
    }
}
