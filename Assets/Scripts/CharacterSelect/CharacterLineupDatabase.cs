// 캐릭터 선택 화면에 필요한 4개 직업 정보를 담는 ScriptableObject
// 프리팹, 설명 텍스트, 미리보기 애니메이션, 카메라 보정값 등을 Inspector에서 관리

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DB_CharacterLineup", menuName = "DungeonSim/Character Lineup Database")]
public class CharacterLineupDatabase : ScriptableObject
{
    public CharacterLineupEntry[] characters = Array.Empty<CharacterLineupEntry>();

    public int Count => characters == null ? 0 : characters.Length;

    // 요청한 index가 characters 배열 안에서 유효한지 확인
    public bool IsValidIndex(int index)
    {
        return characters != null && index >= 0 && index < characters.Length && characters[index] != null;
    }

    // index에 해당하는 캐릭터 정보를 반환
    public CharacterLineupEntry Get(int index)
    {
        if (!IsValidIndex(index))
            return null;

        return characters[index];
    }
}

// 캐릭터 하나의 선택 화면용 데이터
// 직업명, 설명, 프리팹, 애니메이션 클립, 카메라/클릭 보정값
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
    public bool useDirectClipPlayback = true;
    public AnimationClip restClip;
    public AnimationClip selectedClip;
    public bool disableRootMotionForPreview = true;
    public bool loopPreviewClips = true;
    public bool lockLineupTransform = true;

    [Header("Preview Animation - State Fallback")]
    public string restStateName = "Rest";

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
