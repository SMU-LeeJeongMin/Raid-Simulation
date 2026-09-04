using System;
using UnityEngine;

public enum DangerZoneShape
{
    Circle,
    Rectangle,
    Cone
}

public enum DangerZoneCategory
{
    Telegraph,
    ActiveDamage,
    PersistentDOT,
    Tracking,
    SlimeDOT,
    Unknown
}

[Serializable]
public struct DangerZoneSnapshot
{
    public int zoneId;
    public string sourceName;
    public DangerZoneCategory category;
    public DangerZoneShape shape;
    public Vector3 position;
    public Vector3 forward;
    public float radius;
    public float length;
    public float width;
    public float angle;
    public float damage;
    public float tickDamage;
    public float timeRemaining;
    public bool persistent;
}

/// <summary>
/// 현재 활성화된 위험 범위 1개를 표현합니다.
/// 보스 Telegraph, 슬라임 DOT 장판, Tracking Lightning 등이 이 컴포넌트로 등록됩니다.
/// NPC 회피와 GNN/GAT 그래프 노드 생성에 사용합니다.
/// </summary>
public class DangerZoneHandle : MonoBehaviour
{
    [Header("Identity")]
    public int zoneId;
    public string sourceName = "DangerZone";
    public DangerZoneCategory category = DangerZoneCategory.Unknown;
    public DangerZoneShape shape = DangerZoneShape.Circle;

    [Header("Shape")]
    public float radius = 1f;
    public float length = 1f;
    public float width = 1f;
    public float angle = 60f;

    [Header("Damage Info")]
    public float damage;
    public float tickDamage;

    [Header("Lifetime")]
    public bool persistent;
    public float duration = 1f;

    [Header("Line of Sight Exclusion")]
    [Tooltip("설정 시 이 지점에서 차폐물(OneShotWallMarker)에 가려진 위치는 안전으로 판정 (즉사기 벽 숨기 기믹)")]
    public Transform losOrigin;
    public float losCastHeight = 1f;

    private static readonly RaycastHit[] losHits = new RaycastHit[16];

    private float endTime;
    private bool registered;

    public float TimeRemaining
    {
        get
        {
            if (persistent || duration <= 0f)
                return -1f;
            return Mathf.Max(0f, endTime - Time.time);
        }
    }

    public void ConfigureCircle(string newSourceName, DangerZoneCategory newCategory, float newRadius, float newDuration, float newDamage = 0f, float newTickDamage = 0f, bool newPersistent = false)
    {
        sourceName = string.IsNullOrWhiteSpace(newSourceName) ? name : newSourceName;
        category = newCategory;
        shape = DangerZoneShape.Circle;
        radius = Mathf.Max(0.01f, newRadius);
        length = radius * 2f;
        width = radius * 2f;
        angle = 360f;
        damage = Mathf.Max(0f, newDamage);
        tickDamage = Mathf.Max(0f, newTickDamage);
        duration = newDuration;
        persistent = newPersistent || newDuration <= 0f;
        endTime = persistent ? float.PositiveInfinity : Time.time + Mathf.Max(0.01f, newDuration);
        RegisterIfNeeded();
    }

    public void ConfigureRectangle(string newSourceName, DangerZoneCategory newCategory, float newLength, float newWidth, float newDuration, float newDamage = 0f, float newTickDamage = 0f, bool newPersistent = false)
    {
        sourceName = string.IsNullOrWhiteSpace(newSourceName) ? name : newSourceName;
        category = newCategory;
        shape = DangerZoneShape.Rectangle;
        length = Mathf.Max(0.01f, newLength);
        width = Mathf.Max(0.01f, newWidth);
        radius = Mathf.Max(length, width) * 0.5f;
        angle = 0f;
        damage = Mathf.Max(0f, newDamage);
        tickDamage = Mathf.Max(0f, newTickDamage);
        duration = newDuration;
        persistent = newPersistent || newDuration <= 0f;
        endTime = persistent ? float.PositiveInfinity : Time.time + Mathf.Max(0.01f, newDuration);
        RegisterIfNeeded();
    }

    public void ConfigureCone(string newSourceName, DangerZoneCategory newCategory, float newLength, float newAngle, float newDuration, float newDamage = 0f, float newTickDamage = 0f, bool newPersistent = false)
    {
        sourceName = string.IsNullOrWhiteSpace(newSourceName) ? name : newSourceName;
        category = newCategory;
        shape = DangerZoneShape.Cone;
        length = Mathf.Max(0.01f, newLength);
        radius = length;
        width = length;
        angle = Mathf.Clamp(newAngle, 1f, 360f);
        damage = Mathf.Max(0f, newDamage);
        tickDamage = Mathf.Max(0f, newTickDamage);
        duration = newDuration;
        persistent = newPersistent || newDuration <= 0f;
        endTime = persistent ? float.PositiveInfinity : Time.time + Mathf.Max(0.01f, newDuration);
        RegisterIfNeeded();
    }

    private void OnEnable()
    {
        RegisterIfNeeded();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void Update()
    {
        if (!persistent && duration > 0f && Time.time >= endTime)
        {
            Unregister();
            return;
        }
    }

    private void RegisterIfNeeded()
    {
        if (!isActiveAndEnabled || registered)
            return;

        zoneId = DangerZoneRegistry.Register(this);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered)
            return;

        DangerZoneRegistry.Unregister(this);
        registered = false;
    }

    public bool ContainsPoint(Vector3 worldPoint, float padding = 0f)
    {
        // 차폐 제외: 시선 원점에서 벽에 가려진 위치는 범위 안이라도 안전
        if (losOrigin != null && IsShadowedFromOrigin(worldPoint))
            return false;

        return ContainsPointIgnoringShadow(worldPoint, padding);
    }

    // 차폐를 무시한 순수 도형 포함 판정.
    // "그림자 덕분에만 안전한 위치"의 식별(대피 유지 판단)에 사용
    public bool ContainsPointIgnoringShadow(Vector3 worldPoint, float padding = 0f)
    {
        Vector3 origin = transform.position;
        Vector3 flatDelta = worldPoint - origin;
        flatDelta.y = 0f;

        switch (shape)
        {
            case DangerZoneShape.Circle:
                return flatDelta.magnitude <= radius + padding;

            case DangerZoneShape.Rectangle:
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;
                forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float z = Vector3.Dot(flatDelta, forward);
                float x = Mathf.Abs(Vector3.Dot(flatDelta, right));
                return z >= -padding && z <= length + padding && x <= width * 0.5f + padding;

            case DangerZoneShape.Cone:
                Vector3 coneForward = transform.forward;
                coneForward.y = 0f;
                if (coneForward.sqrMagnitude < 0.0001f)
                    coneForward = Vector3.forward;
                coneForward.Normalize();

                float distance = flatDelta.magnitude;
                if (distance > length + padding)
                    return false;
                if (distance <= 0.0001f)
                    return true;

                float a = Vector3.Angle(coneForward, flatDelta.normalized);
                return a <= angle * 0.5f;
        }

        return false;
    }

    // 시선 원점과 지점 사이에 차폐 벽(OneShotWallMarker)이 있는지 (낮은 높이의 수평 레이캐스트)
    private bool IsShadowedFromOrigin(Vector3 point)
    {
        Vector3 from = losOrigin.position;
        from.y = point.y + Mathf.Max(0.1f, losCastHeight);
        Vector3 to = point;
        to.y = from.y;

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return false;

        int count = Physics.RaycastNonAlloc(from, delta / distance, losHits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (losHits[i].collider != null
                && losHits[i].collider.GetComponentInParent<OneShotWallMarker>() != null)
                return true;
        }

        return false;
    }

    public DangerZoneSnapshot ToSnapshot()
    {
        return new DangerZoneSnapshot
        {
            zoneId = zoneId,
            sourceName = sourceName,
            category = category,
            shape = shape,
            position = transform.position,
            forward = transform.forward,
            radius = radius,
            length = length,
            width = width,
            angle = angle,
            damage = damage,
            tickDamage = tickDamage,
            timeRemaining = TimeRemaining,
            persistent = persistent
        };
    }
}
