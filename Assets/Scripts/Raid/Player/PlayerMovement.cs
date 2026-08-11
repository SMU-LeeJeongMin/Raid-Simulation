// 플레이어 이동 (WASD)

using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private enum MotionAnimationState
    {
        None,
        Idle,
        Run
    }

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 720f;
    public float gravity = -25f;
    public float maxFallSpeed = -35f;
    public float groundedStickVelocity = -3f;
    public bool useCameraRelativeMovement = true;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    [Header("Controller Setup")]
    public bool setupCharacterControllerOnAwake = true;
    public Vector3 controllerCenter = new Vector3(0f, 0.6f, 0f);
    public float controllerHeight = 1.2f;
    public float controllerRadius = 0.28f;

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;
    public float groundExtraDistance = 0.18f;
    public bool useGroundProbeFallback = true;

    [Header("Animator / Physics Safety")]
    public bool disableAnimatorRootMotion = true;
    public bool makeRigidbodiesKinematic = true;

    [Header("Animator Parameters - Optional")]
    public string speedFloatParameter = "Speed";
    public string movingBoolParameter = "IsMoving";

    [Header("Movement Animation")]
    public bool useDirectClipPlayback = true;
    public AnimationClip idleClip;
    public AnimationClip runClip;
    public string idleStateName = "IdleA";
    public string runStateName = "Run";
    public float runClipSpeed = 1f;
    public float idleClipSpeed = 1f;
    public bool loopMovementClips = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool externalMovementLock;
    [SerializeField] private bool animationSuppressed;
    [SerializeField] private MotionAnimationState currentMotionAnimation;

    private CharacterController characterController;
    private float verticalVelocity;
    private readonly HashSet<string> animatorParameterNames = new HashSet<string>();

    // 이동 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer movementClipPlayer;
    private SingleClipPlayer MovementClipPlayer => movementClipPlayer ??= new SingleClipPlayer(name + "_MovementGraph", "Movement");

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (setupCharacterControllerOnAwake)
            ApplyCharacterControllerSettings();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (disableAnimatorRootMotion && animator != null)
            animator.applyRootMotion = false;

        if (makeRigidbodiesKinematic)
            DisableRigidbodyPhysics();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        CacheAnimatorParameters();
    }

    private void Start()
    {
        UpdateMovementAnimation(0f);
    }

    private void OnEnable()
    {
        currentMotionAnimation = MotionAnimationState.None;
    }

    private void Update()
    {
        Vector2 input = externalMovementLock ? Vector2.zero : ReadMoveInput();
        Vector3 moveDirection = BuildMoveDirection(input);
        float inputMagnitude = Mathf.Clamp01(input.magnitude);

        Move(moveDirection, inputMagnitude);
        Rotate(moveDirection);
        UpdateAnimatorParameters(inputMagnitude);
        UpdateMovementAnimation(inputMagnitude);
        KeepMovementClipLoopingIfNeeded();
    }

    // 스폰 직후 카메라, CharacterController, GroundMask 값을 외부에서 설정
    public void ConfigureRuntime(
        Transform newCameraTransform,
        Vector3 newControllerCenter,
        float newControllerHeight,
        float newControllerRadius,
        LayerMask newGroundMask)
    {
        cameraTransform = newCameraTransform;
        controllerCenter = newControllerCenter;
        controllerHeight = newControllerHeight;
        controllerRadius = newControllerRadius;
        groundMask = newGroundMask;

        ApplyCharacterControllerSettings();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (disableAnimatorRootMotion && animator != null)
            animator.applyRootMotion = false;

        if (makeRigidbodiesKinematic)
            DisableRigidbodyPhysics();

        CacheAnimatorParameters();
    }

    public void ApplyCharacterControllerSettings()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (characterController == null)
            return;

        characterController.center = controllerCenter;
        characterController.height = Mathf.Max(0.1f, controllerHeight);
        characterController.radius = Mathf.Max(0.01f, controllerRadius);
        characterController.stepOffset = Mathf.Min(0.3f, characterController.height * 0.25f);
        characterController.skinWidth = Mathf.Max(0.02f, characterController.radius * 0.1f);
    }

    // 공격 중 이동을 막기
    public void SetExternalMovementLock(bool locked)
    {
        externalMovementLock = locked;
    }

    // 공격 중 이동 애니메이션 재생x
    public void SetAnimationSuppressed(bool suppressed)
    {
        if (animationSuppressed == suppressed)
            return;

        animationSuppressed = suppressed;

        if (animationSuppressed)
        {
            StopDirectMovementClip();
            currentMotionAnimation = MotionAnimationState.None;
        }
        else
        {
            currentMotionAnimation = MotionAnimationState.None;
        }
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            x += 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            y -= 1f;

        Vector2 input = new Vector2(x, y);
        return input.sqrMagnitude > 1f ? input.normalized : input;
#else
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return input.sqrMagnitude > 1f ? input.normalized : input;
#endif
    }

    private Vector3 BuildMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        if (useCameraRelativeMovement && cameraTransform != null)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward = cameraForward.sqrMagnitude < 0.0001f ? Vector3.forward : cameraForward.normalized;
            cameraRight = cameraRight.sqrMagnitude < 0.0001f ? Vector3.right : cameraRight.normalized;

            return (cameraForward * input.y + cameraRight * input.x).normalized;
        }

        return new Vector3(input.x, 0f, input.y).normalized;
    }

    private void Move(Vector3 moveDirection, float inputMagnitude)
    {
        if (characterController == null)
            return;

        bool grounded = IsGrounded();
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickVelocity;
        else
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, maxFallSpeed);

        Vector3 horizontalVelocity = moveDirection * (moveSpeed * inputMagnitude);
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private bool IsGrounded()
    {
        if (characterController != null && characterController.isGrounded)
            return true;

        if (!useGroundProbeFallback || characterController == null)
            return false;

        Vector3 centerWorld = transform.TransformPoint(characterController.center);
        float radius = Mathf.Max(0.01f, characterController.radius * 0.85f);
        float castDistance = Mathf.Max(0.01f, characterController.height * 0.5f - radius + groundExtraDistance);

        return Physics.SphereCast(centerWorld, radius, Vector3.down, out _, castDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void Rotate(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimatorParameters(float inputMagnitude)
    {
        if (animator == null)
            return;

        float speed01 = Mathf.Clamp01(inputMagnitude);

        if (!string.IsNullOrEmpty(speedFloatParameter) && animatorParameterNames.Contains(speedFloatParameter))
            animator.SetFloat(speedFloatParameter, speed01);

        if (!string.IsNullOrEmpty(movingBoolParameter) && animatorParameterNames.Contains(movingBoolParameter))
            animator.SetBool(movingBoolParameter, speed01 > 0.05f);
    }

    private void UpdateMovementAnimation(float inputMagnitude)
    {
        if (animationSuppressed || animator == null)
            return;

        MotionAnimationState desiredState = inputMagnitude > 0.05f ? MotionAnimationState.Run : MotionAnimationState.Idle;
        if (currentMotionAnimation == desiredState)
            return;

        currentMotionAnimation = desiredState;

        if (desiredState == MotionAnimationState.Run)
            PlayMovementAnimation(runClip, runStateName, runClipSpeed, MotionAnimationState.Run);
        else
            PlayMovementAnimation(idleClip, idleStateName, idleClipSpeed, MotionAnimationState.Idle);
    }

    private void PlayMovementAnimation(AnimationClip clip, string stateName, float speed, MotionAnimationState label)
    {
        if (animator == null)
            return;

        animator.enabled = true;

        if (disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        if (useDirectClipPlayback && clip != null)
        {
            MovementClipPlayer.Play(animator, clip, speed, loopMovementClips);
            return;
        }

        StopDirectMovementClip();

        string resolvedState = ResolveAnimatorStateName(stateName);
        if (!string.IsNullOrEmpty(resolvedState))
            animator.CrossFadeInFixedTime(resolvedState, 0.08f, 0, 0f);
    }

    private string ResolveAnimatorStateName(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return string.Empty;

        string resolved = AnimatorStateUtility.ResolveStateName(animator, stateName);
        if (string.IsNullOrEmpty(resolved))
            Debug.LogWarning($"[PlayerMovement] Animator state not found: {stateName}. If you use Direct Clip Playback with clips assigned, this warning can be ignored.", this);

        return resolved;
    }

    private void StopDirectMovementClip()
    {
        movementClipPlayer?.Stop();
    }

    private void KeepMovementClipLoopingIfNeeded()
    {
        if (loopMovementClips)
            movementClipPlayer?.Tick();
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterNames.Clear();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
            animatorParameterNames.Add(parameter.name);
    }

    private void DisableRigidbodyPhysics()
    {
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in rigidbodies)
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private void OnDisable()
    {
        StopDirectMovementClip();
        currentMotionAnimation = MotionAnimationState.None;
    }

    private void OnDestroy()
    {
        movementClipPlayer?.Dispose();
    }
}
