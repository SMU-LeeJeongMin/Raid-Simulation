// 플레이어를 따라가는 카메라
// 보스와 가까워지면 레이드 카메라 값으로 전환

using UnityEngine;

public class RaidCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Camera Position - Normal")]
    public float distance = 5.5f;
    public float height = 3.0f;
    public bool useTargetForward = false;
    public float yaw = -90f;
    public float targetYawOffset = 0f;

    [Header("Camera Position - Boss / Raid View")]
    public bool enableBossCameraMode = true;
    public bool autoFindBoss = true;
    public Transform bossTarget;
    public float bossCameraEnterDistance = 12f;
    public float bossCameraExitDistance = 15f;
    public bool useHorizontalDistanceToBoss = true;
    public float bossDistance = 8.0f;
    public float bossHeight = 4.2f;
    public Vector3 bossTargetOffset = new Vector3(0f, 1.1f, 0f);
    [Range(0f, 1f)] public float bossLookAtWeight = 0.18f;
    public Vector3 bossLookAtOffset = new Vector3(0f, 2.0f, 0f);
    public float bossCameraBlendSpeed = 2.0f;
    public bool overrideYawInBossMode = false;
    public float bossYaw = -90f;

    [Header("Manual Boss Camera Mode")]
    public bool useManualBossCameraMode = false;
    public bool manualBossCameraMode = false;

    [Header("Wall Collision / Auto Zoom")]
    public bool enableWallCollision = true;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.15f;
    public float minCollisionDistance = 1.2f;
    public bool ignoreBossCollidersForWallCollision = true;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.12f;
    public float rotationSmoothSpeed = 12f;
    public bool snapWhenTargetAssigned = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool bossCameraActive;
    [SerializeField] private float bossCameraBlend;
    [SerializeField] private float currentCameraDistance;
    [SerializeField] private float currentCameraHeight;

    private Vector3 positionVelocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        TryFindBossIfNeeded();
        UpdateBossCameraState();
        FollowTarget();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        positionVelocity = Vector3.zero;

        if (target != null && snapWhenTargetAssigned)
            SnapToTarget();
    }

    public void SetBossTarget(Transform newBossTarget)
    {
        bossTarget = newBossTarget;
    }

    public void SetBossCameraMode(bool active)
    {
        useManualBossCameraMode = true;
        manualBossCameraMode = active;
    }

    public void ClearManualBossCameraMode()
    {
        useManualBossCameraMode = false;
    }

    private void TryFindBossIfNeeded()
    {
        if (!autoFindBoss || bossTarget != null)
            return;

        BossDummyController boss = FindAnyObjectByType<BossDummyController>();
        if (boss != null)
            bossTarget = boss.transform;
    }

    private void UpdateBossCameraState()
    {
        bool shouldUseBossCamera = false;

        if (useManualBossCameraMode)
        {
            shouldUseBossCamera = manualBossCameraMode;
        }
        else if (enableBossCameraMode && bossTarget != null && target != null)
        {
            float distanceToBoss = GetDistanceToBoss();

            if (!bossCameraActive && distanceToBoss <= bossCameraEnterDistance)
                shouldUseBossCamera = true;
            else if (bossCameraActive && distanceToBoss < bossCameraExitDistance)
                shouldUseBossCamera = true;
        }

        bossCameraActive = shouldUseBossCamera;

        float targetBlend = bossCameraActive ? 1f : 0f;
        bossCameraBlend = Mathf.MoveTowards(
            bossCameraBlend,
            targetBlend,
            Mathf.Max(0.01f, bossCameraBlendSpeed) * Time.deltaTime
        );
    }

    private float GetDistanceToBoss()
    {
        if (target == null || bossTarget == null)
            return float.PositiveInfinity;

        Vector3 playerPos = target.position;
        Vector3 bossPos = bossTarget.position;

        if (useHorizontalDistanceToBoss)
        {
            playerPos.y = 0f;
            bossPos.y = 0f;
        }

        return Vector3.Distance(playerPos, bossPos);
    }

    private void FollowTarget()
    {
        Vector3 lookAtPosition = CalculateLookAtPosition();
        Vector3 desiredPosition = CalculateDesiredPosition(lookAtPosition);
        desiredPosition = ResolveWallCollision(lookAtPosition, desiredPosition);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            Mathf.Max(0.001f, positionSmoothTime)
        );

        RotateToLookAt(lookAtPosition);
    }

    private Vector3 CalculateLookAtPosition()
    {
        Vector3 effectiveTargetOffset = Vector3.Lerp(targetOffset, bossTargetOffset, bossCameraBlend);
        Vector3 playerLookAt = target.position + effectiveTargetOffset;

        if (bossTarget == null || bossCameraBlend <= 0.001f || bossLookAtWeight <= 0f)
            return playerLookAt;

        Vector3 bossLookAt = bossTarget.position + bossLookAtOffset;
        float weight = Mathf.Clamp01(bossLookAtWeight * bossCameraBlend);
        return Vector3.Lerp(playerLookAt, bossLookAt, weight);
    }

    private Vector3 CalculateDesiredPosition(Vector3 lookAtPosition)
    {
        currentCameraDistance = Mathf.Lerp(distance, bossDistance, bossCameraBlend);
        currentCameraHeight = Mathf.Lerp(height, bossHeight, bossCameraBlend);

        float effectiveYaw = yaw;
        if (overrideYawInBossMode)
            effectiveYaw = Mathf.LerpAngle(yaw, bossYaw, bossCameraBlend);

        Quaternion cameraBaseRotation = GetCameraBaseRotation(effectiveYaw);
        return lookAtPosition + cameraBaseRotation * new Vector3(0f, currentCameraHeight, -currentCameraDistance);
    }

    // 카메라가 벽 밖으로 나가면 캐릭터 쪽으로 줌인합니다.
    private Vector3 ResolveWallCollision(Vector3 lookAtPosition, Vector3 desiredPosition)
    {
        if (!enableWallCollision)
            return desiredPosition;

        Vector3 toCamera = desiredPosition - lookAtPosition;
        float desiredDistance = toCamera.magnitude;

        if (desiredDistance <= 0.001f)
            return desiredPosition;

        Vector3 direction = toCamera / desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            lookAtPosition,
            Mathf.Max(0.01f, collisionRadius),
            direction,
            desiredDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return desiredPosition;

        float nearestValidDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            // 플레이어 자신의 CharacterController/Collider에 맞아서 줌인되는 것을 방지
            if (IsColliderPartOfTransform(hitCollider, target))
                continue;

            // 보스 Collider에 맞아서 불필요하게 줌인되는 것을 방지
            if (ignoreBossCollidersForWallCollision && IsColliderPartOfTransform(hitCollider, bossTarget))
                continue;

            if (hits[i].distance < nearestValidDistance)
                nearestValidDistance = hits[i].distance;
        }

        if (float.IsPositiveInfinity(nearestValidDistance))
            return desiredPosition;

        float correctedDistance = Mathf.Clamp(
            nearestValidDistance - collisionPadding,
            Mathf.Max(0.05f, minCollisionDistance),
            desiredDistance
        );

        return lookAtPosition + direction * correctedDistance;
    }

    private bool IsColliderPartOfTransform(Collider collider, Transform owner)
    {
        if (owner == null || collider == null)
            return false;

        return collider.transform == owner || collider.transform.IsChildOf(owner);
    }

    private void RotateToLookAt(Vector3 lookAtPosition)
    {
        Vector3 lookDirection = lookAtPosition - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    private Quaternion GetCameraBaseRotation(float effectiveYaw)
    {
        if (useTargetForward && target != null)
            return Quaternion.Euler(0f, target.eulerAngles.y + targetYawOffset, 0f);

        return Quaternion.Euler(0f, effectiveYaw, 0f);
    }

    // 카메라를 보간 없이 즉시 타겟 주변 위치로 이동
    public void SnapToTarget()
    {
        if (target == null)
            return;

        TryFindBossIfNeeded();
        UpdateBossCameraState();

        Vector3 lookAtPosition = CalculateLookAtPosition();
        Vector3 desiredPosition = CalculateDesiredPosition(lookAtPosition);
        transform.position = ResolveWallCollision(lookAtPosition, desiredPosition);
        RotateToLookAt(lookAtPosition);

        positionVelocity = Vector3.zero;
    }
}
