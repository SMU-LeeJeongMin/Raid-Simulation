using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효용 기반 의사결정 정책 (Utility AI, 6단계).
/// 공용 행동 Mask(NpcActionMask)가 허용한 후보 각각에 효용 점수를 계산하고
/// 최고점 행동부터 실행을 시도. FSM의 고정 분기 순서와 달리 상황의 정도
/// (아군 HP 비율, 슬라임 폭발 임박도, 보스 거리)에 따라 우선순위가 연속적으로 바뀜.
///
/// 설계 근거 (Infinite Axis Utility System 계열):
/// - Mark (2009) Behavioral Mathematics for Game AI
/// - Dill, Mark (2010) Improving AI Decision Modeling Through Utility Theory, GDC
/// - Graham (2013) An Introduction to Utility Theory, Game AI Pro
/// 점수 = 반응 곡선(정규화된 고려 요소) x 역할 가중치, 최종 [0, 1] 범위
/// </summary>
public class NpcUtilityPolicy : INpcRolePolicy
{
    private readonly bool tank;
    private readonly bool healer;

    private readonly List<NpcAction> candidates = new List<NpcAction>(12);
    private readonly List<ScoredAction> scored = new List<ScoredAction>(12);

    public NpcUtilityPolicy(bool tank, bool healer)
    {
        this.tank = tank;
        this.healer = healer;
    }

    private struct ScoredAction
    {
        public NpcAction action;
        public float score;
    }

    // 한 틱에 1회 계산하여 모든 점수 함수가 공유하는 상황 요약
    private struct Context
    {
        public bool hasBoss;
        public bool bossFlying;
        public bool bossAttackable;
        public Vector3 bossCenter;
        public Vector3 bossAimPoint;
        public Vector3 facePoint;
        public float bossDistance;
        public float desiredDistance;

        public bool canRetreat;
        public float manaRatio;
        public bool saveManaForUltimate;

        public PlayerStatus lowestAlly;
        public float lowestAllyRatio;
        public PlayerStatus dangerAlly;
        public float dangerAllyRatio;

        public SlimeAddEnemy urgentSlime;
        public float slimeUrgency;   // 0(여유) ~ 1(폭발 임박)
        public float slimeDistance;
    }

    // 역할 가중치: 탱커는 보스 유지 성향, 딜러는 슬라임 전환에 적극적, 힐러는 전투 참여 소극적
    private float SlimeWeight => tank ? 0.6f : healer ? 0.5f : 1f;
    private float BossWeight => tank ? 1.1f : 1f;

    public NpcAction Think(NPCSimpleFSMController npc)
    {
        int count = NpcActionMask.CollectAvailable(npc, includeTacticalChecks: true, candidates);
        if (count == 0)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("Utility FollowPlayer (no candidate)");
            return NpcAction.FollowPlayer;
        }

        Context context = BuildContext(npc);

        // 후보 점수화 후 내림차순 정렬
        scored.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            float score = Score(npc, candidates[i], context);
            if (score <= 0f)
                continue;

            scored.Add(new ScoredAction { action = candidates[i], score = score });
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        // 최고점부터 실행 시도. 실행이 성립하지 않으면(대상 소멸, 쿨타임 등) 차순위로 폴백
        for (int i = 0; i < scored.Count; i++)
        {
            if (TryExecute(npc, scored[i].action, context))
            {
                npc.SetStateLabel($"Utility {scored[i].action} ({scored[i].score:0.00})");
                return scored[i].action;
            }
        }

        npc.FollowPlayerFormation();
        npc.SetStateLabel("Utility FollowPlayer (fallback)");
        return NpcAction.FollowPlayer;
    }

    // ---------- 상황 요약 ----------

    private Context BuildContext(NPCSimpleFSMController npc)
    {
        Context context = new Context();

        context.manaRatio = npc.status != null && npc.status.Mana != null ? npc.status.Mana.Normalized : 1f;

        // 궁극기 마나 아끼기: 게이지가 거의 찼는데 마나가 궁극기 비용에 못 미치면
        // 일반 스킬 사용을 억제하여 마나를 모음 (마나 운용 패턴의 교사 시연)
        context.saveManaForUltimate = false;
        if (npc.skillController != null && npc.status != null && npc.status.Mana != null)
        {
            PlayerSkillDefinition ultimate = npc.skillController.GetSkillDefinitionForUI(PlayerSkillSlot.Ultimate);
            if (ultimate != null
                && npc.skillController.GetUltimateGaugeNormalized() >= 0.85f
                && npc.status.Mana.CurrentMana < ultimate.manaCost)
            {
                context.saveManaForUltimate = true;
            }
        }
        context.hasBoss = npc.boss != null;
        if (context.hasBoss)
        {
            context.bossFlying = npc.IsBossFlyingAndUntargetable();
            // 거리와 무관한 전술 판단 (슬라임 상대 가치 계산과 공격 후보 판정에 사용)
            context.bossAttackable = NpcActionMask.IsBossTacticallyAttackable(npc);
            context.bossCenter = npc.boss.transform.position;
            context.bossAimPoint = npc.GetBossAimPoint();
            context.bossDistance = NPCSimpleFSMController.FlatDistance(npc.transform.position, context.bossAimPoint);
            context.facePoint = context.bossDistance > 0.1f ? context.bossAimPoint : context.bossCenter;
            context.desiredDistance = tank ? npc.meleeDistance : npc.rangedDistance;

            // 후퇴 필요 상황에서만 실행 가능성 검사 (맵 끝에 몰리면 근접 교전으로 전환)
            context.canRetreat = true;
            if (!tank && context.bossDistance < npc.keepDistanceFromBoss)
            {
                Vector3 retreatFrom = context.bossDistance > 0.1f ? context.bossAimPoint : context.bossCenter;
                context.canRetreat = npc.CanRetreatFrom(retreatFrom, npc.keepDistanceFromBoss + Mathf.Max(0f, npc.keepDistanceMargin));
            }
        }

        context.lowestAlly = NpcHealerPolicy.FindLowestHpAlly(out context.lowestAllyRatio);

        if (healer && npc.healerProtectDangerAlly)
        {
            context.dangerAlly = NpcHealerPolicy.FindMostThreatenedAlly(npc);
            context.dangerAllyRatio = NpcHealerPolicy.GetHealthRatio(context.dangerAlly);
        }

        context.urgentSlime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
        if (context.urgentSlime != null)
        {
            // 폭발 임박도: 남은 시간 10초를 여유의 상한으로 정규화
            float remaining = Mathf.Max(0f, context.urgentSlime.ExplosionTimeRemaining);
            context.slimeUrgency = 1f - Mathf.Clamp01(remaining / 10f);
            context.slimeDistance = NPCSimpleFSMController.FlatDistance(npc.transform.position, context.urgentSlime.transform.position);
        }

        return context;
    }

    // ---------- 효용 점수 ----------

    private float Score(NPCSimpleFSMController npc, NpcAction action, Context context)
    {
        switch (action)
        {
            // 생존 최우선 (Mask가 위험 지역 안일 때만 후보에 포함)
            case NpcAction.MoveToSafePosition:
                return 0.95f;

            // 추적 낙뢰 산개 (간격 확보 실패 시 실행 단계에서 차순위로 폴백)
            case NpcAction.SpreadFromParty:
                return 0.9f;

            // 위험 지역 안 아군 보호 (힐러 전용)
            case NpcAction.ShieldDangerAlly:
                if (!healer || context.dangerAlly == null)
                    return 0f;
                return 0.6f + 0.25f * (1f - context.dangerAllyRatio);

            // 치유: 선형 결핍 + 위험 임계 부스트. HP 50% 아군이면 0.25 + 0.35 = 0.6으로 보스 공격(0.5)을 앞섬
            case NpcAction.MoveToAllyAndHeal:
            {
                if (!healer || context.lowestAlly == null)
                    return 0f;

                float need = 1f - context.lowestAllyRatio;
                if (need <= 0.05f)
                    return 0f;

                float boost = context.lowestAllyRatio <= npc.healerLowHpThreshold ? 0.35f : 0f;
                return Mathf.Min(0.85f, 0.5f * need + boost);
            }

            // 예방 실드 (힐러 전용, 치유보다 낮은 기본 순위)
            case NpcAction.ShieldParty:
                if (!healer || context.lowestAlly == null || context.lowestAllyRatio > npc.healerShieldThreshold)
                    return 0f;
                return 0.3f + 0.25f * (1f - context.lowestAllyRatio);

            // 보스 공격 슬롯 선택: 궁극기는 준비되면 최우선, 일반 스킬은 마나 여유가 있을 때만
            // 기본 공격보다 높은 점수를 가짐 (마나가 부족하면 기본 공격으로 자원 보존)
            case NpcAction.AttackBossUltimate:
                if (!context.bossAttackable || !IsInAttackWindow(npc, context))
                    return 0f;
                return 0.62f * BossWeight;

            case NpcAction.AttackBossSkill1:
            case NpcAction.AttackBossSkill2:
            {
                if (!context.bossAttackable || !IsInAttackWindow(npc, context))
                    return 0f;

                float skillScore = 0.6f * BossWeight * ManaFactor(context.manaRatio);

                // 궁극기 준비 임박 시 일반 스킬을 기본 공격 아래로 낮춰 마나 보존
                return context.saveManaForUltimate ? skillScore * 0.6f : skillScore;
            }

            case NpcAction.AttackBossBasic:
                if (!context.bossAttackable || !IsInAttackWindow(npc, context))
                    return 0f;
                return 0.5f * BossWeight;

            case NpcAction.MoveToBoss:
                if (!context.hasBoss || context.bossFlying)
                    return 0f;
                return context.bossDistance > context.desiredDistance ? 0.45f * BossWeight : 0f;

            case NpcAction.KeepDistance:
                if (!context.hasBoss || context.bossFlying || tank || !context.canRetreat)
                    return 0f;
                return context.bossDistance < npc.keepDistanceFromBoss ? 0.55f : 0f;

            // 슬라임 처리: 임박도에 비례하고, 보스 공격 가능 여부에 따라 상대 가치가 바뀜.
            // 보스가 공격 가능해도 폭발 임박 슬라임이 있으면 점수가 보스를 넘을 수 있음 (FSM과의 차별점)
            case NpcAction.AttackSlime:
                if (context.urgentSlime == null || context.slimeDistance > context.desiredDistance)
                    return 0f;
                return SlimeScore(context);

            case NpcAction.MoveToSlime:
                if (context.urgentSlime == null || context.slimeDistance <= context.desiredDistance)
                    return 0f;
                return SlimeScore(context);

            // 보스 비행 중 재집결
            case NpcAction.RegroupDuringBossFly:
                return 0.4f;

            // 기본 대기 행동
            case NpcAction.FollowPlayer:
                return 0.08f;

            default:
                return 0f;
        }
    }

    // 마나 여유도 반응 곡선: 20% 이하면 0, 60% 이상이면 1
    // 스킬 점수(0.6)가 기본 공격(0.5)을 넘으려면 마나가 약 53% 이상이어야 함.
    // 이전 임계(약 65%)는 메이지의 전투 중 마나 평형(약 45%)보다 높아
    // 개전 직후 외에는 스킬이 거의 선택되지 않는 문제의 원인이었으므로 완화.
    // 마나가 마르면 자동으로 기본 공격으로 전환되어 자원을 회복
    private static float ManaFactor(float manaRatio)
    {
        return Mathf.Clamp01((manaRatio - 0.2f) / 0.4f);
    }

    private float SlimeScore(Context context)
    {
        float baseScore = 0.35f + 0.45f * context.slimeUrgency;
        float bossFactor = context.bossAttackable ? 0.85f : 1.25f;
        return Mathf.Min(0.9f, SlimeWeight * baseScore * bossFactor);
    }

    private bool IsInAttackWindow(NPCSimpleFSMController npc, Context context)
    {
        if (context.bossDistance > context.desiredDistance)
            return false;

        // 너무 가까워도 물러날 곳이 없으면(맵 끝 몰림) 근접 교전으로 인정
        if (!tank && context.bossDistance < npc.keepDistanceFromBoss && context.canRetreat)
            return false;

        return true;
    }

    // ---------- 실행 ----------

    // 실행은 공용 실행기 사용 (GNN 정책과 동일한 실행 계층, 비교 공정성 조건)
    private bool TryExecute(NPCSimpleFSMController npc, NpcAction action, Context context)
    {
        return NpcActionExecutor.TryExecute(npc, action, tank);
    }
}
