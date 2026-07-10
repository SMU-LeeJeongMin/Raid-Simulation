// 투사체 VFX

using System;
using UnityEngine;

public class SkillProjectileVFX : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed = 12f;
    private float maxLifetime = 2f;
    private bool lookAtTarget = true;
    private float elapsed;
    private Action onArrived;
    private bool completed;
    private Quaternion rotationOffset = Quaternion.identity;

    public void Initialize(
        Vector3 target,
        float projectileSpeed,
        float lifetime,
        bool rotateToTarget,
        Action arriveCallback,
        Quaternion visualRotationOffset)
    {
        targetPosition = target;
        speed = Mathf.Max(0.1f, projectileSpeed);
        maxLifetime = Mathf.Max(0.05f, lifetime);
        lookAtTarget = rotateToTarget;
        onArrived = arriveCallback;
        rotationOffset = visualRotationOffset;

        if (lookAtTarget)
            RotateTowardTarget();
    }

    public void Initialize(Vector3 target, float projectileSpeed, float lifetime, bool rotateToTarget, Action arriveCallback)
    {
        Initialize(target, projectileSpeed, lifetime, rotateToTarget, arriveCallback, Quaternion.identity);
    }

    private void Update()
    {
        if (completed)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= maxLifetime)
        {
            Complete();
            return;
        }

        Vector3 current = transform.position;
        Vector3 next = Vector3.MoveTowards(current, targetPosition, speed * Time.deltaTime);
        transform.position = next;

        if (lookAtTarget)
            RotateTowardTarget();

        if ((targetPosition - next).sqrMagnitude <= 0.01f)
            Complete();
    }

    private void Complete()
    {
        if (completed)
            return;

        completed = true;
        onArrived?.Invoke();
        Destroy(gameObject);
    }

    private void RotateTowardTarget()
    {
        Vector3 direction = targetPosition - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * rotationOffset;
    }
}
