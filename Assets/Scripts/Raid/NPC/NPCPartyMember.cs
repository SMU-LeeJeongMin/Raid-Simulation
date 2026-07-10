// NPC 정보 스크립트

using UnityEngine;

[RequireComponent(typeof(PlayerClassInfo))]
public class NPCPartyMember : MonoBehaviour
{
    [Header("Identity")]
    public int partyIndex = -1;
    public string characterId;
    public string displayName;
    public string roleName;

    [Header("References")]
    public PlayerClassInfo classInfo;
    public PlayerStatus playerStatus;
    public Health health;
    public Mana mana;
    public ShieldResource shield;
    public Animator animator;
    public PlayerBasicAttack basicAttack;
    public PlayerSkillController skillController;
    public PlayerMovement playerMovement;

    [Header("Runtime Flags")]
    public bool disablePlayerInputComponents = true;
    public bool disableRootMotion = true;
    public bool makeRigidbodiesKinematic = true;

    public bool IsDead => health != null && health.IsDead;

    public void Initialize(int index, CharacterLineupEntry entry)
    {
        partyIndex = index;

        if (entry != null)
        {
            characterId = entry.characterId;
            displayName = entry.jobName;
            roleName = entry.roleName;
        }

        ResolveReferences();
        ApplyIdentityToClassInfo();
        PrepareAsNPC();
    }

    public void ResolveReferences()
    {
        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = gameObject.AddComponent<PlayerStatus>();

        playerStatus.ResolveReferences();
        playerStatus.ApplyStatusSettings();

        if (health == null)
            health = playerStatus.Health != null ? playerStatus.Health : GetComponent<Health>();

        if (mana == null)
            mana = playerStatus.Mana != null ? playerStatus.Mana : GetComponent<Mana>();

        if (shield == null)
            shield = playerStatus.Shield != null ? playerStatus.Shield : GetComponent<ShieldResource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        if (skillController == null)
            skillController = GetComponent<PlayerSkillController>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void ApplyIdentityToClassInfo()
    {
        if (classInfo == null)
            return;

        if (!string.IsNullOrWhiteSpace(characterId))
            classInfo.characterId = characterId;

        if (!string.IsNullOrWhiteSpace(displayName))
            classInfo.jobName = displayName;

        if (!string.IsNullOrWhiteSpace(roleName))
            classInfo.roleName = roleName;
    }

    private void PrepareAsNPC()
    {
        if (disablePlayerInputComponents)
            DisableHumanInputOnly();

        if (disableRootMotion && animator != null)
            animator.applyRootMotion = false;

        if (makeRigidbodiesKinematic)
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
    }

    private void DisableHumanInputOnly()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (basicAttack != null)
        {
            basicAttack.enabled = true;
            basicAttack.useLeftMouseButton = false;
            basicAttack.useKeyboardNumber1 = false;
            basicAttack.ignoreInputWhenPointerIsOverUI = false;
            basicAttack.SetExternalActionLock(false);
        }

        if (skillController != null)
        {
            skillController.enabled = true;
            skillController.useKeyboardInput = false;
            skillController.ignoreInputWhenPointerIsOverUI = false;
        }
    }
}
