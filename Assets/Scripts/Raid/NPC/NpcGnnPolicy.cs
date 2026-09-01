using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GNN 추론 기반 의사결정 정책 (8-A, 독립 모드).
/// GnnModelRunner가 추론한 자기 노드의 행동 logits에 공용 행동 Mask(전술 검사 포함)를
/// 적용하고, 높은 점수 순으로 공용 실행기(NpcActionExecutor)에 실행을 위임.
/// 행동 후보와 실행이 다른 정책과 동일하므로 차이는 순수하게 판단 모델에만 존재.
///
/// 모델 미준비(모델 미연결, Sentis 미설치 씬 설정 오류) 시 대형 유지로 폴백하고
/// 1회 경고 출력 (기준선처럼 조용히 동작하여 결과가 오염되는 것 방지).
/// </summary>
public class NpcGnnPolicy : INpcRolePolicy
{
    private readonly bool tank;

    private readonly float[] logits = new float[GnnFeatureEncoder.NumActions];
    private readonly List<ScoredAction> ranked = new List<ScoredAction>(GnnFeatureEncoder.NumActions);
    private bool warnedNotReady;

    private struct ScoredAction
    {
        public NpcAction action;
        public float logit;
    }

    public NpcGnnPolicy(bool tank, bool healer)
    {
        this.tank = tank;
        // healer 구분은 실행기와 Mask가 스킬 가용성으로 처리하므로 별도 분기 불필요
    }

    public NpcAction Think(NPCSimpleFSMController npc)
    {
        GnnModelRunner runner = GnnModelRunner.Instance;

        if (runner == null || !runner.IsReady || npc.status == null
            || !runner.TryGetLogits(npc.status, npc.aiMode, logits))
        {
            if (!warnedNotReady)
            {
                warnedNotReady = true;
                Debug.LogWarning($"[NpcGnnPolicy] GNN 추론 불가 ({npc.aiMode}). GnnModelRunner 배치와 해당 모드의 모델 슬롯 등록 확인 필요. 대형 유지로 폴백", npc);
            }

            npc.FollowPlayerFormation();
            npc.SetStateLabel("GNN NotReady: Follow Player");
            return NpcAction.FollowPlayer;
        }

        // 공용 Mask(전술 검사 포함)를 통과한 행동만 후보로 수집
        ranked.Clear();
        for (int i = 0; i < NpcActionMask.AllActions.Length; i++)
        {
            NpcAction action = NpcActionMask.AllActions[i];
            if (!NpcActionMask.IsAvailable(npc, action, includeTacticalChecks: true))
                continue;

            ranked.Add(new ScoredAction { action = action, logit = logits[(int)action] });
        }

        if (ranked.Count == 0)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("GNN FollowPlayer (no candidate)");
            return NpcAction.FollowPlayer;
        }

        ranked.Sort((a, b) => b.logit.CompareTo(a.logit));

        // 최고 logit부터 실행 시도. 실행이 성립하지 않으면(후퇴 불가 등) 차순위로 폴백
        for (int i = 0; i < ranked.Count; i++)
        {
            if (NpcActionExecutor.TryExecute(npc, ranked[i].action, tank))
            {
                npc.SetStateLabel($"{npc.aiMode} {ranked[i].action} ({ranked[i].logit:0.00})");
                return ranked[i].action;
            }
        }

        npc.FollowPlayerFormation();
        npc.SetStateLabel("GNN FollowPlayer (fallback)");
        return NpcAction.FollowPlayer;
    }
}
