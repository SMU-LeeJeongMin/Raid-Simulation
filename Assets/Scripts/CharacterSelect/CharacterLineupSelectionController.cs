// 캐릭터 선택 화면의 중심 컨트롤러
// 4명의 캐릭터를 생성하고, 마우스 클릭 선택, 카메라 이동, UI 표시, Raid 씬 로드를 관리

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CharacterLineupSelectionController : MonoBehaviour
{
    [Header("Data")]
    public CharacterLineupDatabase database;

    [Header("Lineup")]
    public Transform lineupParent;
    public Transform[] spawnPoints = new Transform[4];

    [Header("References")]
    public Camera selectionCamera;
    public CharacterSelectionCameraRig cameraRig;
    public CharacterSelectionUIController uiController;

    [Header("Click")]
    public LayerMask characterClickMask = ~0;
    public float clickRayDistance = 1000f;
    public bool ignoreClickWhenPointerIsOverUI = true;

    [Header("Scene Load")]
    public string raidSceneName = "Raid";
    public bool loadSceneAsync = true;

    private readonly List<CharacterLineupActor> actors = new List<CharacterLineupActor>();
    private int selectedIndex = -1;
    private Coroutine loadRoutine;

    private void Awake()
    {
        if (selectionCamera == null)
            selectionCamera = Camera.main;

        if (cameraRig == null)
            cameraRig = GetComponent<CharacterSelectionCameraRig>();

        if (uiController == null)
            uiController = GetComponent<CharacterSelectionUIController>();

        if (uiController != null)
            uiController.Initialize(this);
    }

    // 시작 시 캐릭터 4명을 배치하고 전체 라인업 화면 상태로 초기화
    private void Start()
    {
        SpawnLineupCharacters();
        GoBackToLineup();
    }

    // 매 프레임 마우스 클릭을 확인하고, 클릭된 캐릭터가 있으면 선택 처리
    private void Update()
    {
        if (WasLeftMouseButtonPressed(out Vector2 screenPosition))
            TrySelectActorFromScreenPosition(screenPosition);
    }

    // New Input System 또는 Legacy Input에서 왼쪽 마우스 클릭을 감지
    private bool WasLeftMouseButtonPressed(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
            return false;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return false;

        screenPosition = Mouse.current.position.ReadValue();
        return true;
#else
        if (!Input.GetMouseButtonDown(0))
            return false;

        screenPosition = Input.mousePosition;
        return true;
#endif
    }

    // 화면 좌표에서 Raycast를 쏴서 클릭한 캐릭터를 찾기
    private void TrySelectActorFromScreenPosition(Vector2 screenPosition)
    {
        if (selectionCamera == null)
            return;

        if (ignoreClickWhenPointerIsOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = selectionCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickRayDistance, characterClickMask, QueryTriggerInteraction.Ignore))
            return;

        CharacterLineupActor actor = hit.collider.GetComponentInParent<CharacterLineupActor>();
        if (actor == null)
            return;

        SelectActor(actor);
    }


    // DB에 등록된 캐릭터 프리팹을 SpawnPoint 위치에 생성
    private void SpawnLineupCharacters()
    {
        ClearSpawnedActors();

        if (database == null)
        {
            Debug.LogError("[CharacterLineupSelectionController] Database is not assigned.");
            return;
        }

        int count = Mathf.Min(database.Count, spawnPoints == null ? 0 : spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            CharacterLineupEntry entry = database.Get(i);
            Transform spawnPoint = spawnPoints[i];

            if (entry == null || entry.characterPrefab == null || spawnPoint == null)
            {
                Debug.LogWarning($"[CharacterLineupSelectionController] Missing entry, prefab, or spawn point at index {i}.");
                continue;
            }

            Vector3 position = spawnPoint.position + spawnPoint.TransformVector(entry.lineupPositionOffset);
            Quaternion rotation = spawnPoint.rotation * Quaternion.Euler(entry.lineupEulerOffset);

            GameObject instance = Instantiate(entry.characterPrefab, position, rotation, lineupParent);
            instance.name = $"Lineup_{i}_{entry.jobName}";

            if (entry.lineupScale != Vector3.zero)
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, entry.lineupScale);

            CharacterLineupActor actor = instance.GetComponent<CharacterLineupActor>();
            if (actor == null)
                actor = instance.AddComponent<CharacterLineupActor>();

            actor.Initialize(i, entry);
            actors.Add(actor);
        }
    }

    // 기존에 생성된 라인업 캐릭터들을 제거
    private void ClearSpawnedActors()
    {
        for (int i = actors.Count - 1; i >= 0; i--)
        {
            if (actors[i] != null)
                Destroy(actors[i].gameObject);
        }

        actors.Clear();
    }

    // 클릭된 캐릭터를 현재 선택 캐릭터로 설정
    private void SelectActor(CharacterLineupActor selectedActor)
    {
        if (selectedActor == null || selectedActor.Entry == null)
            return;

        selectedIndex = selectedActor.Index;

        foreach (CharacterLineupActor actor in actors)
        {
            if (actor == null)
                continue;

            if (actor == selectedActor)
                actor.PlaySelected();
            else
                actor.PlayRest();
        }

        if (cameraRig != null)
            cameraRig.MoveToCharacter(selectedActor);

        if (uiController != null)
            uiController.ShowSelectedState(selectedActor.Entry);
    }

    // Back 버튼
    public void GoBackToLineup()
    {
        selectedIndex = -1;

        foreach (CharacterLineupActor actor in actors)
        {
            if (actor != null)
                actor.PlayRest();
        }

        if (cameraRig != null)
            cameraRig.MoveToLineup();

        if (uiController != null)
            uiController.HideSelectedState();
    }

    // Select 버튼
    public void ConfirmSelectionAndLoadRaid()
    {
        if (database == null || !database.IsValidIndex(selectedIndex))
        {
            Debug.LogWarning("[CharacterLineupSelectionController] No character selected.");
            return;
        }

        CharacterLineupEntry entry = database.Get(selectedIndex);
        SelectedCharacterMemory.Save(selectedIndex, entry.characterId, entry.jobName);

        if (string.IsNullOrEmpty(raidSceneName))
        {
            Debug.LogError("[CharacterLineupSelectionController] Raid scene name is empty.");
            return;
        }

        if (loadRoutine != null)
            return;

        if (loadSceneAsync)
            loadRoutine = StartCoroutine(LoadRaidSceneRoutine());
        else
            SceneManager.LoadScene(raidSceneName);
    }

    // Raid 씬을 비동기로 로드
    private IEnumerator LoadRaidSceneRoutine()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(raidSceneName);
        if (operation == null)
        {
            Debug.LogError($"[CharacterLineupSelectionController] Could not load scene: {raidSceneName}");
            loadRoutine = null;
            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }
}
