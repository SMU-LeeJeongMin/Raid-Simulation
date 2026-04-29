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
    [Tooltip("모든 캐릭터 클로즈업 카메라 위치를 한 번에 보정합니다. 카메라 자체를 낮추고 싶으면 Y를 음수로 내리세요.")]
    public Vector3 globalCloseupCameraWorldAdjustment = Vector3.zero;

    [Tooltip("모든 캐릭터의 바라보는 지점을 한 번에 보정합니다. 캐릭터를 화면 위쪽에 놓고 싶으면 Y를 약간 낮춰보세요.")]
    public Vector3 globalCloseupLookAtWorldAdjustment = Vector3.zero;

    [Tooltip("카메라 회전은 유지한 채 화면 구도를 보정합니다. 캐릭터를 화면 위쪽으로 올리고 싶으면 Y를 -0.1 ~ -0.4 정도로 설정하세요.")]
    public Vector3 closeupCameraLocalFramingOffset = Vector3.zero;

    private Coroutine moveRoutine;
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;

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

    private void StartMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveCameraRoutine(targetPosition, targetRotation));
    }

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
