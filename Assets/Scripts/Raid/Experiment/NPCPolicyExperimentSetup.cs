using System.Collections;
using UnityEngine;

/// <summary>
/// 같은 씬에서 Basic FSM과 Pattern-aware FSM을 쉽게 바꿔 테스트하기 위한 설정 스크립트입니다.
/// RaidGameManager에 붙이고 aiMode만 바꾸면 NPCPartySpawner와 MetricsLogger에 같은 알고리즘 이름을 적용합니다.
/// </summary>
public class NPCPolicyExperimentSetup : MonoBehaviour
{
    public NPCFSMMode aiMode = NPCFSMMode.PatternAwareFSM;
    public NPCPartySpawner partySpawner;
    public RaidMetricsLogger metricsLogger;
    public bool applyOnAwake = true;
    public bool reapplyAfterSpawn = true;
    public float reapplyDelay = 0.5f;

    private void Awake()
    {
        if (applyOnAwake)
            Apply();
    }

    private void Start()
    {
        if (reapplyAfterSpawn)
            StartCoroutine(ReapplyRoutine());
    }

    [ContextMenu("Apply AI Mode")]
    public void Apply()
    {
        if (partySpawner == null)
            partySpawner = FindFirstObjectByType<NPCPartySpawner>();

        if (metricsLogger == null)
            metricsLogger = FindFirstObjectByType<RaidMetricsLogger>();

        if (partySpawner != null)
            partySpawner.defaultAIMode = aiMode;

        if (metricsLogger != null)
            metricsLogger.algorithmName = aiMode.ToString();

        NPCSimpleFSMController[] controllers = FindObjectsByType<NPCSimpleFSMController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].aiMode = aiMode;
        }
    }

    private IEnumerator ReapplyRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, reapplyDelay));
        Apply();
    }
}
