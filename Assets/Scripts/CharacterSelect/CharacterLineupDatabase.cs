using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DB_CharacterLineup", menuName = "DungeonSim/Character Lineup Database")]
public class CharacterLineupDatabase : ScriptableObject
{
    public CharacterLineupEntry[] characters = Array.Empty<CharacterLineupEntry>();

    public int Count => characters == null ? 0 : characters.Length;

    public bool IsValidIndex(int index)
    {
        return characters != null && index >= 0 && index < characters.Length && characters[index] != null;
    }

    public CharacterLineupEntry Get(int index)
    {
        if (!IsValidIndex(index))
            return null;

        return characters[index];
    }
}

[Serializable]
public class CharacterLineupEntry
{
    [Header("Basic Info")]
    public string characterId = "warrior";
    public string jobName = "전사";
    public string roleName = "탱커";

    [TextArea(4, 10)]
    public string description = "보스의 공격을 받아내고 파티를 보호하는 직업입니다.";

    [Header("Prefabs")]
    public GameObject characterPrefab;
    public GameObject raidPrefab;

    [Header("Preview Animation - Recommended")]
    [Tooltip("켜두면 Animator Controller의 자동 Transition을 무시하고 아래 AnimationClip을 직접 재생합니다. 캐릭터 선택 화면에서는 이 방식을 권장합니다.")]
    public bool useDirectClipPlayback = true;

    [Tooltip("처음 4명이 서 있을 때 재생할 Rest AnimationClip입니다.")]
    public AnimationClip restClip;

    [Tooltip("캐릭터를 선택했을 때 재생할 Idle/Selected AnimationClip입니다. 예: IdleA")]
    public AnimationClip selectedClip;

    [Tooltip("선택 화면에서는 캐릭터가 걸어가거나 흔들리지 않도록 Root Motion을 끄는 것을 권장합니다.")]
    public bool disableRootMotionForPreview = true;

    [Tooltip("AnimationClip이 Loop 설정되어 있지 않아도 선택 화면에서 반복 재생합니다.")]
    public bool loopPreviewClips = true;

    [Tooltip("애니메이션 Root Motion이나 외부 스크립트 때문에 캐릭터 루트 Transform이 흔들릴 때 원래 위치/회전을 유지합니다.")]
    public bool lockLineupTransform = true;

    [Header("Preview Animation - State Fallback")]
    [Tooltip("Direct Clip Playback을 끄거나 AnimationClip이 비어 있을 때 사용할 Animator State 이름입니다.")]
    public string restStateName = "Rest";

    [Tooltip("Direct Clip Playback을 끄거나 AnimationClip이 비어 있을 때 사용할 선택 Animator State 이름입니다. 예: IdleA")]
    public string selectedStateName = "IdleA";

    [Header("Lineup Transform Adjustment")]
    public Vector3 lineupPositionOffset = Vector3.zero;
    public Vector3 lineupEulerOffset = Vector3.zero;
    public Vector3 lineupScale = Vector3.one;

    [Header("Close-up Camera Settings")]
    public Vector3 closeupCameraWorldOffset = new Vector3(0f, 1.15f, -2.8f);
    public Vector3 closeupLookAtWorldOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Click Collider Auto Setup")]
    public bool addClickColliderIfMissing = true;
    public Vector3 clickColliderCenter = new Vector3(0f, 0.9f, 0f);
    public float clickColliderHeight = 1.8f;
    public float clickColliderRadius = 0.35f;
}
