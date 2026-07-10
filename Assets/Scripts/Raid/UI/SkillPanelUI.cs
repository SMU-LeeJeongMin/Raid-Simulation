// 스킬 panel
// 오른쪽 하단

using System.Collections;
using UnityEngine;

public class SkillPanelUI : MonoBehaviour
{
    [Header("Target")]
    public PlayerSkillController skillController;
    public RaidSelectedCharacterSpawner playerSpawner;
    public bool autoFindPlayer = true;
    public bool keepTryingUntilBound = true;
    public float bindDelay = 0.2f;

    [Header("Icon Database")]
    public PlayerSkillIconDatabase iconDatabase;
    public bool refreshIconsOnBind = true;

    [Header("Buttons")]
    public SkillButtonUI skill1Button;
    public SkillButtonUI skill2Button;
    public SkillButtonUI ultimateButton;

    [Header("Availability")]
    public bool checkTargetRangeForDisabled = false;

    private Coroutine bindRoutine;
    private string lastIconCharacterId;

    private void Awake()
    {
        InitializeButtons();
    }

    private void Start()
    {
        if (skillController == null && autoFindPlayer)
            StartBinding();
        else
            Bind(skillController);
    }

    private void Update()
    {
        if (skillController == null && autoFindPlayer)
            TryBindNow();

        RefreshButtons();
    }

    public void StartBinding()
    {
        if (bindRoutine != null)
            StopCoroutine(bindRoutine);

        bindRoutine = StartCoroutine(BindRoutine());
    }

    private IEnumerator BindRoutine()
    {
        if (bindDelay > 0f)
            yield return new WaitForSeconds(bindDelay);

        do
        {
            if (TryBindNow())
                yield break;

            yield return new WaitForSeconds(0.25f);
        }
        while (keepTryingUntilBound);
    }

    public bool TryBindNow()
    {
        PlayerSkillController found = FindPlayerSkillController();
        if (found == null)
            return false;

        Bind(found);
        return true;
    }

    public void Bind(PlayerSkillController controller)
    {
        skillController = controller;
        InitializeButtons();
        RefreshIcons(true);
        RefreshButtons();
    }

    public void TryUseSkill(PlayerSkillSlot slot)
    {
        if (skillController == null)
            return;

        skillController.TryUseSkill(slot);
        RefreshButtons();
    }

    private void InitializeButtons()
    {
        if (skill1Button != null)
            skill1Button.Initialize(this, PlayerSkillSlot.Skill1);

        if (skill2Button != null)
            skill2Button.Initialize(this, PlayerSkillSlot.Skill2);

        if (ultimateButton != null)
        {
            ultimateButton.Initialize(this, PlayerSkillSlot.Ultimate);
            ultimateButton.showUltimateGaugeText = true;
        }
    }

    private void RefreshButtons()
    {
        RefreshIcons(false);

        if (skill1Button != null)
            skill1Button.Refresh(skillController, checkTargetRangeForDisabled);

        if (skill2Button != null)
            skill2Button.Refresh(skillController, checkTargetRangeForDisabled);

        if (ultimateButton != null)
            ultimateButton.Refresh(skillController, checkTargetRangeForDisabled);
    }

    private void RefreshIcons(bool force)
    {
        if (!refreshIconsOnBind || iconDatabase == null || skillController == null)
            return;

        string characterId = ResolveCharacterId(skillController);
        if (!force && characterId == lastIconCharacterId)
            return;

        lastIconCharacterId = characterId;

        if (skill1Button != null)
            skill1Button.SetIcon(iconDatabase.GetIcon(characterId, PlayerSkillSlot.Skill1));

        if (skill2Button != null)
            skill2Button.SetIcon(iconDatabase.GetIcon(characterId, PlayerSkillSlot.Skill2));

        if (ultimateButton != null)
            ultimateButton.SetIcon(iconDatabase.GetIcon(characterId, PlayerSkillSlot.Ultimate));
    }

    private string ResolveCharacterId(PlayerSkillController controller)
    {
        if (controller == null)
            return SelectedCharacterMemory.LoadSelectedId("warrior");

        if (controller.classInfo != null && !string.IsNullOrWhiteSpace(controller.classInfo.characterId))
            return controller.classInfo.characterId;

        PlayerClassInfo info = controller.GetComponent<PlayerClassInfo>();
        if (info != null && !string.IsNullOrWhiteSpace(info.characterId))
            return info.characterId;

        return SelectedCharacterMemory.LoadSelectedId("warrior");
    }

    private PlayerSkillController FindPlayerSkillController()
    {
        if (skillController != null)
            return skillController;

        if (playerSpawner == null)
            playerSpawner = FindAnyObjectByType<RaidSelectedCharacterSpawner>();

        if (playerSpawner != null && playerSpawner.SpawnedPlayer != null)
        {
            PlayerSkillController fromSpawnedPlayer = playerSpawner.SpawnedPlayer.GetComponent<PlayerSkillController>();
            if (fromSpawnedPlayer != null)
                return fromSpawnedPlayer;
        }

        return FindAnyObjectByType<PlayerSkillController>();
    }
}
