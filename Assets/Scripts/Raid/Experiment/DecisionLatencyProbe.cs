using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 의사결정 지연 계측기.
///
/// 목적: 장면 복잡도(엔티티 수)에 따른 판단 비용의 변화를 Utility와 GNN 사이에서 비교함.
/// Utility는 후보 행동마다 아군, 슬라임, 장판을 순회하므로 비용이 엔티티 수에 비례 증가하고,
/// GNN은 고정 크기(MAX_NODES) 순전파이므로 비용이 일정함. 두 곡선의 교차점이 관측 대상임.
///
/// 사용법
/// - 씬에 이 컴포넌트를 1개 배치 (RaidMetricsLogger와 같은 오브젝트 권장)
/// - Enable Probe를 켜고 짧게(5판 내외) 측정. 본실험에는 끌 것 (계측 자체가 비용임)
/// - 에피소드 종료 시 CSV에 누적 기록됨
///
/// 주의: Stopwatch는 게임 시간이 아닌 실제 시간을 측정하므로 Time Scale을 1로 두고 측정해야 함.
/// </summary>
public class DecisionLatencyProbe : MonoBehaviour
{
    [Header("Probe")]
    [Tooltip("계측 활성화 여부. 본실험에서는 반드시 끌 것")]
    public bool enableProbe = true;
    [Tooltip("표본 상한. 초과분은 버려짐 (메모리 보호)")]
    [Min(1000)] public int maxSamples = 400000;

    [Header("Output")]
    public string fileName = "decision_latency.csv";
    [Tooltip("비우면 persistentDataPath에 저장. 다른 위치를 쓰려면 절대 경로 입력")]
    public string outputRootOverride = "";
    public bool logPathOnStart = true;

    [Header("Runtime Debug")]
    [SerializeField] private int sampleCount;
    [SerializeField] private int writtenCount;
    [SerializeField] private string outputPath;

    // 계측 종류
    public const string KindDecision = "decision";          // NPC 판단 1회 전체 (정책 무관 공통 경로 포함)
    public const string KindUtility = "utility_advise";     // Utility 점수 계산 1회
    public const string KindSnapshot = "graph_snapshot";     // 그래프 스냅샷 생성 1회 (틱당 1회, 파티 공유)
    public const string KindEncode = "gnn_encode";           // 텐서 인코딩 1회
    public const string KindInfer = "gnn_infer";             // 추론과 결과 회수 1회

    private struct Sample
    {
        public string kind;
        public string tag;
        public int entities;
        public int characters;
        public int slimes;
        public int zones;
        public int nodes;
        public int edges;
        public double micro;
        public float t;
    }

    private static DecisionLatencyProbe instance;
    private static bool active;                  // 정적 진입점의 조기 반환 판정용
    private static int cachedEntities;
    private static int cachedCharacters;
    private static int cachedSlimes;
    private static int cachedZones;
    private static int cachedFrame = -1;

    private readonly List<Sample> samples = new List<Sample>();
    private RaidMetricsLogger metricsLogger;

    private void Awake()
    {
        instance = this;
        active = enableProbe;

        string root = string.IsNullOrWhiteSpace(outputRootOverride)
            ? Application.persistentDataPath : outputRootOverride;
        outputPath = Path.Combine(root, string.IsNullOrWhiteSpace(fileName) ? "decision_latency.csv" : fileName);

        if (logPathOnStart)
            Debug.Log($"[DecisionLatencyProbe] 계측 {(active ? "켜짐" : "꺼짐")}, 출력: {outputPath}", this);
    }

    private void OnEnable()
    {
        RaidMetricsLogger.EpisodeFinished += OnEpisodeFinished;
    }

    private void OnDisable()
    {
        RaidMetricsLogger.EpisodeFinished -= OnEpisodeFinished;
        Flush();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            active = false;
        }
    }

    // ---------- 정적 진입점 ----------

    /// <summary>계측 시작. 꺼져 있으면 0을 반환하며 End에서 무시됨</summary>
    public static long Begin()
    {
        return active ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
    }

    /// <summary>계측 종료와 표본 적립. nodes와 edges는 그래프 경로에서만 채움</summary>
    public static void End(string kind, long startTimestamp, string tag = null, int nodes = 0, int edges = 0)
    {
        if (!active || startTimestamp == 0L || instance == null)
            return;

        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
        double micro = elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;

        if (instance.samples.Count >= instance.maxSamples)
            return;

        RefreshEntityCounts();

        instance.samples.Add(new Sample
        {
            kind = kind,
            tag = string.IsNullOrEmpty(tag) ? "" : tag,
            entities = cachedEntities,
            characters = cachedCharacters,
            slimes = cachedSlimes,
            zones = cachedZones,
            nodes = nodes,
            edges = edges,
            micro = micro,
            t = Time.time,
        });
        instance.sampleCount = instance.samples.Count;
    }

    // 엔티티 수는 프레임당 1회만 계산 (계측 자체의 비용 억제)
    private static void RefreshEntityCounts()
    {
        if (Time.frameCount == cachedFrame)
            return;

        cachedFrame = Time.frameCount;

        var statuses = CombatRegistry.PlayerStatuses;
        int characters = 0;
        for (int i = 0; i < statuses.Count; i++)
        {
            if (statuses[i] != null)
                characters++;
        }

        var slimeList = CombatRegistry.Slimes;
        int slimes = 0;
        for (int i = 0; i < slimeList.Count; i++)
        {
            if (slimeList[i] != null && slimeList[i].IsAlive)
                slimes++;
        }

        var zoneList = DangerZoneRegistry.GetActiveZones();
        int zones = 0;
        for (int i = 0; i < zoneList.Count; i++)
        {
            if (zoneList[i] != null && zoneList[i].isActiveAndEnabled)
                zones++;
        }

        int boss = CombatRegistry.FirstBossDummy != null ? 1 : 0;

        cachedCharacters = characters;
        cachedSlimes = slimes;
        cachedZones = zones;
        cachedEntities = characters + boss + slimes + zones;
    }

    // ---------- 기록 ----------

    private void OnEpisodeFinished(RaidMetricsLogger logger, bool clearSuccess, string endReason)
    {
        metricsLogger = logger;
        Flush();
    }

    private void Flush()
    {
        if (samples.Count == 0)
            return;

        if (metricsLogger == null)
            metricsLogger = FindFirstObjectByType<RaidMetricsLogger>();

        string episodeId = metricsLogger != null ? metricsLogger.episodeId : "";
        string algorithm = metricsLogger != null ? metricsLogger.algorithmName : "";

        try
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            FileInfo info = new FileInfo(outputPath);
            bool needHeader = !info.Exists || info.Length == 0;

            StringBuilder builder = new StringBuilder();
            if (needHeader)
                builder.AppendLine("episode_id,algorithm,kind,tag,entities,characters,slimes,zones,nodes,edges,micro,t");

            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                builder.Append(Escape(episodeId)).Append(',')
                    .Append(Escape(algorithm)).Append(',')
                    .Append(s.kind).Append(',')
                    .Append(Escape(s.tag)).Append(',')
                    .Append(s.entities).Append(',')
                    .Append(s.characters).Append(',')
                    .Append(s.slimes).Append(',')
                    .Append(s.zones).Append(',')
                    .Append(s.nodes).Append(',')
                    .Append(s.edges).Append(',')
                    .Append(s.micro.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.t.ToString("F3", CultureInfo.InvariantCulture))
                    .AppendLine();
            }

            File.AppendAllText(outputPath, builder.ToString());
            writtenCount += samples.Count;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[DecisionLatencyProbe] 기록 실패: {exception.Message}", this);
        }

        samples.Clear();
        sampleCount = 0;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
