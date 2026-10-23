using UnityEngine;
using Cinemachine;

[DisallowMultipleComponent]
public class CharacterCameraTargetSystem : MonoBehaviour, ICharacterSubsystem
{
    [Header("相机目标点")]
    [SerializeField] private Transform cameraTarget;

    [Header("运行时自动寻找场景 Virtual Camera")]
    [SerializeField] private bool autoFindSceneVirtualCamera = true;

    [Tooltip("可选：如果场景里有多个 Virtual Camera，可以填名字，比如 Virtual Camera")]
    [SerializeField] private string virtualCameraName = "Virtual Camera";

    [SerializeField] private bool autoBindVirtualCameraFollow = true;

    [Header("基础偏移")]
    [SerializeField] private Vector3 baseLocalOffset = new Vector3(0f, 1f, 0f);

    [Header("移动前置")]
    [SerializeField] private bool useMoveLookAhead = true;
    [SerializeField] private float moveLookAheadX = 0.8f;
    [SerializeField] private float moveLookAheadY = 0.2f;

    [Header("瞄准偏移")]
    [SerializeField] private bool useAimOffset = true;
    [SerializeField] private float aimOffsetDistance = 1.5f;
    [SerializeField] private float maxAimOffsetDistance = 2f;

    [Header("平滑")]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("调试")]
    [SerializeField] private bool debugLog;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;

    private CinemachineVirtualCamera sceneVirtualCamera;

    private Vector3 localVelocity;
    private bool initialized;

    public void Initialize(CharacterRoot root)
    {
        characterRoot = root;
        dataBus = root.characterDataBus;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterCameraTargetSystem] CharacterRoot 为空", this);
            return;
        }

        if (dataBus == null)
        {
            Debug.LogError("[CharacterCameraTargetSystem] CharacterDataBus 为空", this);
            return;
        }

        EnsureCameraTarget();

        if (autoFindSceneVirtualCamera)
        {
            sceneVirtualCamera = FindSceneVirtualCamera();
        }

        if (autoBindVirtualCameraFollow)
        {
            BindVirtualCameraFollow();
        }

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || characterRoot == null || dataBus == null || cameraTarget == null)
            return;

        UpdateCameraTarget();

        // 防御：如果运行中虚拟相机晚于角色生成，或者 Follow 被清空，则再次尝试绑定
        if (autoBindVirtualCameraFollow)
        {
            if (sceneVirtualCamera == null)
                sceneVirtualCamera = FindSceneVirtualCamera();

            if (sceneVirtualCamera != null && sceneVirtualCamera.Follow != cameraTarget)
                BindVirtualCameraFollow();
        }
    }

    private void EnsureCameraTarget()
    {
        if (cameraTarget != null)
            return;

        Transform existTarget = transform.Find("CameraTarget");

        if (existTarget != null)
        {
            cameraTarget = existTarget;
            return;
        }

        GameObject targetObj = new GameObject("CameraTarget");
        targetObj.transform.SetParent(transform);
        targetObj.transform.localPosition = baseLocalOffset;
        targetObj.transform.localRotation = Quaternion.identity;
        targetObj.transform.localScale = Vector3.one;

        cameraTarget = targetObj.transform;
    }

    private CinemachineVirtualCamera FindSceneVirtualCamera()
    {
        CinemachineVirtualCamera[] vcams = FindObjectsOfType<CinemachineVirtualCamera>(true);

        if (vcams == null || vcams.Length == 0)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] 场景中没有找到 CinemachineVirtualCamera", this);

            return null;
        }

        // 1. 优先按名字找
        if (!string.IsNullOrEmpty(virtualCameraName))
        {
            foreach (var vcam in vcams)
            {
                if (vcam == null)
                    continue;

                if (!IsSceneObject(vcam.gameObject))
                    continue;

                if (vcam.name == virtualCameraName)
                {
                    if (debugLog)
                        Debug.Log($"[CharacterCameraTargetSystem] 按名字找到 Virtual Camera: {vcam.name}", vcam);

                    return vcam;
                }
            }
        }

        // 2. 找优先级最高的场景 Virtual Camera
        CinemachineVirtualCamera best = null;
        int bestPriority = int.MinValue;

        foreach (var vcam in vcams)
        {
            if (vcam == null)
                continue;

            if (!IsSceneObject(vcam.gameObject))
                continue;

            if (!vcam.gameObject.activeInHierarchy)
                continue;

            if (vcam.Priority > bestPriority)
            {
                best = vcam;
                bestPriority = vcam.Priority;
            }
        }

        if (best != null && debugLog)
            Debug.Log($"[CharacterCameraTargetSystem] 自动选择 Virtual Camera: {best.name}", best);

        return best;
    }

    private bool IsSceneObject(GameObject obj)
    {
        if (obj == null)
            return false;

        // Project 里的 Prefab 资产没有有效加载场景
        return obj.scene.IsValid() && obj.scene.isLoaded;
    }

    private void BindVirtualCameraFollow()
    {
        if (sceneVirtualCamera == null)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] sceneVirtualCamera 为空，无法绑定 Follow", this);

            return;
        }

        if (cameraTarget == null)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] cameraTarget 为空，无法绑定 Follow", this);

            return;
        }

        sceneVirtualCamera.Follow = cameraTarget;
        sceneVirtualCamera.LookAt = null;

        if (debugLog)
        {
            Debug.Log(
                $"[CharacterCameraTargetSystem] 已绑定 {sceneVirtualCamera.name}.Follow = {cameraTarget.name}",
                sceneVirtualCamera
            );
        }
    }

    private void UpdateCameraTarget()
    {
        Vector3 desiredLocalPosition = baseLocalOffset;

        desiredLocalPosition += GetMoveLookAheadOffset();
        desiredLocalPosition += GetAimOffset();

        cameraTarget.localPosition = Vector3.SmoothDamp(
            cameraTarget.localPosition,
            desiredLocalPosition,
            ref localVelocity,
            smoothTime
        );

        cameraTarget.localRotation = Quaternion.identity;
    }

    private Vector3 GetMoveLookAheadOffset()
    {
        if (!useMoveLookAhead)
            return Vector3.zero;

        Vector2 moveInput = dataBus.moveInput;

        if (moveInput.sqrMagnitude < 0.01f)
            return Vector3.zero;

        return new Vector3(
            moveInput.x * moveLookAheadX,
            moveInput.y * moveLookAheadY,
            0f
        );
    }

    private Vector3 GetAimOffset()
    {
        if (!useAimOffset)
            return Vector3.zero;

        if (!dataBus.isAiming)
            return Vector3.zero;

        Vector3 aimDir = dataBus.aimDirection;
        aimDir.z = 0f;

        if (aimDir.sqrMagnitude < 0.001f)
            return Vector3.zero;

        aimDir.Normalize();

        Vector3 offset = aimDir * aimOffsetDistance;

        if (offset.magnitude > maxAimOffsetDistance)
            offset = offset.normalized * maxAimOffsetDistance;

        return offset;
    }

    public Transform GetCameraTarget()
    {
        return cameraTarget;
    }
}