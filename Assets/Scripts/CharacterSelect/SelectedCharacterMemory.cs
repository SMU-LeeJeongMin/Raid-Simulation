using UnityEngine;

public static class SelectedCharacterMemory
{
    private const string SelectedIndexKey = "DungeonSim.SelectedCharacter.Index";
    private const string SelectedIdKey = "DungeonSim.SelectedCharacter.Id";
    private const string SelectedNameKey = "DungeonSim.SelectedCharacter.Name";

    public static void Save(int index, string characterId, string displayName)
    {
        PlayerPrefs.SetInt(SelectedIndexKey, index);
        PlayerPrefs.SetString(SelectedIdKey, characterId ?? string.Empty);
        PlayerPrefs.SetString(SelectedNameKey, displayName ?? string.Empty);
        PlayerPrefs.Save();
    }

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
