// 플레이어 직업 식별 + UI 초상화 정보

using UnityEngine;

public class PlayerClassInfo : MonoBehaviour
{
    [Header("Identity")]
    public string characterId = "warrior";
    public string jobName = "Warrior";
    public string roleName = "Tank";

    [Header("UI")]
    public Sprite portraitSprite;
}
