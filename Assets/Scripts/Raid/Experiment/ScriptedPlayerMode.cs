using System.Collections;
using UnityEngine;

/// <summary>
/// 실험용 자동 플레이어(scripted player) 모드.
/// 스폰된 플레이어 캐릭터에 NPC와 동일한 정책 스택(NPCSimpleFSMController)을 연결하여
/// 사람 조작 변동성을 제거한 반복 실험을 가능하게 하는 씬 설정 컴포넌트.
///
/// 실험 원칙: NPC 알고리즘 조건(BasicFSM, PatternAwareFSM, GAT 등)이 바뀌어도
/// playerPolicy는 모든 조건에서 동일하게 유지해야 알고리즘 비교의 공정성이 성립.
/// 사람이 직접 조작하는 시연이나 사용자 실험에서는 enableScriptedPlayer를 끄면
/// 기존과 완전히 동일하게 동작 (플레이어 프리팹 및 씬 구성 변경 없음).
/// </summary>
public class ScriptedPlayerMode : MonoBehaviour
{
    [Header("Mode")]
    public bool enableScriptedPlayer = true;

    [Tooltip("플레이어에 적용할 고정 정책. 알고리즘 비교 실험 중에는 변경 금지")]
    public NPCFSMMode playerPolicy = NPCFSMMode.PatternAwareFSM;

    [Header("References")]
    public RaidSelectedCharacterSpawner playerSpawner;

    [Header("Debug")]
    public bool logSetup = true;
    [SerializeField] private bool applied;

    public static ScriptedPlayerMode Instance { get; private set; }

    // 현재 에피소드의 플레이어 조작 모드 (지표 기록용, 3단계 CSV 컬럼의 입력원)
    public static string PlayerControllerModeName =>
        Instance != null && Instance.enableScriptedPlayer ? "scripted" : "human";

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (enableScriptedPlayer)
            StartCoroutine(AttachRoutine());
    }

    // 플레이어 스폰을 기다렸다가 정책 스택 연결 (0.1초 간격 재시도)
    private IEnumerator AttachRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f);

        while (!applied)
        {
            TryAttach();
            if (applied)
                yield break;

            yield return wait;
        }
    }

    private void TryAttach()
    {
        if (playerSpawner == null)
            playerSpawner = FindFirstObjectByType<RaidSelectedCharacterSpawner>();

        if (playerSpawner == null || playerSpawner.SpawnedPlayer == null)
            return;

        GameObject player = playerSpawner.SpawnedPlayer;

        NPCSimpleFSMController controller = player.GetComponent<NPCSimpleFSMController>();
        if (controller == null)
            controller = player.AddComponent<NPCSimpleFSMController>();

        // 자동 플레이어 전용 설정
        // - 자기 자신을 기준점으로 지정 (대형 유지 로직은 자기 기준일 때 정지로 처리됨)
        // - NPC 행동 분포 지표를 오염시키지 않도록 행동 보고 차단
        // - 실험 셋업의 알고리즘 일괄 변경에서 제외되도록 표시
        controller.isScriptedPlayer = true;
        controller.reportActionsToMetrics = false;
        controller.aiMode = playerPolicy;
        controller.playerTarget = player.transform;

        applied = true;

        if (logSetup)
            Debug.Log($"[ScriptedPlayerMode] 자동 플레이어 적용: {player.name}, 정책 {playerPolicy}", this);
    }
}
