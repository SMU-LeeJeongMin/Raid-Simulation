using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 에피소드 자동 반복 실행기 (4단계).
/// 알고리즘 목록 x 회차 수만큼 에피소드를 자동 실행하며, 회차마다 씬을 재시작하여
/// 보스 HP, 슬라임, 장판, 위치, 쿨타임 등 모든 상태를 완전히 초기화.
///
/// 배치 진행 상태는 정적 필드로 유지되어 씬 재시작을 넘어 이어짐.
/// 같은 회차 번호에 같은 seed를 사용(pairSeedsAcrossAlgorithms)하면
/// 알고리즘 간 짝지은 비교(paired comparison, Wilcoxon signed-rank 등)가 가능.
///
/// 씬 설정: Raid 씬의 관리 오브젝트에 부착, enableBatch 체크.
/// Raid 씬이 Build Settings에 포함되어 있어야 재시작이 동작.
/// </summary>
public class ExperimentBatchRunner : MonoBehaviour
{
    [Header("Batch")]
    [Tooltip("배치 실행 여부. 꺼져 있으면 이 컴포넌트는 아무 동작도 하지 않음")]
    public bool enableBatch = false;
    [Min(1)] public int episodesPerAlgorithm = 30;
    [Tooltip("순서대로 실행할 알고리즘 목록. 각 알고리즘마다 episodesPerAlgorithm회 실행")]
    public NPCFSMMode[] algorithms = { NPCFSMMode.BasicFSM, NPCFSMMode.PatternAwareFSM };

    [Tooltip("algorithms와 같은 길이의 조율자 모드 목록. 비우거나 짧으면 해당 조건은 None(조율 없음)")]
    public CoordinatorMode[] coordinatorModes;

    [Header("Seed")]
    public int baseSeed = 1000;
    [Min(1)] public int seedStride = 1;
    [Tooltip("켜면 같은 회차 번호에 같은 seed 사용 (알고리즘 간 짝지은 비교 가능). 끄면 전체 회차에 서로 다른 seed")]
    public bool pairSeedsAcrossAlgorithms = true;

    [Header("Flow")]
    [Tooltip("에피소드 종료 후 다음 재시작까지의 대기 시간")]
    [Min(0f)] public float restartDelay = 2f;
    [Tooltip("이 시간을 넘긴 에피소드는 timeout으로 강제 종료 (교착 방지). 0이면 제한 없음")]
    [Min(0f)] public float episodeTimeLimit = 600f;
    [Tooltip("실험 가속 배율. 물리와 애니메이션 안정성을 위해 3 이하 권장. clear_time은 게임 시간 기준이라 배율과 무관하게 비교 가능")]
    [Range(0.5f, 10f)] public float timeScale = 1f;
    [Tooltip("전체 완료 시 에디터 재생 정지 (빌드에서는 종료)")]
    public bool stopWhenComplete = true;
    public bool logProgress = true;

    [Header("Debug")]
    [SerializeField] private int currentEpisodeNumber;
    [SerializeField] private int totalEpisodes;
    [SerializeField] private string currentAlgorithmLabel;
    [SerializeField] private int currentSeed;

    // 씬 재시작을 넘어 유지되는 배치 진행 상태
    private static bool batchActive;
    private static int episodeCounter;

    // 배치 진행 중의 조건 라벨 (조율자 결합 라벨).
    // NPCPolicyExperimentSetup의 지연 재적용이 로거의 algorithmName을 aiMode 이름으로
    // 되돌리는 것을 막기 위한 공유 지점 (null이면 배치 비활성 = aiMode 이름 사용)
    public static string ActiveConditionLabel { get; private set; }

    // 재생 시작 시 정적 상태 초기화 (에디터의 Domain Reload 비활성 설정에서도 이전 배치 상태가 남지 않도록)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        batchActive = false;
        episodeCounter = 0;
        ActiveConditionLabel = null;
    }

    private RaidMetricsLogger logger;
    private bool episodeFinishedHandled;

    private void OnEnable()
    {
        RaidMetricsLogger.EpisodeFinished += OnEpisodeFinished;
    }

    private void OnDisable()
    {
        RaidMetricsLogger.EpisodeFinished -= OnEpisodeFinished;
    }

    private void Awake()
    {
        if (!enableBatch)
            return;

        if (!batchActive)
        {
            batchActive = true;
            episodeCounter = 0;
        }

        Time.timeScale = Mathf.Clamp(timeScale, 0.5f, 10f);
        ConfigureCurrentEpisode();
    }

    private void Update()
    {
        if (!enableBatch || !batchActive || episodeFinishedHandled)
            return;

        // 교착 안전장치: 시간 제한을 넘긴 에피소드는 timeout으로 강제 종료
        if (episodeTimeLimit > 0f && Time.timeSinceLevelLoad >= episodeTimeLimit)
        {
            if (logger == null)
                logger = FindFirstObjectByType<RaidMetricsLogger>();

            if (logger != null)
                logger.FinishEpisode(false, "timeout");
        }
    }

    // 이번 에피소드의 알고리즘과 seed를 실험 셋업과 로거에 주입.
    // 로거의 Awake(StartEpisode)와 이 컴포넌트의 Awake는 실행 순서가 보장되지 않으므로
    // 주입 후 StartEpisode를 다시 호출하여 어느 순서로 실행되어도 같은 결과가 되도록 함
    private void ConfigureCurrentEpisode()
    {
        int algorithmCount = Mathf.Max(1, algorithms != null ? algorithms.Length : 0);
        totalEpisodes = episodesPerAlgorithm * algorithmCount;
        currentEpisodeNumber = episodeCounter + 1;

        int algorithmIndex = Mathf.Clamp(episodeCounter / episodesPerAlgorithm, 0, algorithmCount - 1);
        int runIndex = episodeCounter % episodesPerAlgorithm;

        NPCFSMMode algorithm = algorithms != null && algorithms.Length > 0
            ? algorithms[algorithmIndex]
            : NPCFSMMode.PatternAwareFSM;

        currentSeed = pairSeedsAcrossAlgorithms
            ? baseSeed + runIndex * seedStride
            : baseSeed + episodeCounter * seedStride;

        // 조건별 조율자 모드 결정 (목록이 짧거나 비어 있으면 None = 조율 없음)
        CoordinatorMode coordinatorMode = coordinatorModes != null && algorithmIndex < coordinatorModes.Length
            ? coordinatorModes[algorithmIndex]
            : CoordinatorMode.None;

        // 조건 라벨: 조율자가 있으면 "GATv2FSM+RuleCoord" 형태로 결과 CSV에서 구분
        currentAlgorithmLabel = coordinatorMode == CoordinatorMode.None
            ? algorithm.ToString()
            : algorithm.ToString() + "+" + coordinatorMode + "Coord";
        ActiveConditionLabel = currentAlgorithmLabel;

        // 실험 셋업에 알고리즘 적용 (스포너, 로거, 기존 NPC 일괄 반영)
        NPCPolicyExperimentSetup setup = FindFirstObjectByType<NPCPolicyExperimentSetup>();
        if (setup != null)
        {
            setup.aiMode = algorithm;
            setup.Apply();
        }

        // 씬의 조율자에 이번 조건의 모드 주입 (None이면 지시 발행 없이 기존 거동과 동일)
        RaidCoordinator coordinator = FindFirstObjectByType<RaidCoordinator>();
        if (coordinator != null)
            coordinator.mode = coordinatorMode;
        else if (coordinatorMode != CoordinatorMode.None)
            Debug.LogWarning("[ExperimentBatchRunner] 조율자 모드가 지정되었으나 씬에 RaidCoordinator가 없음", this);

        // 로거에 seed와 알고리즘 주입 후 에피소드 재시작 (Awake 순서 무관하게 수렴)
        logger = FindFirstObjectByType<RaidMetricsLogger>();
        if (logger != null)
        {
            logger.seed = currentSeed;
            logger.applySeedOnEpisodeStart = true;
            logger.algorithmName = currentAlgorithmLabel;
            logger.StartEpisode();
        }
        else
        {
            Debug.LogWarning("[ExperimentBatchRunner] RaidMetricsLogger를 찾지 못함. 지표가 기록되지 않음", this);
        }

        // 공정성 확인: 배치 실험은 자동 플레이어 사용이 전제
        if (ScriptedPlayerMode.Instance == null || !ScriptedPlayerMode.Instance.enableScriptedPlayer)
            Debug.LogWarning("[ExperimentBatchRunner] 자동 플레이어가 꺼져 있음. 사람 조작 변동이 결과에 섞임", this);

        if (logProgress)
            Debug.Log($"[ExperimentBatchRunner] 에피소드 {currentEpisodeNumber}/{totalEpisodes} 시작: {currentAlgorithmLabel}, seed {currentSeed}", this);
    }

    private void OnEpisodeFinished(RaidMetricsLogger finishedLogger, bool clearSuccess, string endReason)
    {
        if (!enableBatch || !batchActive || episodeFinishedHandled)
            return;

        // 씬 재시작 전 중복 처리 방지 (같은 씬에서 종료 이벤트가 다시 와도 무시)
        episodeFinishedHandled = true;

        if (logProgress)
            Debug.Log($"[ExperimentBatchRunner] 에피소드 {currentEpisodeNumber}/{totalEpisodes} 종료: {endReason}", this);

        episodeCounter++;

        int algorithmCount = Mathf.Max(1, algorithms != null ? algorithms.Length : 0);
        if (episodeCounter >= episodesPerAlgorithm * algorithmCount)
        {
            CompleteBatch();
            return;
        }

        StartCoroutine(RestartSceneRoutine());
    }

    private IEnumerator RestartSceneRoutine()
    {
        // timeScale과 무관한 실제 시간 대기
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelay));

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else
        {
            // Build Settings에 없는 씬은 이름으로 재시도 (실패 시 배치 중단 안내)
            Debug.LogWarning("[ExperimentBatchRunner] 씬이 Build Settings에 없음. File > Build Settings에 Raid 씬 추가 필요", this);
            SceneManager.LoadScene(activeScene.name);
        }
    }

    private void CompleteBatch()
    {
        batchActive = false;
        Time.timeScale = 1f;

        Debug.Log($"[ExperimentBatchRunner] 배치 완료: 총 {episodeCounter}회. CSV는 persistentDataPath에 저장됨");

        if (!stopWhenComplete)
            return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
