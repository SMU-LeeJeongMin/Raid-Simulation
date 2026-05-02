// 플레이어를 따라가는 카메라
using UnityEngine;

public class RaidCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Camera Position")]
    public float distance = 5.5f;
    public float height = 3.0f;
    public bool useTargetForward = false;
    public float yaw = -90f;
    public float targetYawOffset = 0f;

    [Header("Wall Collision / Auto Zoom")]
    public bool enableWallCollision = true;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.15f;
    public float minCollisionDistance = 1.2f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.12f;
    public float rotationSmoothSpeed = 12f;
    public bool snapWhenTargetAssigned = true;

    private Vector3 positionVelocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        positionVelocity = Vector3.zero;

        if (target != null && snapWhenTargetAssigned)
            SnapToTarget();
    }

    private void FollowTarget()
    {
        Vector3 lookAtPosition = target.position + targetOffset;
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

    private Vector3 CalculateDesiredPosition(Vector3 lookAtPosition)
    {
        Quaternion cameraBaseRotation = GetCameraBaseRotation();
        return lookAtPosition + cameraBaseRotation * new Vector3(0f, height, -distance);
    }

    // 카메라가 벽 밖으로 나가면 캐릭터를 줌인
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
            if (IsColliderPartOfTarget(hitCollider))
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

    private bool IsColliderPartOfTarget(Collider collider)
    {
        if (target == null || collider == null)
            return false;

        return collider.transform == target || collider.transform.IsChildOf(target);
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

    private Quaternion GetCameraBaseRotation()
    {
        if (useTargetForward && target != null)
            return Quaternion.Euler(0f, target.eulerAngles.y + targetYawOffset, 0f);

        return Quaternion.Euler(0f, yaw, 0f);
    }

    // 카메라를 보간 없이 즉시 타겟 주변 위치로 이동
    public void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 lookAtPosition = target.position + targetOffset;
        Vector3 desiredPosition = CalculateDesiredPosition(lookAtPosition);
        transform.position = ResolveWallCollision(lookAtPosition, desiredPosition);
        RotateToLookAt(lookAtPosition);

        positionVelocity = Vector3.zero;
    }
}
