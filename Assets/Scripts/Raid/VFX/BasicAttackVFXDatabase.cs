// 직업별 기본공격 명중 VFX 저장 ScriptableObject

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DB_BasicAttackVFX", menuName = "DungeonSim/Basic Attack VFX Database")]
public class BasicAttackVFXDatabase : ScriptableObject
{
    public BasicAttackVFXEntry[] entries = Array.Empty<BasicAttackVFXEntry>();

    public BasicAttackVFXEntry GetEntry(string characterId)
    {
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            BasicAttackVFXEntry entry = entries[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    [ContextMenu("Fill Default Hit VFX Profiles")]
    public void FillDefaultHitVFXProfiles()
    {
        entries = new[]
        {
            CreateDefault("warrior", "Warrior"),
            CreateDefault("archer", "Archer"),
            CreateDefault("mage", "Mage"),
            CreateDefault("healer", "Healer")
        };
    }

    private BasicAttackVFXEntry CreateDefault(string characterId, string displayName)
    {
        return new BasicAttackVFXEntry
        {
            characterId = characterId,
            displayName = displayName,
            spawnHitVFX = true,
            hitAnchorName = "VFXHitAnchor",
            hitVFXOffset = Vector3.zero,
            hitVFXEuler = Vector3.zero,
            hitVFXScale = Vector3.one,
            attachHitVFXToTarget = false,
            autoDestroyDelay = 3f
        };
    }

    private void Reset()
    {
        FillDefaultHitVFXProfiles();
    }
}

[Serializable]
public class BasicAttackVFXEntry
{
    [Header("Class")]
    public string characterId = "warrior";
    public string displayName = "Warrior";

    [Header("Hit VFX")]
    public GameObject hitVFXPrefab;
    public bool spawnHitVFX = true;
    public string hitAnchorName = "VFXHitAnchor";
    public Vector3 hitVFXOffset = Vector3.zero;
    public Vector3 hitVFXEuler = Vector3.zero;
    public Vector3 hitVFXScale = Vector3.one;
    public bool attachHitVFXToTarget = false;

    [Header("Lifetime")]
    [Min(0.05f)] public float autoDestroyDelay = 3f;
}
