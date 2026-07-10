// 보스 패턴 VFX
using System;
using UnityEngine;

public class BossPatternProjectile : MonoBehaviour
{
    public float speed = 5f;
    public float maxLifetime = 5f;
    public bool lookAtDirection = true;
    public float hitRadius = 0.8f;
    public PlayerStatus targetPlayer;
    public bool hitAnyPartyMember = true;
    public Action<PlayerStatus> onHitPlayer;
    public Action onArrived;

    private Vector3 targetPosition;
    private float elapsed;
    private bool completed;

    public void Initialize(Vector3 target, float projectileSpeed, float lifetime)
    {
        targetPosition = target;
        speed = Mathf.Max(0.1f, projectileSpeed);
        maxLifetime = Mathf.Max(0.05f, lifetime);
        RotateToTarget();
    }

    private void Update()
    {
        if (completed)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= maxLifetime)
        {
            Complete(false);
            return;
        }

        Vector3 current = transform.position;
        Vector3 next = Vector3.MoveTowards(current, targetPosition, speed * Time.deltaTime);
        transform.position = next;

        if (lookAtDirection)
            RotateToTarget();

        PlayerStatus hit = FindHitTarget();
        if (hit != null)
        {
            onHitPlayer?.Invoke(hit);
            Complete(true);
            return;
        }

        if ((targetPosition - next).sqrMagnitude <= 0.02f)
            Complete(false);
    }

    private PlayerStatus FindHitTarget()
    {
        if (hitAnyPartyMember)
        {
            PlayerStatus[] players = FindObjectsByType<PlayerStatus>();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerStatus player = players[i];
                if (!IsValidTarget(player))
                    continue;

                Vector3 flat = player.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= hitRadius)
                    return player;
            }

            return null;
        }

        if (!IsValidTarget(targetPlayer))
            return null;

        Vector3 delta = targetPlayer.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude <= hitRadius ? targetPlayer : null;
    }

    private bool IsValidTarget(PlayerStatus player)
    {
        if (player == null)
            return false;

        if (player.GetComponent<BossDummyController>() != null)
            return false;

        if (player.Health == null || player.Health.IsDead)
            return false;

        return true;
    }

    private void Complete(bool hit)
    {
        if (completed)
            return;

        completed = true;
        onArrived?.Invoke();
        Destroy(gameObject);
    }

    private void RotateToTarget()
    {
        Vector3 direction = targetPosition - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
