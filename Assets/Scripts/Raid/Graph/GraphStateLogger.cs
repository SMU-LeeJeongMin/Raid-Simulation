using System.IO;
using UnityEngine;

/// <summary>
/// 그래프 스냅샷 JSONL 기록기 (5단계).
/// 스냅샷 간격마다 GraphSnapshotBuilder로 현재 전투 상태를 캡처하여
/// 에피소드별 .jsonl 파일에 1줄씩 기록. GNN 학습 데이터 수집용.
///
/// 에피소드 식별과 알고리즘 이름은 RaidMetricsLogger를 따라가므로
/// 배치 실험(ExperimentBatchRunner)과 함께 사용하면 CSV 행과 그래프 로그가
/// episode_id로 조인 가능.
///
/// Coordinator(8-B)는 파일이 아니라 CaptureSnapshot()을 직접 호출하여
/// 같은 빌더의 실시간 상태를 사용.
/// </summary>
public class GraphStateLogger : MonoBehaviour
{
    [Header("Logging")]
    public bool enableLogging = true;
    [Tooltip("스냅샷 기록 간격 (초). NPC 판단 틱(0.25초)과 같은 값 권장")]
    [Min(0.05f)] public float snapshotInterval = 0.25f;
    [Tooltip("전투 중(에피소드 진행 중)에만 기록할지 여부")]
    public bool logOnlyDuringEpisode = true;
    public bool logPathOnStart = true;

    [Header("Output")]
    [Tooltip("저장 루트 절대 경로 (예: D:\\Raid Simulation). 비우면 기본 persistentDataPath 사용")]
    public string customOutputRoot = "";
    [Tooltip("저장 루트 아래 폴더 이름")]
    public string outputFolderName = "GraphLogs";
    [Tooltip("스냅샷 노드 수 상한. 학습 모델의 고정 입력 크기와 같은 값을 사용해야 수집 데이터와 추론 조건이 일치")]
    [Min(8)] public int maxNodes = 40;

    [Header("Runtime Debug")]
    [SerializeField] private int writtenSnapshotCount;
    [SerializeField] private string currentFilePath;

    private RaidMetricsLogger metricsLogger;
    private StreamWriter writer;
    private string writerEpisodeId;
    private float nextSnapshotTime;
    private float episodeStartTime;
    private int tickCounter;

    private void Awake()
    {
        if (logPathOnStart)
            Debug.Log($"[GraphStateLogger] Output folder: {Path.Combine(ResolveOutputRoot(), outputFolderName)}", this);
    }

    private void OnEnable()
    {
        RaidMetricsLogger.EpisodeFinished += OnEpisodeFinished;
    }

    private void OnDisable()
    {
        RaidMetricsLogger.EpisodeFinished -= OnEpisodeFinished;
        CloseWriter();
    }

    private void OnApplicationQuit()
    {
        CloseWriter();
    }

    private void Update()
    {
        if (!enableLogging || Time.time < nextSnapshotTime)
            return;

        nextSnapshotTime = Time.time + Mathf.Max(0.05f, snapshotInterval);

        if (metricsLogger == null)
        {
            metricsLogger = FindFirstObjectByType<RaidMetricsLogger>();
            if (metricsLogger == null)
                return;
        }

        string episodeId = metricsLogger.episodeId;
        if (string.IsNullOrWhiteSpace(episodeId))
            return;

        // 에피소드가 바뀌면 새 파일 시작
        if (writer == null || writerEpisodeId != episodeId)
            OpenWriterForEpisode(episodeId, metricsLogger.algorithmName);

        if (writer == null)
            return;

        GraphSnapshot snapshot = GraphSnapshotBuilder.Build(
            episodeId, metricsLogger.algorithmName, tickCounter, Time.time - episodeStartTime, null, maxNodes);

        // 전투 개체가 없으면(로딩 직후 등) 기록 생략
        if (snapshot.nodes.Count == 0)
            return;

        writer.WriteLine(JsonUtility.ToJson(snapshot));
        tickCounter++;
        writtenSnapshotCount++;
    }

    // Coordinator와 조언 UI가 사용할 실시간 조회 진입점 (파일 기록과 무관하게 호출 가능)
    public GraphSnapshot CaptureSnapshot()
    {
        string episodeId = metricsLogger != null ? metricsLogger.episodeId : string.Empty;
        string algorithm = metricsLogger != null ? metricsLogger.algorithmName : string.Empty;
        return GraphSnapshotBuilder.Build(episodeId, algorithm, tickCounter, Time.time - episodeStartTime, null, maxNodes);
    }

    // 저장 루트 결정: 지정 경로가 있으면 생성 후 사용, 실패 시 기본 경로로 폴백
    private string ResolveOutputRoot()
    {
        if (string.IsNullOrWhiteSpace(customOutputRoot))
            return Application.persistentDataPath;

        try
        {
            Directory.CreateDirectory(customOutputRoot);
            return customOutputRoot;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GraphStateLogger] 지정 저장 경로 사용 불가, 기본 경로로 폴백: {customOutputRoot} ({e.Message})", this);
            return Application.persistentDataPath;
        }
    }

    private void OpenWriterForEpisode(string episodeId, string algorithm)
    {
        CloseWriter();

        try
        {
            string folder = Path.Combine(ResolveOutputRoot(), outputFolderName);
            Directory.CreateDirectory(folder);

            string safeAlgorithm = string.IsNullOrWhiteSpace(algorithm) ? "unknown" : algorithm;
            currentFilePath = Path.Combine(folder, $"graph_{episodeId}_{safeAlgorithm}.jsonl");

            writer = new StreamWriter(currentFilePath, append: true);
            writer.AutoFlush = false;

            writerEpisodeId = episodeId;
            episodeStartTime = Time.time;
            tickCounter = 0;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[GraphStateLogger] 로그 파일 열기 실패: {exception.Message}", this);
            writer = null;
        }
    }

    private void OnEpisodeFinished(RaidMetricsLogger logger, bool clearSuccess, string endReason)
    {
        // 에피소드 종료 시 즉시 저장 (배치의 씬 재시작 전에 유실 방지)
        CloseWriter();
    }

    private void CloseWriter()
    {
        if (writer == null)
            return;

        try
        {
            writer.Flush();
            writer.Dispose();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[GraphStateLogger] 로그 파일 닫기 실패: {exception.Message}", this);
        }

        writer = null;
        writerEpisodeId = null;
    }
}
