// 캐릭터 선택 화면의 카메라 이동

using System.Collections;
using UnityEngine;

public class CharacterSelectionCameraRig : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public Transform lineupCameraPoint;

    [Header("Movement")]
    public float moveDuration = 0.65f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Close-up Fine Tuning")]
    public Vector3 globalCloseupCameraWorldAdjustment = Vector3.zero;
    public Vector3 globalCloseupLookAtWorldAdjustment = Vector3.zero;
    public Vector3 closeupCameraLocalFramingOffset = Vector3.zero;

    private Coroutine moveRoutine;
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;

    // 카메라 참조를 찾고, 라인업 포인트가 없을 때를 대비해 초기 카메라 위치를 저장
    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            initialCameraPosition = targetCamera.transform.position;
            initialCameraRotation = targetCamera.transform.rotation;
        }
    }

    // 카메라를 4명의 캐릭터가 모두 보이는 라인업 위치로 이동
    public void MoveToLineup()
    {
        if (targetCamera == null)
            return;

        Vector3 targetPosition = lineupCameraPoint != null
            ? lineupCameraPoint.position
            : initialCameraPosition;

        Quaternion targetRotation = lineupCameraPoint != null
            ? lineupCameraPoint.rotation
            : initialCameraRotation;

        StartMove(targetPosition, targetRotation);
    }

    // 선택된 캐릭터 기준으로 클로즈업 카메라 위치와 회전을 계산해 이동
    // DB의 closeup offset과 Rig의 보정값을 함께 사용
    public void MoveToCharacter(CharacterLineupActor actor)
    {
        if (targetCamera == null || actor == null || actor.Entry == null)
            return;

        CharacterLineupEntry entry = actor.Entry;
        Vector3 targetPosition = actor.transform.position + entry.closeupCameraWorldOffset + globalCloseupCameraWorldAdjustment;
        Vector3 lookAtPosition = actor.transform.position + entry.closeupLookAtWorldOffset + globalCloseupLookAtWorldAdjustment;

        Vector3 direction = lookAtPosition - targetPosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = actor.transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        targetPosition += targetRotation * closeupCameraLocalFramingOffset;

        StartMove(targetPosition, targetRotation);
    }

    // 이미 진행 중인 카메라 이동이 있으면 중단하고 새 이동을 시작
    private void StartMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveCameraRoutine(targetPosition, targetRotation));
    }

    // Lerp/Slerp를 사용해 카메라 위치와 회전을 부드럽게 보간
    private IEnumerator MoveCameraRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        Transform cameraTransform = targetCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;

        float duration = Mathf.Max(0.01f, moveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = moveCurve != null ? moveCurve.Evaluate(t) : t;

            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

            yield return null;
        }

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
        moveRoutine = null;
    }
}
