using UnityEngine;

/// <summary>
/// 그래프 스냅샷을 학습 때와 동일한 순서, 동일한 규칙의 텐서로 인코딩.
/// 순서와 규칙은 매니페스트(*_policy_v1.manifest.json)와 1:1 대응하며 변경 시 재학습 필요.
///
/// 노드 특징: 원핫 4블록(노드 종류 6, 직업 5, 역할 4, 행동 상태 9) + 스칼라 30 = 54차원
/// 엣지 특징: 관계 원핫 11 + 스칼라 9 = 20차원 (GATv2 전용)
///
/// 모델별 그래프 입력 형식:
/// - WeightedUndirected (GCN)      : 무방향 근접 가중 인접 [N, N]
/// - Relational (RGCN)             : 관계별 방향 인접 [R, N, N]
/// - DirectedEdgeFeatures (GATv2)  : 방향 인접 [N, N] + 엣지 특징 [N, N, 20]
/// </summary>
public static class GnnFeatureEncoder
{
    // 원핫 38 (노드 종류 6, 직업 5, 역할 4, 행동 상태 9, 스킬 슬롯 4, 효과 종류 6, 적용 범위 4) + 스칼라 37
    public const int FeatureDim = 75;
    public const int NumActions = 17;
    public const int EdgeFeatureDim = 20;

    // 매니페스트의 scalar_features와 동일한 순서 (검증용)
    public static readonly string[] ScalarFeatureNames =
    {
        "hp_ratio", "hp_valid", "mp_ratio", "shield_ratio", "ultimate_ratio",
        "is_alive", "is_targetable", "is_invulnerable", "is_moving", "is_casting",
        "speed_ratio", "has_target",
        "pos_x_norm", "pos_z_norm", "position_valid", "dir_x", "dir_z",
        "explosion_time_ratio", "time_remaining_ratio", "time_remaining_valid",
        "skill1_ready", "skill2_ready", "ultimate_ready",
        "is_flying", "is_add_phase", "pattern_active",
        "radius_ratio", "is_persistent",
        "blocks_boss_damage", "remaining_count_ratio",
        "mana_cost_ratio", "cooldown_ratio", "cooldown_valid", "cast_time_ratio",
        "skill_ready", "requires_ultimate", "duration_ratio",
    };

    // 매니페스트의 edge_scalar_names와 동일한 순서
    public static readonly string[] EdgeScalarNames =
    {
        "distance_ratio", "distance_valid", "in_range", "line_of_sight", "is_current_target",
        "is_aggro_target", "time_to_effect_ratio", "time_to_effect_valid", "edge_active",
    };

    // ---------- 노드 인코딩 ----------

    // node의 특징을 buffer[offset..offset+54)에 기록
    public static void Encode(GraphNodeData node, float[] buffer, int offset)
    {
        int cursor = offset;

        // 캐릭터 노드의 자기 행동 누설 특징(activity, has_target, is_moving, speed_ratio) 차단:
        // 모두 현재 수행 중인 행동의 결과값이라 라벨과 거의 1:1 대응.
        // 남겨두면 상황 판단 대신 현재 행동을 복사하는 지름길을 학습하고,
        // 배포 시 자기 출력이 다시 입력이 되어 한 행동에 갇히는 자기강화 고리 발생.
        // 보스와 슬라임의 같은 특징은 정당한 관찰 정보이므로 유지 (노트북 인코딩과 동일 규칙)
        bool isCharacter = node.node_type_id == GnnSchema.NodeTypeCharacter;

        // 원핫 블록 (매니페스트 onehot_blocks 순서: 종류, 직업, 역할, 행동 상태, 스킬 슬롯, 효과 종류, 적용 범위)
        cursor = WriteOneHot(buffer, cursor, node.node_type_id - 1, 6);
        cursor = WriteOneHot(buffer, cursor, node.class_id, 5);
        cursor = WriteOneHot(buffer, cursor, node.role_id, 4);
        cursor = WriteOneHot(buffer, cursor, isCharacter ? -1 : node.activity_id, 9);
        cursor = WriteOneHot(buffer, cursor, node.skill_slot_id, 4);
        cursor = WriteOneHot(buffer, cursor, node.effect_type_id, 6);
        cursor = WriteOneHot(buffer, cursor, node.target_scope_id, 4);

        // 스칼라 (ScalarFeatureNames 순서와 반드시 일치)
        buffer[cursor++] = node.hp_ratio;
        buffer[cursor++] = node.hp_valid;
        buffer[cursor++] = node.mp_ratio;
        buffer[cursor++] = node.shield_ratio;
        buffer[cursor++] = node.ultimate_ratio;
        buffer[cursor++] = node.is_alive;
        buffer[cursor++] = node.is_targetable;
        buffer[cursor++] = node.is_invulnerable;
        buffer[cursor++] = isCharacter ? 0f : node.is_moving;
        buffer[cursor++] = node.is_casting;
        buffer[cursor++] = isCharacter ? 0f : node.speed_ratio;
        buffer[cursor++] = isCharacter ? 0f : node.has_target;
        buffer[cursor++] = node.pos_x_norm;
        buffer[cursor++] = node.pos_z_norm;
        buffer[cursor++] = node.position_valid;
        buffer[cursor++] = node.dir_x;
        buffer[cursor++] = node.dir_z;
        buffer[cursor++] = node.explosion_time_ratio;
        buffer[cursor++] = node.time_remaining_ratio;
        buffer[cursor++] = node.time_remaining_valid;
        buffer[cursor++] = node.skill1_ready;
        buffer[cursor++] = node.skill2_ready;
        buffer[cursor++] = node.ultimate_ready;
        buffer[cursor++] = node.is_flying;
        buffer[cursor++] = node.is_add_phase;
        buffer[cursor++] = node.pattern_active;
        buffer[cursor++] = node.radius_ratio;
        buffer[cursor++] = node.is_persistent;
        buffer[cursor++] = node.blocks_boss_damage;
        buffer[cursor++] = node.remaining_count_ratio;
        buffer[cursor++] = node.mana_cost_ratio;
        buffer[cursor++] = node.cooldown_ratio;
        buffer[cursor++] = node.cooldown_valid;
        buffer[cursor++] = node.cast_time_ratio;
        buffer[cursor++] = node.skill_ready;
        buffer[cursor++] = node.requires_ultimate;
        buffer[cursor++] = node.duration_ratio;

        if (cursor - offset != FeatureDim)
            Debug.LogError($"[GnnFeatureEncoder] 특징 차원 불일치: {cursor - offset} != {FeatureDim}");
    }

    private static void EncodeNodes(GraphSnapshot snapshot, int maxNodes, float[] featureBuffer)
    {
        System.Array.Clear(featureBuffer, 0, featureBuffer.Length);

        for (int i = 0; i < snapshot.nodes.Count; i++)
        {
            GraphNodeData node = snapshot.nodes[i];
            if (node.node_index < maxNodes)
                Encode(node, featureBuffer, node.node_index * FeatureDim);
        }
    }

    // ---------- GCN: 무방향 근접 가중 인접 ----------

    // 엣지 가중치: 거리 정보가 있으면 근접도와 사거리 여부의 평균, 없으면 구조적 연결로 1.0
    // (노트북 edge_proximity_weight와 동일 식)
    private static float ProximityWeight(GraphEdgeData edge)
    {
        if (edge.distance_valid > 0.5f)
            return Mathf.Clamp01(0.5f * (1f - edge.distance_ratio) + 0.5f * edge.in_range);

        return 1f;
    }

    // GCN은 관계 유형을 구분하지 못하므로 근접 가중치로 거리 감각을 전달.
    // SELF_LOOP는 정규화 단계에서 A + I로 추가되므로 제외 (중복 방지)
    public static void EncodeWeightedUndirected(GraphSnapshot snapshot, int maxNodes,
        float[] featureBuffer, float[] adjacencyBuffer)
    {
        EncodeNodes(snapshot, maxNodes, featureBuffer);
        System.Array.Clear(adjacencyBuffer, 0, adjacencyBuffer.Length);

        for (int i = 0; i < snapshot.edges.Count; i++)
        {
            GraphEdgeData edge = snapshot.edges[i];
            if (edge.relation_type_id == GnnSchema.RelationSelfLoop)
                continue;

            int source = edge.source_index;
            int target = edge.target_index;
            if (source >= maxNodes || target >= maxNodes)
                continue;

            float weight = ProximityWeight(edge);
            int a = target * maxNodes + source;
            int b = source * maxNodes + target;
            adjacencyBuffer[a] = Mathf.Max(adjacencyBuffer[a], weight);
            adjacencyBuffer[b] = Mathf.Max(adjacencyBuffer[b], weight);
        }
    }

    // ---------- RGCN: 관계별 방향 인접 [R, N, N] ----------

    // SELF_LOOP는 자기 가중치 행렬 W_0가 담당하므로 제외
    public static void EncodeRelational(GraphSnapshot snapshot, int maxNodes,
        float[] featureBuffer, float[] relationalAdjacencyBuffer)
    {
        EncodeNodes(snapshot, maxNodes, featureBuffer);
        System.Array.Clear(relationalAdjacencyBuffer, 0, relationalAdjacencyBuffer.Length);

        int matrixSize = maxNodes * maxNodes;

        for (int i = 0; i < snapshot.edges.Count; i++)
        {
            GraphEdgeData edge = snapshot.edges[i];
            int relation = edge.relation_type_id;
            if (relation == GnnSchema.RelationSelfLoop || relation < 0 || relation >= GnnSchema.RelationCount)
                continue;

            int source = edge.source_index;
            int target = edge.target_index;
            if (source >= maxNodes || target >= maxNodes)
                continue;

            relationalAdjacencyBuffer[relation * matrixSize + target * maxNodes + source] = 1f;
        }
    }

    // ---------- GATv2: 방향 인접 + 엣지 특징 ----------

    // 같은 노드쌍의 병렬 엣지는 관계 원핫 다중 활성 + 스칼라 최대값으로 병합 (학습과 동일 규칙)
    public static void EncodeDirectedWithEdgeFeatures(GraphSnapshot snapshot, int maxNodes,
        float[] featureBuffer, float[] adjacencyBuffer, float[] edgeBuffer)
    {
        EncodeNodes(snapshot, maxNodes, featureBuffer);
        System.Array.Clear(adjacencyBuffer, 0, adjacencyBuffer.Length);
        System.Array.Clear(edgeBuffer, 0, edgeBuffer.Length);

        for (int i = 0; i < snapshot.edges.Count; i++)
        {
            GraphEdgeData edge = snapshot.edges[i];
            int source = edge.source_index;
            int target = edge.target_index;
            if (source >= maxNodes || target >= maxNodes)
                continue;

            adjacencyBuffer[target * maxNodes + source] = 1f;

            int baseIndex = (target * maxNodes + source) * EdgeFeatureDim;

            if (edge.relation_type_id >= 0 && edge.relation_type_id < GnnSchema.RelationCount)
                edgeBuffer[baseIndex + edge.relation_type_id] = 1f;

            int scalarBase = baseIndex + GnnSchema.RelationCount;
            edgeBuffer[scalarBase + 0] = Mathf.Max(edgeBuffer[scalarBase + 0], edge.distance_ratio);
            edgeBuffer[scalarBase + 1] = Mathf.Max(edgeBuffer[scalarBase + 1], edge.distance_valid);
            edgeBuffer[scalarBase + 2] = Mathf.Max(edgeBuffer[scalarBase + 2], edge.in_range);
            edgeBuffer[scalarBase + 3] = Mathf.Max(edgeBuffer[scalarBase + 3], edge.line_of_sight);
            edgeBuffer[scalarBase + 4] = Mathf.Max(edgeBuffer[scalarBase + 4], edge.is_current_target);
            edgeBuffer[scalarBase + 5] = Mathf.Max(edgeBuffer[scalarBase + 5], edge.is_aggro_target);
            edgeBuffer[scalarBase + 6] = Mathf.Max(edgeBuffer[scalarBase + 6], edge.time_to_effect_ratio);
            edgeBuffer[scalarBase + 7] = Mathf.Max(edgeBuffer[scalarBase + 7], edge.time_to_effect_valid);
            edgeBuffer[scalarBase + 8] = Mathf.Max(edgeBuffer[scalarBase + 8], edge.edge_active);
        }
    }

    private static int WriteOneHot(float[] buffer, int cursor, int value, int size)
    {
        if (value >= 0 && value < size)
            buffer[cursor + value] = 1f;
        return cursor + size;
    }
}
