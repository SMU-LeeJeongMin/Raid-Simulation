// 선택 화면에 배치된 캐릭터 1명을 제어하는 스크립트
// Rest/Selected 애니메이션 재생, 클릭용 Collider 생성, 위치 고정을 담당

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class CharacterLineupActor : MonoBehaviour
{
    public int Index { get; private set; } = -1;
    public CharacterLineupEntry Entry { get; private set; }

    [Header("Runtime Debug")]
    [SerializeField] private string currentAnimationLabel;
    [SerializeField] private bool selected;
    [SerializeField] private bool usingDirectClipPlayback;

    private Animator animator;

    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable clipPlayable;
    private AnimationClip currentClip;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;

    // 캐릭터가 생성된 직후 호출
    // DB 데이터를 연결하고, Animator/초기 Transform을 저장한 뒤 Rest 상태로 시작
    public void Initialize(int index, CharacterLineupEntry entry)
    {
        Index = index;
        Entry = entry;
        animator = GetComponentInChildren<Animator>(true);

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;

        if (animator != null && Entry != null && Entry.disableRootMotionForPreview)
            animator.applyRootMotion = false;

        EnsureClickableCollider();
        PlayRest();
    }

    // 직접 재생 중인 AnimationClip이 끝났을 때 다시 처음부터 반복
    private void Update()
    {
        KeepClipLoopingIfNeeded();
    }

    // 선택 화면에서 처음 위치/회전/크기를 유지
    private void LateUpdate()
    {
        if (Entry != null && Entry.lockLineupTransform)
        {
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
        }
    }

    // 선택되지 않은 기본 대기 상태를 재생
    public void PlayRest()
    {
        selected = false;

        if (Entry == null)
            return;

        if (Entry.useDirectClipPlayback && Entry.restClip != null)
            PlayClip(Entry.restClip, "Rest Clip");
        else
            PlayAnimatorState(Entry.restStateName, "Rest State");
    }

    // 캐릭터가 선택되었을 때의 애니메이션을 재생
    public void PlaySelected()
    {
        selected = true;

        if (Entry == null)
            return;

        if (Entry.useDirectClipPlayback && Entry.selectedClip != null)
            PlayClip(Entry.selectedClip, "Selected Clip");
        else
            PlayAnimatorState(Entry.selectedStateName, "Selected State");
    }

    // AnimationClip을 직접 재생
    private void PlayClip(AnimationClip clip, string label)
    {
        if (animator == null || clip == null)
            return;

        animator.enabled = true;

        if (Entry != null && Entry.disableRootMotionForPreview)
            animator.applyRootMotion = false;

        CreateGraphIfNeeded();

        if (clipPlayable.IsValid())
            clipPlayable.Destroy();

        currentClip = clip;
        clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        clipPlayable.SetTime(0d);
        clipPlayable.SetSpeed(1d);
        clipPlayable.SetDone(false);

        output.SetSourcePlayable(clipPlayable);
        graph.Play();

        currentAnimationLabel = label + " : " + clip.name;
        usingDirectClipPlayback = true;
    }

    // AnimationClip이 비어 있을 때 사용하는 예비 재생 방식
    private void PlayAnimatorState(string stateName, string label)
    {
        StopDirectClipPlayback();

        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.enabled = true;

        if (Entry != null && Entry.disableRootMotionForPreview)
            animator.applyRootMotion = false;

        string playableName = ResolvePlayableStateName(stateName);
        animator.Play(playableName, 0, 0f);
        animator.Update(0f);

        currentAnimationLabel = label + " : " + stateName;
        usingDirectClipPlayback = false;
    }

    // Animator state 이름이 짧은 이름인지, Base Layer 경로가 필요한지 확인
    private string ResolvePlayableStateName(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return stateName;

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, shortHash))
            return stateName;

        string baseLayerPath = "Base Layer." + stateName;
        int fullHash = Animator.StringToHash(baseLayerPath);
        if (animator.HasState(0, fullHash))
            return baseLayerPath;

        Debug.LogWarning($"[CharacterLineupActor] Animator state was not found on '{name}': '{stateName}'. " +
                         "If the character jitters or switches to another animation, assign Rest Clip / Selected Clip in DB_CharacterLineup and keep Use Direct Clip Playback enabled.", this);
        return stateName;
    }

    // AnimationClip 직접 재생에 필요한 PlayableGraph를 처음 한 번 생성
    private void CreateGraphIfNeeded()
    {
        if (graph.IsValid())
            return;

        graph = PlayableGraph.Create(name + "_CharacterSelectPreviewGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "Animation", animator);
    }

    // 직접 Clip 재생을 중지
    private void StopDirectClipPlayback()
    {
        currentClip = null;

        if (clipPlayable.IsValid())
            clipPlayable.Destroy();

        if (graph.IsValid())
            graph.Stop();
    }

    // Clip의 Loop 설정과 관계없이 선택 화면에서 계속 반복 재생
    private void KeepClipLoopingIfNeeded()
    {
        if (Entry == null || !Entry.loopPreviewClips)
            return;

        if (!graph.IsValid() || !clipPlayable.IsValid() || currentClip == null)
            return;

        double length = currentClip.length;
        if (length <= 0.0001d)
            return;

        double time = clipPlayable.GetTime();
        if (time >= length)
        {
            clipPlayable.SetTime(time % length);
            clipPlayable.SetDone(false);
        }
    }

    // 캐릭터 프리팹에 Collider가 없으면 클릭 감지를 위해 CapsuleCollider를 자동으로 추가
    private void EnsureClickableCollider()
    {
        if (Entry == null || !Entry.addClickColliderIfMissing)
            return;

        Collider[] existingColliders = GetComponentsInChildren<Collider>(true);
        if (existingColliders != null && existingColliders.Length > 0)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.center = Entry.clickColliderCenter;
        capsule.height = Entry.clickColliderHeight;
        capsule.radius = Entry.clickColliderRadius;
    }

    private void OnDisable()
    {
        DestroyPlayableGraph();
    }

    private void OnDestroy()
    {
        DestroyPlayableGraph();
    }

    // 오브젝트가 사라질 때 PlayableGraph를 정리해서 메모리 누수 방지
    private void DestroyPlayableGraph()
    {
        if (graph.IsValid())
            graph.Destroy();

        currentClip = null;
    }
}
