// Raid 씬에서 선택 화면에서 고른 캐릭터를 생성

using UnityEngine;

public class RaidSelectedCharacterSpawner : MonoBehaviour
{
    public enum SpawnHeightMode
    {
        UseSpawnPointPosition,
        TreatSpawnPointAsGround,
        RaycastNearSpawnPoint
    }

    [Header("Spawn Data")]
    public CharacterLineupDatabase database;
    public Transform spawnPoint;
    public bool useFallbackFirstCharacter = true;

    [Header("Spawn Height")]
    public SpawnHeightMode spawnHeightMode = SpawnHeightMode.TreatSpawnPointAsGround;
    public LayerMask groundMask = ~0;
    public float maxSnapUpDistance = 0.5f;
    public float maxSnapDownDistance = 1.5f;
    public float spawnGroundOffset = 0.03f;

    [Header("Player Setup")]
    public bool addMovementControllerIfMissing = true;
    public bool addCharacterControllerIfMissing = true;
    public bool configureCharacterController = true;
    public Vector3 controllerCenter = new Vector3(0f, 0.6f, 0f);
    public float controllerHeight = 1.2f;
    public float controllerRadius = 0.28f;
    public bool removeCharacterSelectActorComponent = true;
    public bool disableAnimatorRootMotion = true;
    public bool makeRigidbodiesKinematic = true;

    [Header("Camera Setup")]
    public Camera raidCamera;
    public RaidCameraFollow cameraFollow;
    public bool addCameraFollowIfMissing = true;
    public bool assignCameraTargetOnSpawn = true;
    public bool forceFixedYawCamera = true;

    private GameObject spawnedPlayer;
    public GameObject SpawnedPlayer => spawnedPlayer;

    private void Start()
    {
        SpawnSelectedCharacter();
    }

    public GameObject SpawnSelectedCharacter()
    {
        DestroyPreviousSpawnedInstanceOnly();

        if (database == null)
        {
            return null;
        }

        int selectedIndex = SelectedCharacterMemory.LoadSelectedIndex(0);
        if (!database.IsValidIndex(selectedIndex))
        {
            if (!useFallbackFirstCharacter || !database.IsValidIndex(0))
            {
                return null;
            }

            selectedIndex = 0;
        }

        CharacterLineupEntry entry = database.Get(selectedIndex);
        GameObject prefab = entry.raidPrefab != null ? entry.raidPrefab : entry.characterPrefab;

        if (prefab == null)
        {
            return null;
        }

        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        spawnedPlayer = Instantiate(prefab, basePosition, rotation);
        spawnedPlayer.name = $"Player_{entry.jobName}";

        SetupSpawnedPlayer(spawnedPlayer);
        ApplySpawnHeight(spawnedPlayer, basePosition);
        SetupCamera(spawnedPlayer.transform);

        return spawnedPlayer;
    }

    private void DestroyPreviousSpawnedInstanceOnly()
    {
        if (spawnedPlayer == null)
            return;

        if (spawnedPlayer.scene.IsValid())
            Destroy(spawnedPlayer);

        spawnedPlayer = null;
    }

    private void SetupSpawnedPlayer(GameObject player)
    {
        if (player == null)
            return;

        if (removeCharacterSelectActorComponent)
        {
            CharacterLineupActor[] lineupActors = player.GetComponentsInChildren<CharacterLineupActor>(true);
            foreach (CharacterLineupActor actor in lineupActors)
            {
                if (actor != null)
                    Destroy(actor);
            }
        }

        if (makeRigidbodiesKinematic)
            MakeRigidbodiesKinematic(player);

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (disableAnimatorRootMotion && animator != null)
            animator.applyRootMotion = false;

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController == null && addCharacterControllerIfMissing)
            characterController = player.AddComponent<CharacterController>();

        if (characterController != null && configureCharacterController)
            ConfigureCharacterController(characterController);

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null && addMovementControllerIfMissing)
            movement = player.AddComponent<PlayerMovement>();

        if (movement != null)
        {
            Camera cameraToUse = raidCamera != null ? raidCamera : Camera.main;
            movement.disableAnimatorRootMotion = disableAnimatorRootMotion;
            movement.makeRigidbodiesKinematic = makeRigidbodiesKinematic;
            movement.ConfigureRuntime(
                cameraToUse != null ? cameraToUse.transform : null,
                controllerCenter,
                controllerHeight,
                controllerRadius,
                groundMask
            );
        }
    }

    private void ConfigureCharacterController(CharacterController characterController)
    {
        characterController.center = controllerCenter;
        characterController.height = Mathf.Max(0.1f, controllerHeight);
        characterController.radius = Mathf.Max(0.01f, controllerRadius);
        characterController.stepOffset = Mathf.Min(0.3f, characterController.height * 0.25f);
        characterController.skinWidth = Mathf.Max(0.02f, characterController.radius * 0.1f);
    }

    private void ApplySpawnHeight(GameObject player, Vector3 basePosition)
    {
        if (player == null)
            return;

        switch (spawnHeightMode)
        {
            case SpawnHeightMode.UseSpawnPointPosition:
                player.transform.position = basePosition;
                break;

            case SpawnHeightMode.TreatSpawnPointAsGround:
                PlaceCharacterControllerBottomAtY(player, basePosition.y);
                break;

            case SpawnHeightMode.RaycastNearSpawnPoint:
                if (TryFindGroundNearSpawnPoint(basePosition, out RaycastHit hit))
                    PlaceCharacterControllerBottomAtY(player, hit.point.y);
                else
                    PlaceCharacterControllerBottomAtY(player, basePosition.y);
                break;
        }
    }

    private bool TryFindGroundNearSpawnPoint(Vector3 basePosition, out RaycastHit hit)
    {
        float up = Mathf.Max(0f, maxSnapUpDistance);
        float down = Mathf.Max(0.01f, maxSnapDownDistance);
        Vector3 rayOrigin = basePosition + Vector3.up * up;
        float rayDistance = up + down;

        return Physics.Raycast(rayOrigin, Vector3.down, out hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void PlaceCharacterControllerBottomAtY(GameObject player, float groundY)
    {
        if (player == null)
            return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            Vector3 p = player.transform.position;
            p.y = groundY + spawnGroundOffset;
            player.transform.position = p;
            return;
        }

        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        float controllerBottomLocalY = controller.center.y - controller.height * 0.5f;
        Vector3 position = player.transform.position;
        position.y = groundY - controllerBottomLocalY + spawnGroundOffset;
        player.transform.position = position;

        controller.enabled = wasEnabled;
    }

    private void MakeRigidbodiesKinematic(GameObject player)
    {
        Rigidbody[] rigidbodies = player.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in rigidbodies)
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private void SetupCamera(Transform playerTransform)
    {
        if (!assignCameraTargetOnSpawn || playerTransform == null)
            return;

        if (raidCamera == null)
            raidCamera = Camera.main;

        if (cameraFollow == null && raidCamera != null)
            cameraFollow = raidCamera.GetComponent<RaidCameraFollow>();

        if (cameraFollow == null && raidCamera != null && addCameraFollowIfMissing)
            cameraFollow = raidCamera.gameObject.AddComponent<RaidCameraFollow>();

        if (cameraFollow != null)
        {
            if (forceFixedYawCamera)
                cameraFollow.useTargetForward = false;

            cameraFollow.SetTarget(playerTransform);
        }
    }
}
