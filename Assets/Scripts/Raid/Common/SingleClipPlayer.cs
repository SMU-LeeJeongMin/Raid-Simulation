using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// PlayableGraph 기반 단일 AnimationClip 재생을 담당하는 공용 클래스.
/// 보스, 슬라임, 플레이어, NPC 컨트롤러에서 반복 구현되던 그래프 생성,
/// 클립 재생, 수동 루프 유지, 정지, 파괴 로직의 단일 구현.
/// </summary>
public class SingleClipPlayer
{
    private readonly string graphName;
    private readonly string outputName;

    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable playable;
    private AnimationClip currentClip;
    private bool loop;

    public SingleClipPlayer(string graphName, string outputName)
    {
        this.graphName = graphName;
        this.outputName = outputName;
    }

    // 현재 재생 중인 클립 (재생 중이 아니면 null)
    public AnimationClip CurrentClip => currentClip;

    public bool IsPlaying => currentClip != null && playable.IsValid();

    // 재생 속도를 직접 지정하여 클립 재생
    public void Play(Animator animator, AnimationClip clip, float speed = 1f, bool loop = false)
    {
        if (animator == null || clip == null)
            return;

        CreateGraphIfNeeded(animator);

        if (playable.IsValid())
            playable.Destroy();

        currentClip = clip;
        this.loop = loop;

        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetTime(0d);
        playable.SetSpeed(Mathf.Max(0.01f, speed));
        playable.SetDone(false);
        output.SetSourcePlayable(playable);
        graph.Play();
    }

    // 지정한 시간 동안 클립 전체가 재생되도록 속도를 환산하여 재생
    public void PlayTimed(Animator animator, AnimationClip clip, float duration, bool loop = false)
    {
        if (clip == null)
            return;

        float speed = duration <= 0.0001f ? 1f : clip.length / Mathf.Max(0.05f, duration);
        Play(animator, clip, speed, loop);
    }

    // 같은 클립이 같은 루프 설정으로 이미 재생 중이면 재시작하지 않음. 새로 재생했으면 true 반환
    public bool PlayIfChanged(Animator animator, AnimationClip clip, float speed, bool loop)
    {
        if (clip != null && currentClip == clip && this.loop == loop && graph.IsValid() && playable.IsValid())
            return false;

        Play(animator, clip, speed, loop);
        return true;
    }

    // 루프 클립의 수동 반복 유지 (소유 클래스의 Update에서 매 프레임 호출)
    public void Tick()
    {
        if (!loop || currentClip == null || !playable.IsValid())
            return;

        double length = currentClip.length;
        if (length <= 0.0001d)
            return;

        double time = playable.GetTime();
        if (time >= length)
        {
            playable.SetTime(time % length);
            playable.SetDone(false);
        }
    }

    // 클립이 끝 지점에 도달했으면 마지막 포즈로 고정 (사망 포즈 유지 용도)
    public void FreezeAtEndIfFinished()
    {
        if (currentClip == null || !playable.IsValid())
            return;

        if (playable.GetTime() >= currentClip.length)
            FreezeAtEnd();
    }

    // 즉시 마지막 포즈로 고정
    public void FreezeAtEnd()
    {
        if (currentClip == null || !playable.IsValid())
            return;

        loop = false;
        playable.SetTime(currentClip.length);
        playable.SetSpeed(0d);
        playable.SetDone(true);

        if (graph.IsValid())
            graph.Play();
    }

    // 재생 중지 (그래프는 유지하여 재사용)
    public void Stop()
    {
        currentClip = null;
        loop = false;

        if (playable.IsValid())
            playable.Destroy();

        if (graph.IsValid())
            graph.Stop();
    }

    // 그래프 파괴 (소유 클래스의 OnDestroy에서 호출 필수)
    public void Dispose()
    {
        currentClip = null;
        loop = false;

        if (graph.IsValid())
            graph.Destroy();
    }

    private void CreateGraphIfNeeded(Animator animator)
    {
        if (graph.IsValid())
            return;

        graph = PlayableGraph.Create(graphName);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, outputName, animator);
    }
}
