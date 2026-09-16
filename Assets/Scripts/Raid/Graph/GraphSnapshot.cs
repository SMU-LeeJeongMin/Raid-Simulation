using System;
using System.Collections.Generic;

/// <summary>
/// 그래프 스냅샷 데이터 구조 (GNN_SCHEMA_V1).
/// 필드 이름은 Node 문서, Message passing 문서의 표기와 동일하게 유지하여
/// Colab 학습 코드에서 문서를 그대로 참조할 수 있게 함.
/// 모든 노드가 같은 필드 집합을 가지며, 적용되지 않는 특징은 값 0 + Valid Mask 0으로 구분.
/// </summary>
[Serializable]
public class GraphSnapshot
{
    public string schema_version = GnnSchema.Version;
    public string episode_id;
    public string algorithm;
    public int tick;
    public float t;                    // 에피소드 시작 이후 경과 시간 (초)
    public List<GraphNodeData> nodes = new List<GraphNodeData>();
    public List<GraphEdgeData> edges = new List<GraphEdgeData>();
}

[Serializable]
public class GraphNodeData
{
    // ---------- 공통 Core ----------
    public string node_id;
    public int node_index;
    public int node_type_id;
    public int node_family_id;
    public int team_id;
    public int controller_id;
    public int is_active = 1;

    public float pos_x_norm;
    public float pos_z_norm;
    public int position_valid;
    public float dir_x;
    public float dir_z;
    public int direction_valid;

    // ---------- Actor 계열 공통 ----------
    public float hp_ratio;
    public int hp_valid;
    public int is_alive;
    public int is_targetable;
    public int is_invulnerable;
    public int is_moving;
    public float speed_ratio;
    public int activity_id;
    public int has_target;

    // ---------- Character ----------
    public int class_id;
    public int role_id;
    public float mp_ratio;
    public int mp_valid;
    public float shield_ratio;
    public int shield_valid;
    public float ultimate_ratio;
    public int is_casting;
    public int policy_action_id;
    public int teacher_action_id = -1; // DAgger 라벨: 이 상태에서 교사(Utility)가 고를 행동 (-1이면 무효)
    public int command_id;             // 조율자 지시 코드 (0 Free ~ 4 HoldDefensive, RL 학습 데이터)
    public int command_target_class_id; // ProtectAlly 지시의 보호 대상 직업 코드 (없으면 0)
    public int[] action_mask;          // NpcAction 0~12의 실행 가능 여부 (전술 검사 포함)
    public int action_mask_valid;
    public int skill1_ready;           // v1 확장: SKILL 노드 도입 전의 캐릭터 수준 요약
    public int skill2_ready;
    public int ultimate_ready;
    public int is_scripted_player;     // v1 확장: 자동 플레이어 여부 (학습 데이터 필터링용)

    // ---------- Boss ----------
    public string pattern_name;
    public int pattern_active;
    public int is_flying;
    public int is_add_phase;

    // ---------- Slime ----------
    public float explosion_time_ratio;
    public int is_explosion_armed;
    public int creates_persistent_zone;
    public int is_objective_target;

    // ---------- Effect 계열 (DangerZone) ----------
    public int effect_type_id;
    public int target_scope_id;
    public int shape_id;
    public int zone_stage_id;
    public int source_type_id;
    public int is_persistent;
    public float radius_ratio;
    public int radius_valid;
    public float length_ratio;
    public int length_valid;
    public float width_ratio;
    public int width_valid;
    public float angle_ratio;
    public int angle_valid;
    public float time_remaining_ratio;
    public int time_remaining_valid;

    // ---------- Skill ----------
    public int skill_slot_id;
    public float mana_cost_ratio;
    public float cooldown_ratio;
    public int cooldown_valid;
    public float cast_time_ratio;
    public int skill_ready;
    public int requires_ultimate;
    public float duration_ratio;

    // ---------- Objective ----------
    public int objective_type_id;
    public float progress_ratio;
    public float remaining_count_ratio;
    public int blocks_boss_damage;
    public int failure_causes_damage;
}

[Serializable]
public class GraphEdgeData
{
    public int source_index;
    public int target_index;
    public int relation_type_id;

    // 공통 엣지 특징 (Message passing 문서 4장)
    public float distance_ratio;
    public int distance_valid;
    public int in_range;
    public int line_of_sight;          // 현재 아레나는 개방 지형이므로 항상 1 (장애물 도입 시 판정 교체)
    public int is_current_target;
    public int is_aggro_target;
    public float time_to_effect_ratio;
    public int time_to_effect_valid;
    public float expected_effect_ratio;
    public int expected_effect_valid;
    public int edge_active = 1;
}
