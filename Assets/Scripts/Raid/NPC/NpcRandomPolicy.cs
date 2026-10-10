using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무작위 의사결정 정책 (교사 없는 데이터 수집용).
///
/// 목적: 학습 라벨을 기록된 미래에서 유도하는 설계에서는 데이터를 만드는 정책이
/// 무엇이든 무관함. 무작위 정책은 손으로 만든 규칙을 전혀 참조하지 않으므로
/// 파이프라인에서 교사를 완전히 제거하며, 동시에 상태 공간을 가장 넓게 덮음.
///
/// 행동 후보와 실행은 다른 정책과 동일한 Mask와 실행기를 사용하므로
/// 차이는 순수하게 "무엇을 고르는가"에만 존재함.
///
/// 주의: 성능이 낮은 정책이므로 전멸이 잦고 에피소드가 짧아짐. 이는 의도된 것이며
/// 저체력과 사망 라벨의 양성 비율을 높여 학습에 유리함.
/// </summary>
public class NpcRandomPolicy : INpcRolePolicy
{
    private readonly bool tank;
    private readonly List<NpcAction> candidates = new List<NpcAction>(GnnFeatureEncoder.NumActions);

    public NpcRandomPolicy(bool tank, bool healer)
    {
        this.tank = tank;
        // healer 구분은 Mask와 실행기가 스킬 가용성으로 처리하므로 별도 분기 불필요
    }

    public NpcAction Think(NPCSimpleFSMController npc)
    {
        // 공용 Mask(전술 검사 포함)를 통과한 행동만 후보로 수집
        candidates.Clear();
        for (int i = 0; i < NpcActionMask.AllActions.Length; i++)
        {
            NpcAction action = NpcActionMask.AllActions[i];
            if (NpcActionMask.IsAvailable(npc, action, includeTacticalChecks: true))
                candidates.Add(action);
        }

        if (candidates.Count == 0)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("Random FollowPlayer (no candidate)");
            return NpcAction.FollowPlayer;
        }

        // 후보를 무작위 순서로 섞은 뒤 실행 가능한 첫 행동을 채택 (Fisher-Yates)
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (NpcActionExecutor.TryExecute(npc, candidates[i], tank))
            {
                npc.SetStateLabel($"Random {candidates[i]}");
                return candidates[i];
            }
        }

        npc.FollowPlayerFormation();
        npc.SetStateLabel("Random FollowPlayer (fallback)");
        return NpcAction.FollowPlayer;
    }
}
