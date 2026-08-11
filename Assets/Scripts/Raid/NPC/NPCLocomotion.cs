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
        Vector3 destination = position + direction * Mathf.Max(desiredSeparation, owner.keepDistanceFromBoss);
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
            bool foundDestination = TryGetReachableNavDestination(desiredDestination, out destination);

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
            RotateToward(velocity.normalized);

        owner.UpdateMotionAnimation(moving);
    }

    public bool TryGetReachableNavDestination(Vector3 requestedDestination, out Vector3 destination)
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

        return hasPath && reusablePath.status == NavMeshPathStatus.PathComplete;
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
            RotateToward(desiredMoveDirection);

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
