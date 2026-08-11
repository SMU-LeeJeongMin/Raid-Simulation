// 보스 패턴 VFX
using System;
using System.Collections.Generic;
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
            // 씬 전체 탐색 대신 레지스트리 순회 (투사체당 매 프레임 호출되는 핫패스)
            IReadOnlyList<PlayerStatus> players = CombatRegistry.PlayerStatuses;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerStatus player = players[i];
                if (!PartyTargetUtility.IsValidPlayerTarget(player))
                    continue;

                Vector3 flat = player.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= hitRadius)
                    return player;
            }

            return null;
        }

        if (!PartyTargetUtility.IsValidPlayerTarget(targetPlayer))
            return null;

        Vector3 delta = targetPlayer.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude <= hitRadius ? targetPlayer : null;
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
