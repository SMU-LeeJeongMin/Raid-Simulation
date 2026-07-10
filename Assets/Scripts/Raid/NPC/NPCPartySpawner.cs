// 플레이어가 고르지 않은 캐릭터 3명을 NPC 파티원으로 생성하는 스크립트

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NPCPartySpawner : MonoBehaviour
{
    [Header("Data")]
    public CharacterLineupDatabase database;

    [Header("Player Reference")]
    public RaidSelectedCharacterSpawner playerSpawner;
    public Transform playerTransform;
    public bool waitForPlayerSpawner = true;
    public float spawnDelay = 0.15f;

    [Header("Spawn")]
    public bool spawnOnStart = true;
    public Transform partyParent;
    public Transform[] spawnPoints = new Transform[3];

    public Vector3[] fallbackFormationOffsets =
    {
        new Vector3(-1.4f, 0f, -1.2f),
        new Vector3(1.4f, 0f, -1.2f),
        new Vector3(0f, 0f, -2.2f)
    };

    [Header("NPC Setup")]
    public bool disablePlayerInputComponents = true;
    public bool useRaidPrefab = true;
    public bool addSimpleFSMController = true;
    public NPCFSMMode defaultAIMode = NPCFSMMode.PatternAwareFSM;

    [Header("Events")]
    public UnityEvent onPartySpawned;

    [Header("Runtime")]
    [SerializeField] private List<NPCPartyMember> partyMembers = new List<NPCPartyMember>();

    public IReadOnlyList<NPCPartyMember> PartyMembers => partyMembers;
    public int PartyCount => partyMembers.Count;

    private Coroutine spawnRoutine;

    private void Start()
    {
        if (spawnOnStart)
            SpawnPartyMembersDelayed();
    }

    [ContextMenu("Spawn Party Members Delayed")]
    public void SpawnPartyMembersDelayed()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        if (waitForPlayerSpawner)
        {
            if (playerSpawner == null)
                playerSpawner = FindAnyObjectByType<RaidSelectedCharacterSpawner>();

            float startTime = Time.time;
            while (playerSpawner != null && playerSpawner.SpawnedPlayer == null && Time.time - startTime < 5f)
                yield return null;
        }

        SpawnPartyMembers();
    }

    [ContextMenu("Spawn Party Members Now")]
    public void SpawnPartyMembers()
    {
        ClearExistingPartyMembers();

        if (database == null)
        {
            return;
        }

        ResolvePlayerTransform();

        int selectedIndex = SelectedCharacterMemory.LoadSelectedIndex(0);
        int spawnedCount = 0;

        for (int i = 0; i < database.Count; i++)
        {
            if (i == selectedIndex)
                continue;

            if (!database.IsValidIndex(i))
                continue;

            CharacterLineupEntry entry = database.Get(i);
            GameObject prefab = useRaidPrefab && entry.raidPrefab != null ? entry.raidPrefab : entry.characterPrefab;
            if (prefab == null)
                continue;

            Vector3 position = GetSpawnPosition(spawnedCount);
            Quaternion rotation = GetSpawnRotation();

            Transform parent = partyParent != null ? partyParent : transform;
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = $"NPC_{entry.jobName}";

            NPCPartyMember member = instance.GetComponent<NPCPartyMember>();
            if (member == null)
                member = instance.AddComponent<NPCPartyMember>();

            member.disablePlayerInputComponents = disablePlayerInputComponents;
            member.Initialize(spawnedCount, entry);
            partyMembers.Add(member);

            if (addSimpleFSMController)
            {
                NPCSimpleFSMController controller = instance.GetComponent<NPCSimpleFSMController>();
                if (controller == null)
                    controller = instance.AddComponent<NPCSimpleFSMController>();

                controller.aiMode = defaultAIMode;
                Vector3 formationOffset = GetFormationOffset(spawnedCount);
                controller.Initialize(member, playerTransform, formationOffset);
            }
            spawnedCount++;
            if (spawnedCount >= 3)
                break;
        }
        onPartySpawned?.Invoke();
    }

    public void ClearExistingPartyMembers()
    {
        for (int i = partyMembers.Count - 1; i >= 0; i--)
        {
            NPCPartyMember member = partyMembers[i];
            if (member != null)
                Destroy(member.gameObject);
        }

        partyMembers.Clear();
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null)
            return;

        if (playerSpawner == null)
            playerSpawner = FindAnyObjectByType<RaidSelectedCharacterSpawner>();

        if (playerSpawner != null && playerSpawner.SpawnedPlayer != null)
            playerTransform = playerSpawner.SpawnedPlayer.transform;
    }

    private Vector3 GetFormationOffset(int index)
    {
        if (fallbackFormationOffsets != null && index >= 0 && index < fallbackFormationOffsets.Length)
            return fallbackFormationOffsets[index];

        return new Vector3((index - 1) * 1.4f, 0f, -1.6f);
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints != null && index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null)
            return spawnPoints[index].position;

        Vector3 basePosition = playerTransform != null ? playerTransform.position : transform.position;
        Quaternion baseRotation = playerTransform != null ? playerTransform.rotation : transform.rotation;
        return basePosition + baseRotation * GetFormationOffset(index);
    }

    private Quaternion GetSpawnRotation()
    {
        if (playerTransform != null)
            return playerTransform.rotation;

        return transform.rotation;
    }
}
