using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 전투 상태를 GNN_SCHEMA_V1 그래프 스냅샷으로 변환하는 단일 구현.
/// GraphStateLogger(JSONL 기록)와 이후 Coordinator(실시간 상태 조회)가 같은 빌더를 사용.
///
/// v1 수집 범위: CHARACTER, BOSS, SLIME, DANGER_ZONE, OBJECTIVE 노드와
/// SELF_LOOP, TARGETS, THREATENS, CAN_ATTACK, CAN_SUPPORT, AFFECTS,
/// NEEDS_SUPPORT, REQUIRES, PARTY_RELATION 관계.
/// SKILL 노드와 OWNS_SKILL, CAN_APPLY_EFFECT 관계는 스키마에 번호만 예약 (v2).
/// </summary>
public static class GraphSnapshotBuilder
{
    // Objective의 remaining_count_ratio 계산용: 이번 Add Phase에서 관측된 최대 동시 슬라임 수
    private static int maxAliveSlimesInPhase;

    // 노드로 표현하는 스킬 슬롯 (기본 공격은 자원과 쿨타임 개념이 없어 제외)
    private static readonly PlayerSkillSlot[] SkillSlots =
    {
        PlayerSkillSlot.Skill1,
        PlayerSkillSlot.Skill2,
        PlayerSkillSlot.Ultimate
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        maxAliveSlimesInPhase = 0;
    }

    public static GraphSnapshot Build(string episodeId, string algorithm, int tick, float episodeTime)
    {
        return Build(episodeId, algorithm, tick, episodeTime, null, 0);
    }

    // characterIndexMap: 캐릭터 → 노드 인덱스 매핑 출력 (GNN 추론에서 자기 노드의 logits 행을 찾는 용도)
    // maxNodes: 노드 수 상한 (0이면 무제한). 상한 초과 시 의사결정 중요도 순으로 선별.
    //   캐릭터와 보스, Objective는 항상 포함하고, 슬라임은 폭발 임박 순,
    //   위험 지역은 파티와 가까운 순으로 남김 (장판이 많은 위험 상황에서 회피 판단 근거를 보존)
    public static GraphSnapshot Build(string episodeId, string algorithm, int tick, float episodeTime,
        System.Collections.Generic.Dictionary<PlayerStatus, int> characterIndexMap, int maxNodes)
    {
        characterIndexMap?.Clear();

        GraphSnapshot snapshot = new GraphSnapshot
        {
            episode_id = episodeId,
            algorithm = algorithm,
            tick = tick,
            t = episodeTime
        };

        // ---------- 노드 수집 ----------

        var characterStatuses = new List<PlayerStatus>();
        var characterControllers = new List<NPCSimpleFSMController>();
        var characterIndices = new List<int>();

        var statuses = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null)
                continue;

            NPCSimpleFSMController controller = status.GetComponent<NPCSimpleFSMController>();
            int index = snapshot.nodes.Count;
            snapshot.nodes.Add(BuildCharacterNode(status, controller, index));

            characterStatuses.Add(status);
            characterControllers.Add(controller);
            characterIndices.Add(index);

            if (characterIndexMap != null)
                characterIndexMap[status] = index;
        }

        BossDummyController boss = CombatRegistry.FirstBossDummy;
        BossSkillPatternController bossPattern = CombatRegistry.FirstBossSkillController;
        Health bossHealth = boss != null ? (boss.health != null ? boss.health : boss.GetComponent<Health>()) : null;
        int bossIndex = -1;
        if (boss != null)
        {
            bossIndex = snapshot.nodes.Count;
            snapshot.nodes.Add(BuildBossNode(boss, bossHealth, bossPattern, bossIndex));
        }

        // SKILL 노드: 캐릭터마다 보유 스킬 3종 (기본 공격은 자원과 쿨타임이 없어 노드로 만들지 않음)
        var skillOwnerSlots = new List<int>();          // characterStatuses 내 소유자 위치
        var skillDefinitions = new List<PlayerSkillDefinition>();
        var skillIndices = new List<int>();

        for (int i = 0; i < characterStatuses.Count; i++)
        {
            PlayerSkillController skills = characterStatuses[i].GetComponent<PlayerSkillController>();
            if (skills == null)
                continue;

            for (int s = 0; s < SkillSlots.Length; s++)
            {
                PlayerSkillSlot slot = SkillSlots[s];
                PlayerSkillDefinition definition = skills.GetSkillDefinitionForUI(slot);
                if (definition == null)
                    continue;

                int index = snapshot.nodes.Count;
                snapshot.nodes.Add(BuildSkillNode(characterStatuses[i], skills, definition, slot, index));

                skillOwnerSlots.Add(i);
                skillDefinitions.Add(definition);
                skillIndices.Add(index);
            }
        }

        // 후보 수집 후 중요도 정렬 (상한 초과 시 선별 기준)
        var slimeCandidates = new List<SlimeAddEnemy>();
        var slimes = CombatRegistry.Slimes;
        for (int i = 0; i < slimes.Count; i++)
        {
            if (slimes[i] != null && slimes[i].IsAlive)
                slimeCandidates.Add(slimes[i]);
        }

        var zoneCandidates = new List<DangerZoneHandle>();
        var activeZones = DangerZoneRegistry.GetActiveZones();
        for (int i = 0; i < activeZones.Count; i++)
        {
            if (activeZones[i] != null && activeZones[i].isActiveAndEnabled)
                zoneCandidates.Add(activeZones[i]);
        }

        bool bossImmune = bossHealth != null && bossHealth.DamageImmune;
        bool objectiveActive = bossImmune || slimeCandidates.Count > 0;

        // 노드 상한 적용: 캐릭터와 보스, Objective를 확보한 나머지를 슬라임과 위험 지역에 배분
        if (maxNodes > 0)
        {
            int reserved = snapshot.nodes.Count + (objectiveActive ? 1 : 0);
            int budget = Mathf.Max(0, maxNodes - reserved);

            if (slimeCandidates.Count + zoneCandidates.Count > budget)
            {
                // 슬라임은 폭발 임박 순, 위험 지역은 파티와 가까운 순
                slimeCandidates.Sort((a, b) => a.ExplosionTimeRemaining.CompareTo(b.ExplosionTimeRemaining));
                zoneCandidates.Sort((a, b) =>
                    DistanceToNearestCharacter(a, characterStatuses).CompareTo(
                        DistanceToNearestCharacter(b, characterStatuses)));

                // 생존 판단이 우선이므로 위험 지역에 절반 이상을 먼저 배정
                int zoneQuota = Mathf.Min(zoneCandidates.Count, Mathf.CeilToInt(budget * 0.5f));
                int slimeQuota = Mathf.Min(slimeCandidates.Count, budget - zoneQuota);
                zoneQuota = Mathf.Min(zoneCandidates.Count, budget - slimeQuota);

                if (slimeCandidates.Count > slimeQuota)
                    slimeCandidates.RemoveRange(slimeQuota, slimeCandidates.Count - slimeQuota);
                if (zoneCandidates.Count > zoneQuota)
                    zoneCandidates.RemoveRange(zoneQuota, zoneCandidates.Count - zoneQuota);
            }
        }

        var aliveSlimes = new List<SlimeAddEnemy>();
        var slimeIndices = new List<int>();
        for (int i = 0; i < slimeCandidates.Count; i++)
        {
            int index = snapshot.nodes.Count;
            snapshot.nodes.Add(BuildSlimeNode(slimeCandidates[i], index));
            aliveSlimes.Add(slimeCandidates[i]);
            slimeIndices.Add(index);
        }

        var zones = new List<DangerZoneHandle>();
        var zoneIndices = new List<int>();
        for (int i = 0; i < zoneCandidates.Count; i++)
        {
            int index = snapshot.nodes.Count;
            snapshot.nodes.Add(BuildZoneNode(zoneCandidates[i], index));
            zones.Add(zoneCandidates[i]);
            zoneIndices.Add(index);
        }

        // Objective: Add Phase(보스 무적 또는 슬라임 생존) 동안 KILL_SLIMES 목표 활성
        int objectiveIndex = -1;
        if (objectiveActive)
        {
            maxAliveSlimesInPhase = Mathf.Max(maxAliveSlimesInPhase, aliveSlimes.Count);
            objectiveIndex = snapshot.nodes.Count;
            snapshot.nodes.Add(BuildObjectiveNode(aliveSlimes.Count, bossImmune, objectiveIndex));
        }
        else
        {
            maxAliveSlimesInPhase = 0;
        }

        // ---------- 엣지 수집 ----------

        // SELF_LOOP: 모든 노드 → 자기 자신
        for (int i = 0; i < snapshot.nodes.Count; i++)
            snapshot.edges.Add(new GraphEdgeData
            {
                source_index = i,
                target_index = i,
                relation_type_id = GnnSchema.RelationSelfLoop,
                line_of_sight = 1
            });

        // PARTY_RELATION: 파티원 사이 양방향
        for (int i = 0; i < characterIndices.Count; i++)
        {
            for (int j = 0; j < characterIndices.Count; j++)
            {
                if (i == j)
                    continue;

                snapshot.edges.Add(MakeDistanceEdge(
                    characterIndices[i], characterIndices[j], GnnSchema.RelationPartyRelation,
                    characterStatuses[i].transform.position, characterStatuses[j].transform.position));
            }
        }

        for (int i = 0; i < characterStatuses.Count; i++)
        {
            PlayerStatus status = characterStatuses[i];
            NPCSimpleFSMController controller = characterControllers[i];
            int characterIndex = characterIndices[i];
            bool alive = status.Health != null && !status.Health.IsDead;
            Vector3 position = status.transform.position;

            NpcAction action = controller != null ? controller.CurrentPolicyAction : NpcAction.None;

            // 보스 관련 엣지
            if (boss != null && bossHealth != null && !bossHealth.IsDead && alive)
            {
                Vector3 aimPoint = controller != null ? controller.GetBossAimPoint() : boss.transform.position;
                float surfaceDistance = NPCSimpleFSMController.FlatDistance(position, aimPoint);
                float desiredDistance = ResolveDesiredDistance(status, controller);

                // THREATENS: 보스 → 캐릭터
                snapshot.edges.Add(MakeDistanceEdge(bossIndex, characterIndex, GnnSchema.RelationThreatens,
                    boss.transform.position, position));

                // CAN_ATTACK: 캐릭터 → 보스 (공용 Mask의 전술 검사 기준)
                // CAN_ATTACK 엣지는 거리와 무관한 전술 판단으로 생성 (사거리 여부는 in_range 특징이 담당)
                bool canAttackBoss = controller != null
                    ? NpcActionMask.IsBossTacticallyAttackable(controller)
                    : !bossHealth.DamageImmune && (bossPattern == null || !bossPattern.IsFlying);

                if (canAttackBoss)
                {
                    GraphEdgeData edge = MakeDistanceEdge(characterIndex, bossIndex, GnnSchema.RelationCanAttack,
                        position, aimPoint);
                    edge.in_range = surfaceDistance <= desiredDistance ? 1 : 0;
                    snapshot.edges.Add(edge);
                }

                // TARGETS: 현재 정책 행동이 보스를 향하면 연결
                if (action == NpcAction.AttackBoss || action == NpcAction.MoveToBoss)
                {
                    GraphEdgeData edge = MakeDistanceEdge(characterIndex, bossIndex, GnnSchema.RelationTargets,
                        position, aimPoint);
                    edge.is_current_target = 1;
                    snapshot.edges.Add(edge);
                }
            }

            // 슬라임 관련 엣지
            for (int s = 0; s < aliveSlimes.Count; s++)
            {
                if (!alive)
                    break;

                SlimeAddEnemy slime = aliveSlimes[s];
                Vector3 slimePosition = slime.transform.position;
                float slimeDistance = NPCSimpleFSMController.FlatDistance(position, slimePosition);
                float desiredDistance = ResolveDesiredDistance(status, controller);

                GraphEdgeData attackEdge = MakeDistanceEdge(characterIndex, slimeIndices[s], GnnSchema.RelationCanAttack,
                    position, slimePosition);
                attackEdge.in_range = slimeDistance <= desiredDistance ? 1 : 0;
                snapshot.edges.Add(attackEdge);

                // TARGETS: 슬라임 공격 또는 접근 중이면 가장 시급한 슬라임을 대상으로 간주
                if ((action == NpcAction.AttackSlime || action == NpcAction.MoveToSlime)
                    && slime == SlimeTargetSelector.FindMostUrgent(position))
                {
                    GraphEdgeData targetEdge = MakeDistanceEdge(characterIndex, slimeIndices[s], GnnSchema.RelationTargets,
                        position, slimePosition);
                    targetEdge.is_current_target = 1;
                    snapshot.edges.Add(targetEdge);
                }
            }

            // CAN_SUPPORT / NEEDS_SUPPORT: 지원 역할 기준
            int classId = ResolveClassId(status);
            bool isSupport = GnnSchema.RoleIdFromClassId(classId) == GnnSchema.RoleSupport;
            float supportRange = controller != null ? controller.healerFollowDistance : 6f;

            for (int a = 0; a < characterStatuses.Count; a++)
            {
                if (a == i || !alive)
                    continue;

                PlayerStatus ally = characterStatuses[a];
                if (!PartyTargetUtility.IsValidPlayerTarget(ally))
                    continue;

                float allyDistance = NPCSimpleFSMController.FlatDistance(position, ally.transform.position);

                if (isSupport)
                {
                    GraphEdgeData edge = MakeDistanceEdge(characterIndex, characterIndices[a], GnnSchema.RelationCanSupport,
                        position, ally.transform.position);
                    edge.in_range = allyDistance <= supportRange ? 1 : 0;
                    snapshot.edges.Add(edge);
                }

                // NEEDS_SUPPORT: HP가 온전하지 않거나 위험 지역에 있는 아군 → 지원 역할
                bool allySupportRole = GnnSchema.RoleIdFromClassId(ResolveClassId(ally)) == GnnSchema.RoleSupport;
                if (allySupportRole)
                {
                    float hpRatio = NpcHealerPolicy.GetHealthRatio(status);
                    bool needsSupport = hpRatio < 1f || DangerZoneRegistry.IsPointInAnyZone(position, 0.15f);
                    if (needsSupport)
                    {
                        GraphEdgeData edge = MakeDistanceEdge(characterIndex, characterIndices[a], GnnSchema.RelationNeedsSupport,
                            position, ally.transform.position);
                        edge.in_range = allyDistance <= supportRange ? 1 : 0;
                        snapshot.edges.Add(edge);
                    }
                }
            }

            // AFFECTS: 캐릭터를 포함하는 장판 → 캐릭터
            for (int z = 0; z < zones.Count; z++)
            {
                DangerZoneHandle zone = zones[z];
                if (!alive || !zone.ContainsPoint(position, 0.15f))
                    continue;

                GraphEdgeData edge = MakeDistanceEdge(zoneIndices[z], characterIndex, GnnSchema.RelationAffects,
                    zone.transform.position, position);
                edge.in_range = 1;

                float remaining = zone.TimeRemaining;
                if (remaining >= 0f && zone.duration > 0f)
                {
                    edge.time_to_effect_ratio = Mathf.Clamp01(remaining / zone.duration);
                    edge.time_to_effect_valid = 1;
                }

                snapshot.edges.Add(edge);
            }
        }

        // 슬라임 → 추적 대상 (TARGETS + THREATENS)
        for (int s = 0; s < aliveSlimes.Count; s++)
        {
            SlimeAddEnemy slime = aliveSlimes[s];
            PlayerStatus target = slime.CurrentTarget;
            if (target == null)
                continue;

            int targetIndex = -1;
            for (int i = 0; i < characterStatuses.Count; i++)
            {
                if (characterStatuses[i] == target)
                {
                    targetIndex = characterIndices[i];
                    break;
                }
            }

            if (targetIndex < 0)
                continue;

            GraphEdgeData targets = MakeDistanceEdge(slimeIndices[s], targetIndex, GnnSchema.RelationTargets,
                slime.transform.position, target.transform.position);
            targets.is_current_target = 1;
            targets.is_aggro_target = 1;
            snapshot.edges.Add(targets);

            GraphEdgeData threatens = MakeDistanceEdge(slimeIndices[s], targetIndex, GnnSchema.RelationThreatens,
                slime.transform.position, target.transform.position);
            threatens.is_aggro_target = 1;
            threatens.time_to_effect_ratio = Mathf.Clamp01(slime.ExplosionTimeRatio);
            threatens.time_to_effect_valid = 1;
            snapshot.edges.Add(threatens);
        }

        // OWNS_SKILL: 캐릭터 → 스킬, CAN_APPLY_EFFECT: 스킬 → 적용 가능 대상
        for (int s = 0; s < skillIndices.Count; s++)
        {
            int ownerSlot = skillOwnerSlots[s];
            int skillIndex = skillIndices[s];
            int ownerIndex = characterIndices[ownerSlot];
            PlayerStatus owner = characterStatuses[ownerSlot];
            NPCSimpleFSMController ownerController = characterControllers[ownerSlot];
            PlayerSkillDefinition definition = skillDefinitions[s];

            snapshot.edges.Add(new GraphEdgeData
            {
                source_index = ownerIndex,
                target_index = skillIndex,
                relation_type_id = GnnSchema.RelationOwnsSkill,
                line_of_sight = 1
            });

            bool ownerAlive = owner.Health != null && !owner.Health.IsDead;
            if (!ownerAlive)
                continue;

            Vector3 ownerPosition = owner.transform.position;
            float engageDistance = ResolveDesiredDistance(owner, ownerController);
            float supportRange = ownerController != null ? ownerController.healerFollowDistance : 6f;
            bool isSupport = GnnSchema.IsSupportEffect(definition.effectType);

            if (isSupport)
            {
                // 지원 스킬: 살아 있는 아군마다 적용 가능성 연결 (회복량 대비 비율 포함)
                for (int a = 0; a < characterStatuses.Count; a++)
                {
                    PlayerStatus ally = characterStatuses[a];
                    if (!PartyTargetUtility.IsValidPlayerTarget(ally))
                        continue;

                    GraphEdgeData edge = MakeDistanceEdge(skillIndex, characterIndices[a], GnnSchema.RelationCanApplyEffect,
                        ownerPosition, ally.transform.position);
                    edge.in_range = NPCSimpleFSMController.FlatDistance(ownerPosition, ally.transform.position) <= supportRange ? 1 : 0;
                    FillSkillEffectFeatures(edge, definition, ally.Health, isSupport: true);
                    snapshot.edges.Add(edge);
                }

                continue;
            }

            // 공격 스킬: 보스와 슬라임에 적용 가능성 연결 (예상 피해 비율 포함)
            if (bossIndex >= 0 && bossHealth != null && !bossHealth.IsDead)
            {
                Vector3 aimPoint = ownerController != null ? ownerController.GetBossAimPoint() : boss.transform.position;
                GraphEdgeData edge = MakeDistanceEdge(skillIndex, bossIndex, GnnSchema.RelationCanApplyEffect,
                    ownerPosition, aimPoint);
                edge.in_range = NPCSimpleFSMController.FlatDistance(ownerPosition, aimPoint) <= engageDistance ? 1 : 0;
                FillSkillEffectFeatures(edge, definition, bossHealth, isSupport: false);
                snapshot.edges.Add(edge);
            }

            for (int k = 0; k < aliveSlimes.Count; k++)
            {
                Vector3 slimePosition = aliveSlimes[k].transform.position;
                GraphEdgeData edge = MakeDistanceEdge(skillIndex, slimeIndices[k], GnnSchema.RelationCanApplyEffect,
                    ownerPosition, slimePosition);
                edge.in_range = NPCSimpleFSMController.FlatDistance(ownerPosition, slimePosition) <= engageDistance ? 1 : 0;
                FillSkillEffectFeatures(edge, definition, aliveSlimes[k].GetComponent<Health>(), isSupport: false);
                snapshot.edges.Add(edge);
            }
        }

        // REQUIRES: Objective → 처리 대상 슬라임
        if (objectiveIndex >= 0)
        {
            for (int s = 0; s < slimeIndices.Count; s++)
                snapshot.edges.Add(new GraphEdgeData
                {
                    source_index = objectiveIndex,
                    target_index = slimeIndices[s],
                    relation_type_id = GnnSchema.RelationRequires,
                    line_of_sight = 1
                });
        }

        return snapshot;
    }

    // ---------- 노드 빌더 ----------

    private static GraphNodeData BuildCharacterNode(PlayerStatus status, NPCSimpleFSMController controller, int index)
    {
        Health health = status.Health;
        bool dead = health == null || health.IsDead;
        int classId = ResolveClassId(status);

        bool isNpc = status.GetComponent<NPCPartyMember>() != null;
        bool isScripted = controller != null && controller.isScriptedPlayer;

        GraphNodeData node = new GraphNodeData
        {
            node_id = (isNpc ? "NPC_" : "Player_") + status.name,
            node_index = index,
            node_type_id = GnnSchema.NodeTypeCharacter,
            node_family_id = GnnSchema.FamilyActor,
            team_id = GnnSchema.TeamParty,
            controller_id = isNpc || isScripted ? GnnSchema.ControllerPartyAI : GnnSchema.ControllerHumanPlayer,
            is_scripted_player = isScripted ? 1 : 0,

            class_id = classId,
            role_id = GnnSchema.RoleIdFromClassId(classId),

            hp_ratio = NpcHealerPolicy.GetHealthRatio(status),
            hp_valid = 1,
            is_alive = dead ? 0 : 1,
            is_targetable = dead ? 0 : 1,
            is_invulnerable = 0
        };

        FillPositionAndDirection(node, status.transform);

        if (status.Mana != null)
        {
            node.mp_ratio = status.Mana.Normalized;
            node.mp_valid = 1;
        }

        if (status.Shield != null && health != null && health.MaxHealth > 0f)
        {
            node.shield_ratio = Mathf.Max(0f, status.Shield.CurrentShield / health.MaxHealth);
            node.shield_valid = 1;
        }

        PlayerSkillController skills = status.GetComponent<PlayerSkillController>();
        if (skills != null)
        {
            node.ultimate_ratio = Mathf.Clamp01(skills.GetUltimateGaugeNormalized());
            node.is_casting = skills.IsSkillInProgress ? 1 : 0;
            node.skill1_ready = skills.CanUseSkillForUI(PlayerSkillSlot.Skill1, false, out _) ? 1 : 0;
            node.skill2_ready = skills.CanUseSkillForUI(PlayerSkillSlot.Skill2, false, out _) ? 1 : 0;
            node.ultimate_ready = skills.CanUseSkillForUI(PlayerSkillSlot.Ultimate, false, out _) ? 1 : 0;
        }

        if (controller != null)
        {
            NpcAction action = controller.CurrentPolicyAction;
            node.policy_action_id = (int)action;

            // DAgger 라벨: 이 상태에서 교사(Utility)가 고를 행동.
            // 학습 모델이 조종한 판의 상태를 교사 행동으로 재라벨링하는 데 사용 (Ross et al. 2011)
            node.teacher_action_id = controller.ComputeTeacherAdviceId();
            node.activity_id = dead ? GnnSchema.ActivityDead
                : node.is_casting == 1 ? GnnSchema.ActivityCast
                : NpcActions.GetActivityId(action);
            node.has_target = IsTargetedAction(action) ? 1 : 0;

            // 행동 Mask: 학습 모델의 행동 후보와 동일한 기준 (전술 검사 포함)
            node.action_mask = new int[NpcActions.ActionCount];
            for (int i = 0; i < NpcActionMask.AllActions.Length; i++)
            {
                NpcAction candidate = NpcActionMask.AllActions[i];
                node.action_mask[(int)candidate] =
                    NpcActionMask.IsAvailable(controller, candidate, includeTacticalChecks: true) ? 1 : 0;
            }
            node.action_mask_valid = 1;

            if (controller.navMeshAgent != null && controller.navMeshAgent.enabled)
            {
                float speed = controller.navMeshAgent.velocity.magnitude;
                node.speed_ratio = controller.moveSpeed > 0f ? Mathf.Clamp01(speed / controller.moveSpeed) : 0f;
                node.is_moving = speed > 0.1f ? 1 : 0;
            }
        }
        else
        {
            // 사람 조작 캐릭터: 정책 행동이 없으므로 Mask와 행동은 무효 처리
            node.activity_id = dead ? GnnSchema.ActivityDead : GnnSchema.ActivityIdle;
            node.action_mask = new int[NpcActions.ActionCount];
            node.action_mask_valid = 0;
        }

        return node;
    }

    private static GraphNodeData BuildBossNode(BossDummyController boss, Health health, BossSkillPatternController pattern, int index)
    {
        bool dead = health == null || health.IsDead;
        bool flying = pattern != null && pattern.IsFlying;
        string patternName = pattern != null ? pattern.CurrentPatternName : string.Empty;
        bool immune = health != null && health.DamageImmune;

        GraphNodeData node = new GraphNodeData
        {
            node_id = "Boss_" + boss.name,
            node_index = index,
            node_type_id = GnnSchema.NodeTypeBoss,
            node_family_id = GnnSchema.FamilyActor,
            team_id = GnnSchema.TeamEnemy,
            controller_id = GnnSchema.ControllerEnemyAI,

            hp_ratio = health != null && health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f,
            hp_valid = health != null ? 1 : 0,
            is_alive = dead ? 0 : 1,
            is_targetable = dead || flying ? 0 : 1,
            is_invulnerable = immune ? 1 : 0,
            is_flying = flying ? 1 : 0,
            is_add_phase = immune ? 1 : 0,

            pattern_name = patternName,
            pattern_active = string.IsNullOrEmpty(patternName) ? 0 : 1,
            activity_id = dead ? GnnSchema.ActivityDead
                : !string.IsNullOrEmpty(patternName) ? GnnSchema.ActivityCast
                : GnnSchema.ActivityIdle
        };

        FillPositionAndDirection(node, boss.transform);
        return node;
    }

    private static GraphNodeData BuildSlimeNode(SlimeAddEnemy slime, int index)
    {
        Health health = slime.GetComponent<Health>();

        GraphNodeData node = new GraphNodeData
        {
            node_id = "Slime_" + slime.GetInstanceID(),
            node_index = index,
            node_type_id = GnnSchema.NodeTypeSlime,
            node_family_id = GnnSchema.FamilyActor,
            team_id = GnnSchema.TeamEnemy,
            controller_id = GnnSchema.ControllerEnemyAI,

            hp_ratio = health != null && health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 1f,
            hp_valid = health != null ? 1 : 0,
            is_alive = 1,
            is_targetable = 1,
            is_moving = 1,
            activity_id = GnnSchema.ActivityMove,
            has_target = slime.CurrentTarget != null ? 1 : 0,

            explosion_time_ratio = Mathf.Clamp01(slime.ExplosionTimeRatio),
            time_remaining_ratio = Mathf.Clamp01(slime.ExplosionTimeRatio),
            time_remaining_valid = 1,
            is_explosion_armed = 1,
            creates_persistent_zone = slime.CreatesPersistentZone ? 1 : 0,
            is_objective_target = 1
        };

        FillPositionAndDirection(node, slime.transform);
        return node;
    }

    private static GraphNodeData BuildZoneNode(DangerZoneHandle zone, int index)
    {
        float remaining = zone.TimeRemaining;
        bool hasTime = !zone.persistent && zone.duration > 0f && remaining >= 0f;

        GraphNodeData node = new GraphNodeData
        {
            node_id = "Zone_" + zone.zoneId,
            node_index = index,
            node_type_id = GnnSchema.NodeTypeDangerZone,
            node_family_id = GnnSchema.FamilyEffect,
            team_id = GnnSchema.TeamEnemy,
            controller_id = GnnSchema.ControllerEnvironment,

            effect_type_id = zone.tickDamage > 0f ? GnnSchema.EffectDot : GnnSchema.EffectDamage,
            target_scope_id = GnnSchema.ScopeArea,
            shape_id = GnnSchema.ShapeIdFromZoneShape(zone.shape),
            zone_stage_id = GnnSchema.ZoneStageFromCategory(zone.category),
            source_type_id = GnnSchema.ZoneSourceFromCategory(zone.category),
            is_persistent = zone.persistent ? 1 : 0,

            radius_ratio = ArenaBounds.LengthRatio(zone.radius),
            radius_valid = zone.shape == DangerZoneShape.Circle || zone.shape == DangerZoneShape.Cone ? 1 : 0,
            length_ratio = ArenaBounds.LengthRatio(zone.length),
            length_valid = zone.shape == DangerZoneShape.Rectangle || zone.shape == DangerZoneShape.Cone ? 1 : 0,
            width_ratio = ArenaBounds.LengthRatio(zone.width),
            width_valid = zone.shape == DangerZoneShape.Rectangle ? 1 : 0,
            angle_ratio = Mathf.Clamp01(zone.angle / 360f),
            angle_valid = zone.shape == DangerZoneShape.Cone ? 1 : 0,

            time_remaining_ratio = hasTime ? Mathf.Clamp01(remaining / zone.duration) : 0f,
            time_remaining_valid = hasTime ? 1 : 0
        };

        FillPositionAndDirection(node, zone.transform);
        return node;
    }

    private static GraphNodeData BuildSkillNode(PlayerStatus owner, PlayerSkillController skills,
        PlayerSkillDefinition definition, PlayerSkillSlot slot, int index)
    {
        bool isSupport = GnnSchema.IsSupportEffect(definition.effectType);
        bool requiresUltimate = definition.ultimateCost > 0f || slot == PlayerSkillSlot.Ultimate;

        float maxMana = owner.Mana != null ? Mathf.Max(1f, owner.Mana.MaxMana) : 100f;
        float cooldownDuration = skills.GetCooldownDuration(slot);

        GraphNodeData node = new GraphNodeData
        {
            node_id = "Skill_" + owner.name + "_" + slot,
            node_index = index,
            node_type_id = GnnSchema.NodeTypeSkill,
            node_family_id = GnnSchema.FamilyEffect,
            team_id = GnnSchema.TeamParty,
            controller_id = GnnSchema.ControllerPartyAI,

            skill_slot_id = GnnSchema.SkillSlotId(slot),
            effect_type_id = GnnSchema.EffectTypeFromSkill(definition.effectType),
            target_scope_id = isSupport
                ? (definition.effectType == PlayerSkillEffectType.HealNearestAlly ? GnnSchema.ScopeSingle : GnnSchema.ScopeParty)
                : (definition.useAreaDamage ? GnnSchema.ScopeArea : GnnSchema.ScopeSingle),
            shape_id = definition.useAreaDamage ? GnnSchema.ShapeCircle : GnnSchema.ShapeNone,

            mana_cost_ratio = Mathf.Clamp01(definition.manaCost / maxMana),
            cooldown_ratio = Mathf.Clamp01(skills.GetCooldownNormalized(slot)),
            cooldown_valid = cooldownDuration > 0f ? 1 : 0,
            cast_time_ratio = Mathf.Clamp01(definition.actionDuration / 3f),
            skill_ready = skills.CanUseSkillForUI(slot, checkTargetRange: false, out _) ? 1 : 0,
            requires_ultimate = requiresUltimate ? 1 : 0,
            duration_ratio = Mathf.Clamp01(definition.duration / 10f),

            // 스킬은 위치와 방향, 생명력 개념이 없는 노드
            position_valid = 0,
            direction_valid = 0,
            is_active = 1
        };

        return node;
    }

    private static GraphNodeData BuildObjectiveNode(int aliveSlimeCount, bool bossImmune, int index)
    {
        float remainingRatio = maxAliveSlimesInPhase > 0
            ? Mathf.Clamp01(aliveSlimeCount / (float)maxAliveSlimesInPhase)
            : 0f;

        return new GraphNodeData
        {
            node_id = "Objective_KillSlimes",
            node_index = index,
            node_type_id = GnnSchema.NodeTypeObjective,
            node_family_id = GnnSchema.FamilyObjective,
            team_id = GnnSchema.TeamNeutral,
            controller_id = GnnSchema.ControllerNone,

            objective_type_id = GnnSchema.ObjectiveKillSlimes,
            progress_ratio = 1f - remainingRatio,
            remaining_count_ratio = remainingRatio,
            blocks_boss_damage = bossImmune ? 1 : 0,
            failure_causes_damage = 1,

            position_valid = 0,
            direction_valid = 0
        };
    }

    // ---------- 공통 유틸 ----------

    private static void FillPositionAndDirection(GraphNodeData node, Transform transform)
    {
        node.pos_x_norm = ArenaBounds.NormalizeX(transform.position.x);
        node.pos_z_norm = ArenaBounds.NormalizeZ(transform.position.z);
        node.position_valid = 1;

        Vector3 forward = transform.forward;
        node.dir_x = forward.x;
        node.dir_z = forward.z;
        node.direction_valid = 1;
    }

    private static GraphEdgeData MakeDistanceEdge(int source, int target, int relation, Vector3 sourcePosition, Vector3 targetPosition)
    {
        return new GraphEdgeData
        {
            source_index = source,
            target_index = target,
            relation_type_id = relation,
            distance_ratio = ArenaBounds.DistanceRatio(sourcePosition, targetPosition),
            distance_valid = 1,
            line_of_sight = 1
        };
    }

    // 스킬 적용 엣지의 효과 관련 특징: 대상 최대 HP 대비 예상 피해 또는 회복 비율
    private static void FillSkillEffectFeatures(GraphEdgeData edge, PlayerSkillDefinition definition, Health targetHealth, bool isSupport)
    {
        if (targetHealth == null || targetHealth.MaxHealth <= 0f)
            return;

        float amount;
        if (isSupport)
        {
            amount = definition.effectType == PlayerSkillEffectType.ShieldAllAllies
                ? definition.shieldAmount
                : (definition.effectType == PlayerSkillEffectType.HealOverTimeAllAllies ? definition.tickHealAmount : definition.healAmount);
        }
        else
        {
            amount = definition.effectType == PlayerSkillEffectType.DamageOverTime
                ? definition.tickDamage
                : definition.damage * Mathf.Max(1, definition.multiHitCount);
        }

        edge.expected_effect_ratio = Mathf.Max(0f, amount / targetHealth.MaxHealth);
        edge.expected_effect_valid = 1;
    }

    // 위험 지역 선별 기준: 파티원 중 가장 가까운 캐릭터까지의 평면 거리
    private static float DistanceToNearestCharacter(DangerZoneHandle zone, List<PlayerStatus> characters)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == null)
                continue;

            float distance = NPCSimpleFSMController.FlatDistance(zone.transform.position, characters[i].transform.position);
            if (distance < best)
                best = distance;
        }

        return best;
    }

    private static int ResolveClassId(PlayerStatus status)
    {
        PlayerClassInfo info = status.GetComponent<PlayerClassInfo>();
        return GnnSchema.ClassIdFromCharacterId(info != null ? info.characterId : status.name);
    }

    private static float ResolveDesiredDistance(PlayerStatus status, NPCSimpleFSMController controller)
    {
        if (controller != null)
            return GnnSchema.RoleIdFromClassId(ResolveClassId(status)) == GnnSchema.RoleFrontline
                ? controller.meleeDistance
                : controller.rangedDistance;

        return 5.5f;
    }

    private static bool IsTargetedAction(NpcAction action)
    {
        switch (action)
        {
            case NpcAction.AttackBoss:
            case NpcAction.AttackSlime:
            case NpcAction.MoveToBoss:
            case NpcAction.MoveToSlime:
            case NpcAction.MoveToAllyAndHeal:
            case NpcAction.ShieldDangerAlly:
                return true;
            default:
                return false;
        }
    }
}
