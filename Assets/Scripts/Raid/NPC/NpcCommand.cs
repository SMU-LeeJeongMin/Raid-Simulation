using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티 수준 지시의 종류 (8-B Coordinator의 행동 어휘).
/// 어휘를 5종으로 제한하여 규칙 조율자의 거동 해석을 명확히 하고
/// 2단계 RL 조율자의 행동 공간(NPC 3명 x 5종)을 작게 유지
/// </summary>
public enum NpcCommandType
{
    Free = 0,           // 무개입 (조율 없는 조건과 동일한 개별 판단)
    FocusBoss = 1,      // 보스 화력 집중
    HandleSlimes = 2,   // 슬라임 전담
    ProtectAlly = 3,    // 지정 아군 보호 (힐과 실드 우선)
    HoldDefensive = 4   // 수비 대기 (교전 억제, 생존 우선)
}

/// <summary>NPC 1명에게 발행된 지시</summary>
public struct NpcCommand
{
    public NpcCommandType type;
    public PlayerStatus targetAlly;   // ProtectAlly의 보호 대상 (그 외 지시는 null)
    public float issuedTime;
}

/// <summary>
/// 지시 게시판 (blackboard). 발행자는 RaidCoordinator뿐이고 NPC 정책은 읽기 전용.
/// 지시는 강제가 아니라 편향(NpcCommandBias)으로만 반영되며,
/// 미발행 상태의 기본값은 Free(무개입)라서 조율자가 없는 조건과 거동이 동일
/// </summary>
public static class CommandBoard
{
    private static readonly Dictionary<NPCSimpleFSMController, NpcCommand> commands
        = new Dictionary<NPCSimpleFSMController, NpcCommand>();

    public static void Issue(NPCSimpleFSMController npc, NpcCommandType type, PlayerStatus targetAlly = null)
    {
        if (npc == null)
            return;

        commands[npc] = new NpcCommand { type = type, targetAlly = targetAlly, issuedTime = Time.time };
    }

    public static NpcCommand Get(NPCSimpleFSMController npc)
    {
        if (npc != null && commands.TryGetValue(npc, out NpcCommand command))
            return command;

        return new NpcCommand { type = NpcCommandType.Free };
    }

    // 그래프 스냅샷 기록용 지시 코드
    public static int CommandId(NPCSimpleFSMController npc)
    {
        return (int)Get(npc).type;
    }

    // ProtectAlly 지시의 유효한 보호 대상 (없으면 null, 호출 측이 기본 규칙으로 폴백)
    public static PlayerStatus GetProtectTarget(NPCSimpleFSMController npc)
    {
        NpcCommand command = Get(npc);
        if (command.type != NpcCommandType.ProtectAlly)
            return null;

        PlayerStatus target = command.targetAlly;
        if (target == null || target.Health == null || target.Health.IsDead)
            return null;

        return target;
    }

    public static void ClearAll()
    {
        commands.Clear();
    }

    // 씬 재로드(에피소드 반복) 시 파괴된 NPC 키 제거
    public static void CleanupDestroyed()
    {
        List<NPCSimpleFSMController> dead = null;
        foreach (KeyValuePair<NPCSimpleFSMController, NpcCommand> pair in commands)
        {
            if (pair.Key == null)
                (dead ??= new List<NPCSimpleFSMController>()).Add(pair.Key);
        }

        if (dead != null)
            for (int i = 0; i < dead.Count; i++)
                commands.Remove(dead[i]);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        commands.Clear();
    }
}
