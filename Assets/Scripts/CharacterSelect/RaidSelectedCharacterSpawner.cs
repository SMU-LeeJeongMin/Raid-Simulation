// Raid 씬에서 이전 선택 화면에서 고른 캐릭터를 실제 플레이어로 생성

using UnityEngine;

public class RaidSelectedCharacterSpawner : MonoBehaviour
{
    public CharacterLineupDatabase database;
    public Transform spawnPoint;
    public bool useFallbackFirstCharacter = true;

    private GameObject spawnedPlayer;

    private void Start()
    {
        SpawnSelectedCharacter();
    }

    // 저장된 선택 index를 읽고 해당 캐릭터 프리팹을 생성
    public GameObject SpawnSelectedCharacter()
    {
        if (database == null)
        {
            Debug.LogError("[RaidSelectedCharacterSpawner] Database is not assigned.");
            return null;
        }

        int selectedIndex = SelectedCharacterMemory.LoadSelectedIndex(0);
        if (!database.IsValidIndex(selectedIndex))
        {
            if (!useFallbackFirstCharacter || !database.IsValidIndex(0))
            {
                Debug.LogError($"[RaidSelectedCharacterSpawner] Invalid selected index: {selectedIndex}");
                return null;
            }

            selectedIndex = 0;
        }

        CharacterLineupEntry entry = database.Get(selectedIndex);
        GameObject prefab = entry.raidPrefab != null ? entry.raidPrefab : entry.characterPrefab;

        if (prefab == null)
        {
            Debug.LogError($"[RaidSelectedCharacterSpawner] Missing prefab for: {entry.jobName}");
            return null;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        spawnedPlayer = Instantiate(prefab, position, rotation);
        spawnedPlayer.name = $"Player_{entry.jobName}";
        return spawnedPlayer;
    }
}
