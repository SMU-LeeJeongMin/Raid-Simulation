// NPC 파티원 3명의 HP/Shield UI 관리 panel

using System.Collections;
using UnityEngine;

public class PartyStatusPanelUI : MonoBehaviour
{
    [Header("References")]
    public NPCPartySpawner partySpawner;
    public PartyMemberStatusUI[] slots = new PartyMemberStatusUI[3];

    [Header("Auto Bind")]
    public bool autoBindOnStart = true;
    public float bindDelay = 0.35f;
    public bool keepTryingUntilBound = true;
    public float retryInterval = 0.25f;

    [Header("Options")]
    public bool autoFindSlotsInChildren = true;
    public bool hideUnusedSlots = true;
    public bool logBinding = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool isBound;
    [SerializeField] private int boundCount;

    private Coroutine bindRoutine;

    private void Awake()
    {
        AutoFindSlotsIfNeeded();
    }

    private void OnEnable()
    {
        if (autoBindOnStart)
            StartAutoBind();
    }

    private void Start()
    {
        if (autoBindOnStart)
            StartAutoBind();
    }

    [ContextMenu("Start Auto Bind")]
    public void StartAutoBind()
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
            bool bound = BindIfPossible();
            if (bound)
                yield break;

            yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
        }
        while (keepTryingUntilBound);
    }

    [ContextMenu("Bind If Possible")]
    public bool BindIfPossible()
    {
        AutoFindSlotsIfNeeded();

        if (partySpawner == null)
            partySpawner = FindAnyObjectByType<NPCPartySpawner>();

        if (partySpawner == null)
        {
            if (logBinding)
                Debug.LogWarning("[PartyStatusPanelUI] NPCPartySpawner was not found.", this);
            return false;
        }

        var members = partySpawner.PartyMembers;
        if (members == null || members.Count == 0)
        {
            if (logBinding)
                Debug.LogWarning("[PartyStatusPanelUI] Party members are not spawned yet.", this);
            return false;
        }

        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            PartyMemberStatusUI slot = slots[i];
            if (slot == null)
                continue;

            if (i < members.Count && members[i] != null)
            {
                slot.gameObject.SetActive(true);
                slot.Bind(members[i]);
                count++;
            }
            else
            {
                slot.Clear();
                if (hideUnusedSlots)
                    slot.gameObject.SetActive(false);
            }
        }

        boundCount = count;
        isBound = count > 0;

        if (logBinding)
            Debug.Log($"[PartyStatusPanelUI] Bound {count}/{members.Count} NPC party members.", this);

        return isBound;
    }

    private void AutoFindSlotsIfNeeded()
    {
        if (!autoFindSlotsInChildren)
            return;

        bool missing = slots == null || slots.Length == 0;
        if (!missing)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    missing = true;
                    break;
                }
            }
        }

        if (!missing)
            return;

        slots = GetComponentsInChildren<PartyMemberStatusUI>(true);
    }
}
