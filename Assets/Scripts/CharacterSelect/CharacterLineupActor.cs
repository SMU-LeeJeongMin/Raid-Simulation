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

    private void Update()
    {
        KeepClipLoopingIfNeeded();
    }

    private void LateUpdate()
    {
        if (Entry != null && Entry.lockLineupTransform)
        {
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
        }
    }

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

    private void CreateGraphIfNeeded()
    {
        if (graph.IsValid())
            return;

        graph = PlayableGraph.Create(name + "_CharacterSelectPreviewGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "Animation", animator);
    }

    private void StopDirectClipPlayback()
    {
        currentClip = null;

        if (clipPlayable.IsValid())
            clipPlayable.Destroy();

        if (graph.IsValid())
            graph.Stop();
    }

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

    private void DestroyPlayableGraph()
    {
        if (graph.IsValid())
            graph.Destroy();

        currentClip = null;
    }
}
