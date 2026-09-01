using System;
using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// 모델별 그래프 입력 형식 (매니페스트의 graph_input과 대응).
/// </summary>
public enum GnnGraphInput
{
    WeightedUndirected,     // GCN: 무방향 근접 가중 인접
    Relational,             // RGCN: 관계별 방향 인접 [R, N, N]
    DirectedEdgeFeatures    // GATv2: 방향 인접 + 엣지 특징 [N, N, 20]
}

/// <summary>
/// GNN ONNX 모델의 Sentis 추론 실행기 (8-A).
/// 여러 모델(GCN, RGCN, GAT)을 슬롯으로 등록하여 같은 배치 실험 안에서 조건을 전환 가능.
///
/// 전술 틱마다 그래프 스냅샷 1회를 캡처하고, 요청된 모델만 지연 추론하여
/// 파티 전체가 결과를 공유 (틱당 모델별 추론 1회).
///
/// 사용 전제:
/// - Package Manager에서 Sentis(Inference Engine) 설치
/// - Colab(7단계)에서 dynamo=False로 내보낸 단일 파일 onnx와 매니페스트를 Assets에 임포트
/// - 슬롯마다 mode(NPCFSMMode), modelAsset, manifestAsset 지정
/// </summary>
public class GnnModelRunner : MonoBehaviour
{
    [Serializable]
    public class ModelSlotConfig
    {
        [Tooltip("이 모델이 담당하는 알고리즘 조건")]
        public NPCFSMMode mode = NPCFSMMode.GCN;
        public Unity.InferenceEngine.ModelAsset modelAsset;
        [Tooltip("Colab이 생성한 매니페스트 JSON. 연결하면 그래프 입력 형식과 특징 정의를 자동 검증")]
        public TextAsset manifestAsset;
        [Tooltip("매니페스트 미연결 시에만 사용되는 수동 지정 값")]
        public GnnGraphInput graphInput = GnnGraphInput.WeightedUndirected;
    }

    [Header("Models")]
    public ModelSlotConfig[] models = new ModelSlotConfig[0];

    [Header("Inference")]
    [Tooltip("소형 모델은 CPU 백엔드가 GPU 왕복 지연 없이 가장 빠름")]
    public Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU;
    [Tooltip("추론 갱신 간격 (초). NPC 판단 틱(0.25초)보다 짧거나 같게")]
    [Min(0.05f)] public float inferenceInterval = 0.2f;

    [Header("Runtime Debug")]
    [SerializeField] private int readyModelCount;
    [SerializeField] private int snapshotVersion;
    [SerializeField] private int inferenceCount;

    public const int MaxNodes = 40;

    public static GnnModelRunner Instance { get; private set; }

    // 슬롯 1개의 런타임 상태
    private class ModelSlot
    {
        public NPCFSMMode mode;
        public GnnGraphInput graphInput;
        public Unity.InferenceEngine.Worker worker;

        public float[] featureBuffer;
        public float[] adjacencyBuffer;
        public float[] relationalBuffer;
        public float[] edgeBuffer;

        public float[] cachedLogits;
        public int inferredSnapshotVersion = -1;
    }

    private readonly List<ModelSlot> slots = new List<ModelSlot>();
    private readonly Dictionary<PlayerStatus, int> characterIndexMap = new Dictionary<PlayerStatus, int>();
    private GraphSnapshot cachedSnapshot;
    private float lastSnapshotTime = float.NegativeInfinity;

    public bool IsReady => slots.Count > 0;

    private void Awake()
    {
        Instance = this;

        if (models == null || models.Length == 0)
        {
            Debug.LogError("[GnnModelRunner] 등록된 모델 없음. GNN 조건이 동작하지 않음", this);
            return;
        }

        for (int i = 0; i < models.Length; i++)
            TryCreateSlot(models[i]);

        readyModelCount = slots.Count;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].worker?.Dispose();

        slots.Clear();

        if (Instance == this)
            Instance = null;
    }

    private void TryCreateSlot(ModelSlotConfig config)
    {
        if (config == null || config.modelAsset == null)
        {
            Debug.LogWarning("[GnnModelRunner] modelAsset이 비어 있는 슬롯 건너뜀", this);
            return;
        }

        GnnGraphInput graphInput = config.graphInput;
        if (!ValidateManifest(config, ref graphInput))
            return;

        try
        {
            Unity.InferenceEngine.Model model = Unity.InferenceEngine.ModelLoader.Load(config.modelAsset);

            ModelSlot slot = new ModelSlot
            {
                mode = config.mode,
                graphInput = graphInput,
                worker = new Unity.InferenceEngine.Worker(model, backend),
                featureBuffer = new float[MaxNodes * GnnFeatureEncoder.FeatureDim],
            };

            if (graphInput == GnnGraphInput.Relational)
                slot.relationalBuffer = new float[GnnSchema.RelationCount * MaxNodes * MaxNodes];
            else
                slot.adjacencyBuffer = new float[MaxNodes * MaxNodes];

            if (graphInput == GnnGraphInput.DirectedEdgeFeatures)
                slot.edgeBuffer = new float[MaxNodes * MaxNodes * GnnFeatureEncoder.EdgeFeatureDim];

            slots.Add(slot);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GnnModelRunner] 모델 로드 실패 ({config.mode}): {exception.Message}", this);
        }
    }

    // 매니페스트와 인코더 정의 대조 및 그래프 입력 형식 자동 판별
    private bool ValidateManifest(ModelSlotConfig config, ref GnnGraphInput graphInput)
    {
        if (config.manifestAsset == null)
            return true;

        string text = config.manifestAsset.text;

        if (text.Contains("\"graph_input\": \"weighted_undirected\""))
            graphInput = GnnGraphInput.WeightedUndirected;
        else if (text.Contains("\"graph_input\": \"relational\""))
            graphInput = GnnGraphInput.Relational;
        else if (text.Contains("\"graph_input\": \"directed_edge_features\""))
            graphInput = GnnGraphInput.DirectedEdgeFeatures;

        bool ok = text.Contains($"\"feature_dim\": {GnnFeatureEncoder.FeatureDim}")
            && text.Contains($"\"max_nodes\": {MaxNodes}")
            && text.Contains($"\"num_actions\": {GnnFeatureEncoder.NumActions}");

        for (int i = 0; ok && i < GnnFeatureEncoder.ScalarFeatureNames.Length; i++)
            ok = text.Contains($"\"{GnnFeatureEncoder.ScalarFeatureNames[i]}\"");

        if (graphInput == GnnGraphInput.DirectedEdgeFeatures)
            ok = ok && text.Contains($"\"edge_feature_dim\": {GnnFeatureEncoder.EdgeFeatureDim}");

        if (!ok)
            Debug.LogError($"[GnnModelRunner] 매니페스트와 인코더 정의 불일치 ({config.mode}). 모델과 게임 코드 버전 확인 필요", this);

        return ok;
    }

    // 자기 노드의 행동 logits 조회 (틱 내 첫 호출자가 해당 모델의 추론을 갱신)
    public bool TryGetLogits(PlayerStatus status, NPCFSMMode mode, float[] logitsOut)
    {
        if (status == null || logitsOut == null || logitsOut.Length < GnnFeatureEncoder.NumActions)
            return false;

        ModelSlot slot = FindSlot(mode);
        if (slot == null)
            return false;

        EnsureSnapshot();
        if (cachedSnapshot == null)
            return false;

        if (slot.inferredSnapshotVersion != snapshotVersion)
            RunInference(slot);

        if (slot.cachedLogits == null || !characterIndexMap.TryGetValue(status, out int nodeIndex))
            return false;

        int offset = nodeIndex * GnnFeatureEncoder.NumActions;
        if (offset + GnnFeatureEncoder.NumActions > slot.cachedLogits.Length)
            return false;

        for (int i = 0; i < GnnFeatureEncoder.NumActions; i++)
            logitsOut[i] = slot.cachedLogits[offset + i];

        return true;
    }

    public bool HasModelFor(NPCFSMMode mode)
    {
        return FindSlot(mode) != null;
    }

    private ModelSlot FindSlot(NPCFSMMode mode)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].mode == mode)
                return slots[i];
        }

        return null;
    }

    // 스냅샷은 틱당 1회만 생성하여 모든 모델이 공유
    private void EnsureSnapshot()
    {
        if (cachedSnapshot != null && Time.time - lastSnapshotTime < inferenceInterval)
            return;

        lastSnapshotTime = Time.time;

        // 상한을 넘는 상황(장판 다수 등)에서도 중요 노드만 남겨 최신 그래프로 추론
        GraphSnapshot snapshot = GraphSnapshotBuilder.Build(string.Empty, string.Empty, 0, 0f, characterIndexMap, MaxNodes);
        if (snapshot.nodes.Count == 0)
            return;

        cachedSnapshot = snapshot;
        snapshotVersion++;
    }

    private void RunInference(ModelSlot slot)
    {
        slot.inferredSnapshotVersion = snapshotVersion;

        Unity.InferenceEngine.Tensor<float> graphTensor = null;
        Unity.InferenceEngine.Tensor<float> edgeTensor = null;

        try
        {
            using (Unity.InferenceEngine.Tensor<float> features = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, MaxNodes, GnnFeatureEncoder.FeatureDim), EncodeFor(slot)))
            {
                slot.worker.SetInput("node_features", features);

                if (slot.graphInput == GnnGraphInput.Relational)
                {
                    graphTensor = new Unity.InferenceEngine.Tensor<float>(
                        new Unity.InferenceEngine.TensorShape(1, GnnSchema.RelationCount, MaxNodes, MaxNodes), slot.relationalBuffer);
                    slot.worker.SetInput("adjacency_rel", graphTensor);
                }
                else
                {
                    graphTensor = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, MaxNodes, MaxNodes), slot.adjacencyBuffer);
                    slot.worker.SetInput("adjacency", graphTensor);
                }

                if (slot.graphInput == GnnGraphInput.DirectedEdgeFeatures)
                {
                    edgeTensor = new Unity.InferenceEngine.Tensor<float>(
                        new Unity.InferenceEngine.TensorShape(1, MaxNodes, MaxNodes, GnnFeatureEncoder.EdgeFeatureDim), slot.edgeBuffer);
                    slot.worker.SetInput("edge_features", edgeTensor);
                }

                slot.worker.Schedule();

                using (Unity.InferenceEngine.Tensor<float> output = (slot.worker.PeekOutput("action_logits") as Unity.InferenceEngine.Tensor<float>).ReadbackAndClone())
                {
                    slot.cachedLogits = output.DownloadToArray();
                }
            }

            inferenceCount++;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GnnModelRunner] 추론 실패 ({slot.mode}): {exception.Message}", this);
            slot.cachedLogits = null;
        }
        finally
        {
            graphTensor?.Dispose();
            edgeTensor?.Dispose();
        }
    }

    // 슬롯의 그래프 입력 형식에 맞게 버퍼를 채우고 노드 특징 버퍼 반환
    private float[] EncodeFor(ModelSlot slot)
    {
        switch (slot.graphInput)
        {
            case GnnGraphInput.Relational:
                GnnFeatureEncoder.EncodeRelational(cachedSnapshot, MaxNodes, slot.featureBuffer, slot.relationalBuffer);
                break;

            case GnnGraphInput.DirectedEdgeFeatures:
                GnnFeatureEncoder.EncodeDirectedWithEdgeFeatures(cachedSnapshot, MaxNodes,
                    slot.featureBuffer, slot.adjacencyBuffer, slot.edgeBuffer);
                break;

            default:
                GnnFeatureEncoder.EncodeWeightedUndirected(cachedSnapshot, MaxNodes, slot.featureBuffer, slot.adjacencyBuffer);
                break;
        }

        return slot.featureBuffer;
    }
}
