// 캐릭터 선택 씬에서 고른 캐릭터 정보를 저장/불러오는 클래스

using UnityEngine;

public static class SelectedCharacterMemory
{
    private const string SelectedIndexKey = "DungeonSim.SelectedCharacter.Index";
    private const string SelectedIdKey = "DungeonSim.SelectedCharacter.Id";
    private const string SelectedNameKey = "DungeonSim.SelectedCharacter.Name";

    // 선택한 캐릭터의 index, id, 이름을 저장
    // Select 버튼을 눌렀을 때 호출
    public static void Save(int index, string characterId, string displayName)
    {
        PlayerPrefs.SetInt(SelectedIndexKey, index);
        PlayerPrefs.SetString(SelectedIdKey, characterId ?? string.Empty);
        PlayerPrefs.SetString(SelectedNameKey, displayName ?? string.Empty);
        PlayerPrefs.Save();
    }

    // Raid 씬에서 이전에 저장된 캐릭터 index를 읽기
    public static int LoadSelectedIndex(int fallback = 0)
    {
        return PlayerPrefs.GetInt(SelectedIndexKey, fallback);
    }

    public static string LoadSelectedId(string fallback = "")
    {
        return PlayerPrefs.GetString(SelectedIdKey, fallback);
    }

    public static string LoadSelectedName(string fallback = "")
    {
        return PlayerPrefs.GetString(SelectedNameKey, fallback);
    }

    public static bool HasSavedSelection()
    {
        return PlayerPrefs.HasKey(SelectedIndexKey);
    }
}
