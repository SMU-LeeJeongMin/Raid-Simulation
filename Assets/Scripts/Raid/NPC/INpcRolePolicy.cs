/// <summary>
/// NPC 역할별 의사결정 정책 인터페이스.
/// BasicFSM / PatternAwareFSM 외에 Utility, GAT 등 새 정책을 추가할 때
/// 이 인터페이스 구현 클래스를 추가하는 방식으로 확장.
/// </summary>
public interface INpcRolePolicy
{
    // 이번 think 틱의 행동을 결정하고, 지표에 보고할 액션 이름을 반환 (null이면 보고 생략)
    string Think(NPCSimpleFSMController npc);
}
