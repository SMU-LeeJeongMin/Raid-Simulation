// 보스 패턴 범위를 빨간색으로 미리 표시

using UnityEngine;

public enum BossTelegraphShape
{
    Circle,
    Rectangle,
    Cone
}

public class BossTelegraphArea : MonoBehaviour
{
    [Header("Runtime")]
    public BossTelegraphShape shape;
    public float duration = 1f;
    public bool destroyOnComplete = true;
    public Transform followTarget;
    public Vector3 followOffset;

    [Header("Pulse")]
    public bool pulseAlpha = true;
    public float minAlpha = 0.18f;
    public float maxAlpha = 0.55f;
    public float pulseSpeed = 7f;

    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private DangerZoneHandle dangerZoneHandle;
    private float zoneRadius;
    private float zoneLength;
    private float zoneWidth;
    private float zoneAngle;
    private float elapsed;
    private Color baseColor = new Color(1f, 0f, 0f, 0.45f);

    public static BossTelegraphArea CreateCircle(
        Vector3 center,
        float radius,
        float duration,
        Color color,
        Transform parent = null,
        Material sharedMaterial = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Telegraph_Circle";
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(radius * 2f, 0.015f, radius * 2f);
        RemoveCollider(go);

        BossTelegraphArea area = go.AddComponent<BossTelegraphArea>();
        area.shape = BossTelegraphShape.Circle;
        area.zoneRadius = radius;
        area.zoneLength = radius * 2f;
        area.zoneWidth = radius * 2f;
        area.zoneAngle = 360f;
        area.Initialize(duration, color, sharedMaterial);
        return area;
    }

    public static BossTelegraphArea CreateRectangle(
        Vector3 center,
        Vector3 forward,
        float length,
        float width,
        float duration,
        Color color,
        Transform parent = null,
        Material sharedMaterial = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Telegraph_Rectangle";
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        go.transform.localScale = new Vector3(width, 0.025f, length);
        RemoveCollider(go);

        BossTelegraphArea area = go.AddComponent<BossTelegraphArea>();
        area.shape = BossTelegraphShape.Rectangle;
        area.zoneLength = length;
        area.zoneWidth = width;
        area.zoneRadius = Mathf.Max(length, width) * 0.5f;
        area.zoneAngle = 0f;
        area.Initialize(duration, color, sharedMaterial);
        return area;
    }

    public static BossTelegraphArea CreateCone(
        Vector3 origin,
        Vector3 forward,
        float radius,
        float angleDegrees,
        float duration,
        Color color,
        Transform parent = null,
        Material sharedMaterial = null)
    {
        GameObject go = new GameObject("Telegraph_Cone");
        go.transform.SetParent(parent, false);
        go.transform.position = origin;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        filter.mesh = BuildConeMesh(radius, Mathf.Clamp(angleDegrees, 1f, 360f), 48);

        BossTelegraphArea area = go.AddComponent<BossTelegraphArea>();
        area.shape = BossTelegraphShape.Cone;
        area.zoneLength = radius;
        area.zoneRadius = radius;
        area.zoneWidth = radius;
        area.zoneAngle = Mathf.Clamp(angleDegrees, 1f, 360f);
        area.cachedRenderer = renderer;
        area.Initialize(duration, color, sharedMaterial);
        return area;
    }

    public void Initialize(float newDuration, Color color, Material sharedMaterial = null)
    {
        duration = Mathf.Max(0.05f, newDuration);
        baseColor = color;

        if (cachedRenderer == null)
            cachedRenderer = GetComponentInChildren<Renderer>(true);

        runtimeMaterial = sharedMaterial != null ? new Material(sharedMaterial) : CreateDefaultTelegraphMaterial(color);
        ApplyColor(color);

        if (cachedRenderer != null)
            cachedRenderer.material = runtimeMaterial;

        RegisterDangerZone();
    }

    private void RegisterDangerZone()
    {
        if (dangerZoneHandle == null)
            dangerZoneHandle = gameObject.GetComponent<DangerZoneHandle>();
        if (dangerZoneHandle == null)
            dangerZoneHandle = gameObject.AddComponent<DangerZoneHandle>();

        switch (shape)
        {
            case BossTelegraphShape.Circle:
                dangerZoneHandle.ConfigureCircle(name, DangerZoneCategory.Telegraph, zoneRadius > 0f ? zoneRadius : Mathf.Max(transform.localScale.x, transform.localScale.z) * 0.5f, duration, 0f, 0f, false);
                break;
            case BossTelegraphShape.Rectangle:
                dangerZoneHandle.ConfigureRectangle(name, DangerZoneCategory.Telegraph, zoneLength > 0f ? zoneLength : transform.localScale.z, zoneWidth > 0f ? zoneWidth : transform.localScale.x, duration, 0f, 0f, false);
                break;
            case BossTelegraphShape.Cone:
                dangerZoneHandle.ConfigureCone(name, DangerZoneCategory.Telegraph, zoneLength > 0f ? zoneLength : zoneRadius, zoneAngle > 0f ? zoneAngle : 60f, duration, 0f, 0f, false);
                break;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (followTarget != null)
            transform.position = followTarget.position + followOffset;

        if (pulseAlpha && runtimeMaterial != null)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            Color c = baseColor;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            ApplyColor(c);
        }

        if (elapsed >= duration && destroyOnComplete)
            Destroy(gameObject);
    }

    public void SetColor(Color color)
    {
        baseColor = color;
        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        if (runtimeMaterial == null)
            return;

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", color);
    }

    private static Material CreateDefaultTelegraphMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.name = "M_Runtime_BossTelegraph";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        return mat;
    }

    private static Mesh BuildConeMesh(float radius, float angleDegrees, int segments)
    {
        Mesh mesh = new Mesh();
        segments = Mathf.Max(3, segments);

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        float half = angleDegrees * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments) * Mathf.Deg2Rad;
            float x = Mathf.Sin(a) * radius;
            float z = Mathf.Cos(a) * radius;
            vertices[i + 1] = new Vector3(x, 0.01f, z);
        }

        for (int i = 0; i < segments; i++)
        {
            int idx = i * 3;
            triangles[idx] = 0;
            triangles[idx + 1] = i + 1;
            triangles[idx + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
    }
}
