// Raid 씬 HUD를 현재 생성된 플레이어와 보스에 자동 연결
using System.Collections;
using UnityEngine;

public class RaidHUDController : MonoBehaviour
{
    [Header("UI")]
    public PlayerStatusUI playerStatusUI;
    public BossWorldHealthUI bossHealthUI;

    [Header("Auto Bind")]
    public bool autoBindOnStart = true;
    public float bindDelay = 0.25f;
    public bool keepTryingUntilFound = true;
    public bool logBinding = true;

    [Header("Player")]
    public RaidSelectedCharacterSpawner playerSpawner;
    public bool addPlayerStatusIfMissing = true;

    [Header("Boss")]
    public BossDummyController boss;
    public Transform bossHeadTarget;
    public string bossDisplayName = "Dragon Boss";
    private Coroutine bindRoutine;

    private void Start()
    {
        if (autoBindOnStart)
            StartAutoBind();
    }

    public void StartAutoBind()
    {
        if (bindRoutine != null)
            StopCoroutine(bindRoutine);

        bindRoutine = StartCoroutine(BindRoutine());
    }

    private IEnumerator BindRoutine()
    {
        if (bindDelay > 0f)
            yield return new WaitForSeconds(bindDelay);

        do
        {
            bool playerBound = BindPlayerIfPossible();
            bool bossBound = BindBossIfPossible();

            if (playerBound && bossBound)
                yield break;

            yield return new WaitForSeconds(0.25f);
        }
        while (keepTryingUntilFound);
    }

    public bool BindPlayerIfPossible()
    {
        if (playerStatusUI == null)
            return true;

        GameObject playerObject = FindPlayerObject();
        if (playerObject == null)
        {
            return false;
        }

        PlayerStatus status = playerObject.GetComponent<PlayerStatus>();
        if (status == null && addPlayerStatusIfMissing)
        {
            status = playerObject.AddComponent<PlayerStatus>();
        }

        if (status == null)
        {
            return false;
        }

        status.ResolveReferences();
        status.ApplyStatusSettings();
        playerStatusUI.Bind(status);

        bool bound = status.Health != null && status.Mana != null;
        return bound;
    }

    private GameObject FindPlayerObject()
    {
        if (playerSpawner == null)
            playerSpawner = FindAnyObjectByType<RaidSelectedCharacterSpawner>();

        if (playerSpawner != null && playerSpawner.SpawnedPlayer != null)
            return playerSpawner.SpawnedPlayer;

        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>();
        foreach (PlayerStatus status in statuses)
        {
            if (status != null && status.GetComponent<BossDummyController>() == null)
                return status.gameObject;
        }

        PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
            return movement.gameObject;

        PlayerBasicAttack attack = FindAnyObjectByType<PlayerBasicAttack>();
        if (attack != null)
            return attack.gameObject;

        return null;
    }

    public bool BindBossIfPossible()
    {
        if (bossHealthUI == null)
            return true;

        if (boss == null)
            boss = FindAnyObjectByType<BossDummyController>();

        if (boss == null)
        {
            bossHealthUI.Bind((Health)null, null, null, string.Empty);
            return false;
        }

        Health health = boss.health != null ? boss.health : boss.GetComponent<Health>();
        if (health == null)
        {
            bossHealthUI.Bind((Health)null, null, null, string.Empty);
            return false;
        }

        Transform validAnchor = ResolveBossAnchor(boss);
        string displayName = string.IsNullOrWhiteSpace(bossDisplayName) ? boss.bossName : bossDisplayName;
        bossHealthUI.Bind(health, boss.transform, validAnchor, displayName);
        bossHealthUI.rendererBoundsRoot = boss.transform;

        if (logBinding)
        {
            string anchorName = validAnchor != null ? validAnchor.name : "None";
        }

        return true;
    }

    private Transform ResolveBossAnchor(BossDummyController bossController)
    {
        if (bossHeadTarget == null)
            return null;

        bool isBossChild = bossHeadTarget == bossController.transform || bossHeadTarget.IsChildOf(bossController.transform);
        if (isBossChild)
            return bossHeadTarget;
        return null;
    }
}