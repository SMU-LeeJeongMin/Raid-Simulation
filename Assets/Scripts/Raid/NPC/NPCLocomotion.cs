using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC의 이동 실행 계층.
/// NavMeshAgent 관리, CharacterController 폴백, 끼임 복구, 회전을 담당.
/// 의사결정(정책) 계층과 분리되어 있어 새 정책을 추가해도 이동 코드는 재사용.
/// 튜닝 값은 소유 컨트롤러의 직렬화 필드를 그대로 읽음.
/// </summary>
public class NPCLocomotion
{
    private readonly NPCSimpleFSMController owner;

    private float verticalVelocity;
    private Vector3 desiredMoveDirection;
    private Vector3 desiredDestination;
    private bool hasDestination;
    private bool hasFacingOverride;
    private Vector3 facingOverridePosition;
    private float currentStoppingDistance;
    private Vector3 lastAgentPosition;
    private float stuckTimer;
    private readonly NavMeshPath reusablePath = new NavMeshPath();

    public bool UsingNavMesh { get; private set; }
    public string NavMeshState { get; private set; } = string.Empty;

    public NPCLocomotion(NPCSimpleFSMController owner)
    {
        this.owner = owner;
    }

    private Transform Transform => owner.transform;
    private NavMeshAgent Agent => owner.navMeshAgent;
    private CharacterController Body => owner.characterController;

    // ---------- NavMeshAgent 준비 ----------

    public void PrepareAgent()
    {
        if (!owner.useNavMeshAgent)
        {
            UsingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (Agent == null && owner.addNavMeshAgentIfMissing)
            owner.navMeshAgent = owner.gameObject.AddComponent<NavMeshAgent>();

        if (Agent == null)
        {
            UsingNavMesh = false;
            NavMeshState = "No NavMeshAgent";
            RestoreCharacterControllerForFallback();
            return;
        }

        ApplyAgentSettings();
        Agent.stoppingDistance = owner.formationStopDistance;
    }

    // 3곳에 복붙되어 있던 에이전트 설정의 단일 구현
    private void ApplyAgentSettings()
    {
        Agent.speed = owner.moveSpeed;
        Agent.angularSpeed = owner.agentAngularSpeed;
        Agent.acceleration = owner.agentAcceleration;
        Agent.radius = Mathf.Max(0.01f, owner.agentRadius);
        Agent.height = Mathf.Max(0.1f, owner.agentHeight);
        Agent.avoidancePriority = Mathf.Clamp(Mathf.RoundToInt(owner.agentAvoidancePriority), 0, 99);
        Agent.autoBraking = true;
        Agent.updateRotation = false;
        Agent.updatePosition = true;
    }

    public void TryPlaceAgentOnNavMesh()
    {
        if (!owner.useNavMeshAgent || Agent == null)
        {
            UsingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (!NavMesh.SamplePosition(Transform.position, out NavMeshHit hit, Mathf.Max(0.1f, owner.navMeshSampleRadius), owner.navMeshAreaMask))
        {
            Agent.enabled = false;
            UsingNavMesh = false;
            NavMeshState = "No NavMesh Nearby - Fallback";
            RestoreCharacterControllerForFallback();
            return;
        }

        if (Agent.enabled)
            Agent.enabled = false;

        Transform.position = hit.position;
        Agent.enabled = true;

        ApplyAgentSettings();

        UsingNavMesh = Agent.isOnNavMesh;
        NavMeshState = UsingNavMesh ? "Using NavMeshAgent" : "Fallback CharacterController";

        if (UsingNavMesh && Body != null && owner.disableCharacterControllerWhenUsingAgent)
            Body.enabled = false;
        else if (!UsingNavMesh)
            RestoreCharacterControllerForFallback();
    }

    private void RestoreCharacterControllerForFallback()
    {
        if (Body != null && !Body.enabled)
            Body.enabled = true;
    }

    public void EnsureAgentState()
    {
        if (!owner.useNavMeshAgent || Agent == null)
        {
            UsingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (!Agent.enabled || !Agent.isOnNavMesh)
        {
            TryPlaceAgentOnNavMesh();
            return;
        }

        UsingNavMesh = true;
        Agent.speed = owner.moveSpeed;
        Agent.angularSpeed = owner.agentAngularSpeed;
        Agent.acceleration = owner.agentAcceleration;
    }

    // ---------- 이동 의도 설정 ----------

    public void MoveToward(Vector3 position, float stoppingDistance = 0.1f)
    {
        Vector3 direction = position - Transform.position;
        direction.y = 0f;
        desiredMoveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        desiredDestination = position;
        currentStoppingDistance = Mathf.Max(0.05f, stoppingDistance);
        hasDestination = true;
    }

    public void MoveAwayFrom(Vector3 position, float desiredSeparation)
    {
        Vector3 direction = Transform.position - position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Transform.forward;

        direction.Normalize();

        // 이미 기준점에서 목표 간격보다 멀리 있으면 기준점 쪽으로 되돌아가지 않고
        // 바깥 방향으로 한 걸음 더 이동 (기준점 방향으로 도망치는 오동작 방지)
        float currentSeparation = NPCSimpleFSMController.FlatDistance(Transform.position, position);
        float targetSeparation = Mathf.Max(desiredSeparation, owner.keepDistanceFromBoss);
        if (currentSeparation >= targetSeparation)
            targetSeparation = currentSeparation + 1f;

        Vector3 destination = position + direction * targetSeparation;
        desiredMoveDirection = direction;
        desiredDestination = destination;
        currentStoppingDistance = 0.15f;
        hasDestination = true;
    }

    public void MoveToCombatRange(Vector3 targetPosition, float desiredDistance)
    {
        Vector3 awayFromTarget = Transform.position - targetPosition;
        awayFromTarget.y = 0f;
        if (awayFromTarget.sqrMagnitude < 0.0001f)
            awayFromTarget = -Transform.forward;

        awayFromTarget.Normalize();
        Vector3 desiredPosition = targetPosition + awayFromTarget * Mathf.Max(0.1f, desiredDistance);
        MoveToward(desiredPosition, 0.2f);
    }

    public void FollowPlayerFormation()
    {
        Transform playerTarget = owner.playerTarget;
        if (playerTarget == null)
            return;

        // 자동 플레이어처럼 자기 자신이 기준점이면 대형 유지 대신 정지 (후진 반복 방지)
        if (playerTarget == Transform)
        {
            ClearMovement();
            return;
        }

        Vector3 targetPosition = playerTarget.position + playerTarget.rotation * owner.formationOffset;
        float distance = NPCSimpleFSMController.FlatDistance(Transform.position, targetPosition);

        if (distance > owner.formationStopDistance)
        {
            MoveToward(targetPosition, owner.formationStopDistance);
        }
        else
        {
            ClearMovement();
            if (owner.faceSameDirectionAsPlayerWhenIdle)
                RotateToward(playerTarget.forward);
        }
    }

    public void ClearMovement()
    {
        desiredMoveDirection = Vector3.zero;
        hasDestination = false;
        hasFacingOverride = false;
    }

    // 이동 중에도 지정 지점을 계속 바라보게 지시.
    // 미지정 시 기존처럼 이동 속도 방향으로 회전 (퇴각 시 보스에게 등을 돌리는 현상의 보정)
    public void FaceWhileMoving(Vector3 position)
    {
        hasFacingOverride = true;
        facingOverridePosition = position;
    }

    // ---------- 이동 실행 (매 프레임) ----------

    public void MoveAndRotate()
    {
        if (UsingNavMesh && Agent != null && Agent.enabled && Agent.isOnNavMesh)
            MoveWithNavMeshAgent();
        else
            MoveWithCharacterControllerFallback();
    }

    private void MoveWithNavMeshAgent()
    {
        if (hasDestination)
        {
            Vector3 destination;
            bool foundDestination = TryGetNavDestination(desiredDestination, requireCompletePath: true, out destination);

            // 완전 경로가 없으면 부분 경로라도 허용하여 도달 가능한 지점까지 접근 (제자리 멈춤 방지)
            if (!foundDestination)
                foundDestination = TryGetNavDestination(desiredDestination, requireCompletePath: false, out destination);

            // 목적지 주변에 NavMesh가 없으면 진행 방향의 근거리 지점으로 직선 폴백
            if (!foundDestination && desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 nearStep = Transform.position + desiredMoveDirection * 1.5f;
                foundDestination = TryGetNavDestination(nearStep, requireCompletePath: false, out destination);
            }

            if (!foundDestination && owner.playerTarget != null)
                foundDestination = TryFindAlternateFormationDestination(out destination);

            if (foundDestination)
            {
                Agent.stoppingDistance = Mathf.Max(0.05f, currentStoppingDistance);
                Agent.isStopped = false;

                if (!Agent.hasPath || (Agent.destination - destination).sqrMagnitude > 0.04f)
                    Agent.SetDestination(destination);
            }
            else
            {
                StopAgent();
            }
        }
        else
        {
            StopAgent();
        }

        bool moving = IsAgentMoving();
        Vector3 velocity = Agent.velocity;
        velocity.y = 0f;

        if (hasDestination && moving)
        {
            DetectAndRecoverAgentStuck();
        }
        else
        {
            stuckTimer = 0f;
            lastAgentPosition = Transform.position;
        }

        if (moving && velocity.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = velocity.normalized;
            if (hasFacingOverride && ShouldHoldFacingWhileMoving(moveDirection))
                RotateToward(facingOverridePosition - Transform.position);
            else
                RotateToward(moveDirection);
        }

        owner.UpdateMotionAnimation(moving);
    }

    // 이동 중 주시 유지 여부 판단.
    // 이동 방향이 주시 방향과 크게 어긋나면(뒷걸음) 몸을 돌려 이동 (문워크 방지).
    // 도착 후에는 정책의 FacePosition이 다시 보스를 바라보게 함
    private bool ShouldHoldFacingWhileMoving(Vector3 moveDirection)
    {
        if (!owner.turnAroundWhenMovingBackward)
            return true;

        Vector3 faceDirection = facingOverridePosition - Transform.position;
        faceDirection.y = 0f;
        moveDirection.y = 0f;

        if (faceDirection.sqrMagnitude < 0.0001f || moveDirection.sqrMagnitude < 0.0001f)
            return false;

        return Vector3.Angle(faceDirection, moveDirection) < owner.backpedalTurnAngle;
    }

    public bool TryGetReachableNavDestination(Vector3 requestedDestination, out Vector3 destination)
    {
        return TryGetNavDestination(requestedDestination, requireCompletePath: true, out destination);
    }

    // 부분 경로까지 허용한 목적지 산출 (후퇴 가능성 검사 등 실행 가능성 판단용)
    public bool TryGetAnyNavDestination(Vector3 requestedDestination, out Vector3 destination)
    {
        return TryGetNavDestination(requestedDestination, requireCompletePath: false, out destination);
    }

    // 요청 지점을 향한 경로가 실제로 도달하는 끝 지점 산출.
    // 장애물 건너편의 연결되지 않은 NavMesh 섬에 지점이 잡히는 경우,
    // 부분 경로의 마지막 지점(벽 앞)이 반환되므로 도달 가능성 판단이 정확해짐
    public bool TryGetReachableEndpoint(Vector3 requestedDestination, out Vector3 endpoint)
    {
        endpoint = Transform.position;

        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit hit, Mathf.Max(0.1f, owner.destinationSampleRadius), owner.navMeshAreaMask))
            return false;

        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
        {
            endpoint = hit.position;
            return true;
        }

        bool hasPath;
        try
        {
            hasPath = Agent.CalculatePath(hit.position, reusablePath);
        }
        catch (System.Exception exception)
        {
            NavMeshState = "CalculatePath failed: " + exception.GetType().Name;
            return false;
        }

        if (!hasPath || reusablePath.status == NavMeshPathStatus.PathInvalid)
            return false;

        Vector3[] corners = reusablePath.corners;
        if (corners == null || corners.Length == 0)
        {
            endpoint = hit.position;
            return reusablePath.status == NavMeshPathStatus.PathComplete;
        }

        endpoint = corners[corners.Length - 1];
        return true;
    }

    // NavMesh 위 목적지 산출. requireCompletePath가 false면 부분 경로도 허용
    private bool TryGetNavDestination(Vector3 requestedDestination, bool requireCompletePath, out Vector3 destination)
    {
        destination = requestedDestination;

        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit hit, Mathf.Max(0.1f, owner.destinationSampleRadius), owner.navMeshAreaMask))
            return false;

        destination = hit.position;

        if (!owner.validateNavMeshPath || Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return true;

        bool hasPath;
        try
        {
            hasPath = Agent.CalculatePath(destination, reusablePath);
        }
        catch (System.Exception exception)
        {
            NavMeshState = "CalculatePath failed: " + exception.GetType().Name;
            return false;
        }

        if (!hasPath)
            return false;

        if (reusablePath.status == NavMeshPathStatus.PathComplete)
            return true;

        return !requireCompletePath && reusablePath.status == NavMeshPathStatus.PathPartial;
    }

    private bool TryFindAlternateFormationDestination(out Vector3 destination)
    {
        destination = desiredDestination;

        Transform playerTarget = owner.playerTarget;
        if (playerTarget == null)
            return false;

        float radius = Mathf.Max(0.5f, owner.alternateFormationRadius);
        int samples = Mathf.Max(4, owner.alternateFormationSamples);

        for (int i = 0; i < samples; i++)
        {
            float angle = (Mathf.PI * 2f) * i / samples;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 candidate = playerTarget.position + playerTarget.TransformDirection(localOffset);

            if (TryGetReachableNavDestination(candidate, out destination))
                return true;
        }

        return TryGetReachableNavDestination(playerTarget.position, out destination);
    }

    private void DetectAndRecoverAgentStuck()
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return;

        float movedSqr = (Transform.position - lastAgentPosition).sqrMagnitude;
        bool shouldBeMoving = hasDestination && Agent.remainingDistance > Agent.stoppingDistance + 0.35f;

        if (shouldBeMoving && movedSqr < 0.0009f && Agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= Mathf.Max(0.2f, owner.stuckRepathTime))
            {
                stuckTimer = 0f;
                if (TryGetReachableNavDestination(desiredDestination, out Vector3 repathDestination))
                    Agent.SetDestination(repathDestination);
            }
        }
        else
        {
            stuckTimer = 0f;
            lastAgentPosition = Transform.position;
        }
    }

    private void MoveWithCharacterControllerFallback()
    {
        bool moving = desiredMoveDirection.sqrMagnitude > 0.0001f;
        Vector3 move = moving ? desiredMoveDirection * owner.moveSpeed : Vector3.zero;

        if (Body != null && Body.enabled)
        {
            ApplyVerticalVelocity(ref move);
            Body.Move(move * Time.deltaTime);
        }
        else
        {
            Transform.position += move * Time.deltaTime;
        }

        if (moving)
        {
            bool holdFacing = hasFacingOverride && ShouldHoldFacingWhileMoving(desiredMoveDirection);
            RotateToward(holdFacing ? facingOverridePosition - Transform.position : desiredMoveDirection);
        }

        owner.UpdateMotionAnimation(moving);
    }

    // 접지 유지/중력 적분의 단일 구현 (2곳 복붙 제거)
    private void ApplyVerticalVelocity(ref Vector3 move)
    {
        if (Body.isGrounded && verticalVelocity < 0f)
            verticalVelocity = owner.groundedStickVelocity;
        else
            verticalVelocity += owner.gravity * Time.deltaTime;

        move.y = verticalVelocity;
    }

    private bool IsAgentMoving()
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return false;

        if (Agent.pathPending)
            return true;

        if (Agent.velocity.sqrMagnitude > 0.02f)
            return true;

        if (hasDestination && Agent.remainingDistance > Agent.stoppingDistance + 0.05f)
            return true;

        return false;
    }

    public void StopAgent()
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return;

        Agent.isStopped = true;
        Agent.ResetPath();
    }

    // 사망 또는 행동 중일 때 중력만 적용
    public void ApplyGravityOnly()
    {
        if (UsingNavMesh)
            return;

        if (Body == null || !Body.enabled)
            return;

        Vector3 move = Vector3.zero;
        ApplyVerticalVelocity(ref move);
        Body.Move(Vector3.up * move.y * Time.deltaTime);
    }

    public void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Transform.rotation = Quaternion.RotateTowards(Transform.rotation, targetRotation, owner.rotationSpeed * Time.deltaTime);
    }

    public void FacePosition(Vector3 position)
    {
        Vector3 direction = position - Transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
