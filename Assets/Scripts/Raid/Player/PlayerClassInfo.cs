// 플레이어 직업 식별

using UnityEngine;

public class PlayerClassInfo : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("CharacterLineupDatabase의 characterId와 같은 값을 넣으세요. 예: warrior, archer, mage, healer")]
    public string characterId = "warrior";

    [Tooltip("Inspector 확인용 이름입니다. 예: Warrior, Archer, Mage, Healer")]
    public string jobName = "Warrior";

    [Tooltip("선택 사항입니다. 예: Tank, Mechanic DPS, Magic DPS, Support")]
    public string roleName = "Tank";
}
