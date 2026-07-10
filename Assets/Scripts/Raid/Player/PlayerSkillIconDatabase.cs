// 직업별 스킬 버튼 아이콘 DB
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DB_PlayerSkillIcons", menuName = "DungeonSim/Player Skill Icon Database")]
public class PlayerSkillIconDatabase : ScriptableObject
{
    public PlayerSkillIconSet[] iconSets = Array.Empty<PlayerSkillIconSet>();

    public Sprite GetIcon(string characterId, PlayerSkillSlot slot)
    {
        PlayerSkillIconSet set = GetSet(characterId);
        if (set == null)
            return null;

        switch (slot)
        {
            case PlayerSkillSlot.Skill1:
                return set.skill1Icon;
            case PlayerSkillSlot.Skill2:
                return set.skill2Icon;
            case PlayerSkillSlot.Ultimate:
                return set.ultimateIcon;
            default:
                return null;
        }
    }

    public PlayerSkillIconSet GetSet(string characterId)
    {
        if (iconSets == null)
            return null;

        for (int i = 0; i < iconSets.Length; i++)
        {
            PlayerSkillIconSet set = iconSets[i];
            if (set == null)
                continue;

            if (string.Equals(set.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return set;
        }

        return null;
    }

    [ContextMenu("Fill Default Icon Entries")]
    public void FillDefaultIconEntries()
    {
        iconSets = new[]
        {
            new PlayerSkillIconSet { characterId = "warrior", displayName = "Warrior" },
            new PlayerSkillIconSet { characterId = "archer", displayName = "Archer" },
            new PlayerSkillIconSet { characterId = "mage", displayName = "Mage" },
            new PlayerSkillIconSet { characterId = "healer", displayName = "Healer" }
        };
    }

    private void Reset()
    {
        FillDefaultIconEntries();
    }
}

[Serializable]
public class PlayerSkillIconSet
{
    [Header("Class")]
    public string characterId = "warrior";
    public string displayName = "Warrior";

    [Header("Icons")]
    public Sprite skill1Icon;
    public Sprite skill2Icon;
    public Sprite ultimateIcon;
}
