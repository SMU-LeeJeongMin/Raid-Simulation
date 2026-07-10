// 슬라임이 제시간에 처리되지 못하였을 때 남기는 장판
using System.Collections;
using UnityEngine;

public class SlimeDotZone : MonoBehaviour
{
    public float radius = 2.2f;
    public float tickDamage = 8f;
    public float tickInterval = 0.5f;
    public float duration = -1f;
    public GameObject zoneVFXPrefab;
    public Color fallbackColor = new Color(0.8f, 0.05f, 0.05f, 0.35f);
    public float visualHeight = 0.035f;

    private GameObject visualInstance;
    private DangerZoneHandle dangerZoneHandle;
    private float elapsed;

    public static SlimeDotZone Create(Vector3 position, float radius, float tickDamage, float tickInterval, float duration, GameObject zoneVFXPrefab)
    {
        GameObject zoneObject = new GameObject("Slime_Persistent_DOT_Zone");
        zoneObject.transform.position = position;
        SlimeDotZone zone = zoneObject.AddComponent<SlimeDotZone>();
        zone.radius = radius;
        zone.tickDamage = tickDamage;
        zone.tickInterval = tickInterval;
        zone.duration = duration;
        zone.zoneVFXPrefab = zoneVFXPrefab;
        return zone;
    }

    private void Start()
    {
        RegisterDangerZone();
        RaidMetricsEvents.ReportSlimeDotZoneCreated(this);
        CreateVisual();
        StartCoroutine(TickRoutine());
    }

    private void RegisterDangerZone()
    {
        dangerZoneHandle = GetComponent<DangerZoneHandle>();
        if (dangerZoneHandle == null)
            dangerZoneHandle = gameObject.AddComponent<DangerZoneHandle>();

        dangerZoneHandle.ConfigureCircle("Slime_DOT_Zone", DangerZoneCategory.SlimeDOT, radius, duration, 0f, tickDamage, duration <= 0f);
    }

    private void Update()
    {
        if (duration <= 0f)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private IEnumerator TickRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, tickInterval));
        while (true)
        {
            ApplyTickDamage();
            yield return wait;
        }
    }

    private void ApplyTickDamage()
    {
        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>();
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null || status.Health.IsDead)
                continue;

            Vector3 delta = status.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= radius)
                status.TakeDamage(tickDamage, gameObject);
        }
    }

    private void CreateVisual()
    {
        if (zoneVFXPrefab != null)
        {
            visualInstance = Instantiate(zoneVFXPrefab, transform.position, Quaternion.identity, transform);
            return;
        }

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Slime_DOT_Zone_Visual";
        cylinder.transform.SetParent(transform, false);
        cylinder.transform.localPosition = Vector3.up * 0.02f;
        cylinder.transform.localScale = new Vector3(radius * 2f, visualHeight, radius * 2f);

        Collider col = cylinder.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = cylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = fallbackColor;
            renderer.sharedMaterial = mat;
        }

        visualInstance = cylinder;
    }
}
