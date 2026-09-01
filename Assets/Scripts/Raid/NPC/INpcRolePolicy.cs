/// <summary>
/// NPC 역할별 의사결정 정책 인터페이스.
/// BasicFSM / PatternAwareFSM 외에 Utility, GCN, GAT 등 새 정책을 추가할 때
/// 이 인터페이스 구현 클래스를 추가하는 방식으로 확장.
/// 모든 정책은 NpcActionMask가 허용하는 행동 중에서만 선택해야 함.
/// </summary>
public interface INpcRolePolicy
{
    // 이번 think 틱의 행동을 결정하여 반환 (None이면 보고 생략)
    NpcAction Think(NPCSimpleFSMController npc);
}
