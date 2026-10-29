/*
 * 脚本：MapResearchRoot
 * 功能：在独立样地组织镜头输入、八档平滑观察、物件显示与结构诊断。
 * 边界：不实现人物Motor/攻击，不改世界根/碰撞/TimeScale；仅通过角色窄契约注入正常观察基。
 * 协作：先于CharacterRoot发布本帧实际相机；结构相机只观察冻结的显示，不成为人物瞄准相机。
 * 生命周期：首次在Start等各组件启用后精确绑定；随后重新启用由OnEnable绑定；失焦/禁用精确释放。
 */
using Ossastra.Characters.ViewSpace;
using UnityEngine;

namespace Ossastra.MapResearch
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapResearchCameraInput))]
    public sealed class MapResearchRoot : MonoBehaviour, ICharacterViewContext
    {
        [Header("场景显式装配：不扫描/替换正式场景")]
        public Camera gameplayCamera;
        public Camera observerCamera;
        public MonoBehaviour participantComponent;
        public MapResearchCameraInput cameraInput;
        public ScenePropView[] props;
        public Collider[] collisionVolumes;
        public RectTransform overlayPointerBlocker;
        [Header("正常观察相机；世界物件与角色根不旋转")]
        public bool followParticipant = true;
        public Vector3 fixedTarget;
        public Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);
        public float initialYaw;
        [Range(10f, 80f)] public float pitch = 35f;
        [Min(0.1f)] public float distance = 16f;
        public bool orthographic;
        [Range(10f, 90f)] public float fieldOfView = 35f;
        [Min(0.1f)] public float orthographicSize = 9f;
        [Min(1f)] public float degreesPerSecond = 180f;
        [Header("镜头有限缩放，不改变人物/物件比例")]
        [Min(0.1f)] public float minimumZoom = 5f;
        [Min(0.1f)] public float maximumZoom = 32f;
        [Min(0.1f)] public float zoomSpeed = 8f;
        [Header("结构诊断；网格只是逻辑参考，不代表所有物件吸格")]
        public bool showOverlay = true;
        public Vector3 gridOrigin;
        [Min(0.1f)] public float gridSize = 1f;
        [Range(1, 32)] public int gridExtent = 8;

        private ICharacterViewParticipant participant;
        private MapOrbitState orbit, observerOrbit;
        private CameraState savedGameplay, savedObserver;
        private bool bound, inputOwned, structureMode, observerTop, hasFocus = true;
        private bool started;
        private bool uiToggle, uiSide, uiTop, uiReset;
        private float zoom, observerZoom;
        private Vector3 structureTarget;
        private Vector3 viewRight = Vector3.right, viewForward = Vector3.forward;
        private Quaternion viewRotation = Quaternion.identity;

        public Camera GameplayCamera => gameplayCamera;
        public bool IsValid => isActiveAndEnabled && gameplayCamera != null && orbit != null;
        public Vector3 ViewRight => viewRight;
        public Vector3 ViewForward => viewForward;
        public Quaternion ViewRotation => viewRotation;
        public bool FreezePresentation => structureMode;
        public bool IsStructureMode => structureMode;
        public float ActualYaw => orbit != null ? orbit.ActualYaw : initialYaw;
        public float TargetYaw => orbit != null ? orbit.TargetYaw : initialYaw;
        public int TargetSector => orbit != null ? orbit.TargetSector : 0;

        private struct CameraState
        {
            private Vector3 position;
            private Quaternion rotation;
            private bool enabled, orthographic;
            private float size, fov;
            public static CameraState Capture(Camera camera) => new CameraState
            {
                position = camera.transform.position, rotation = camera.transform.rotation,
                enabled = camera.enabled, orthographic = camera.orthographic,
                size = camera.orthographicSize, fov = camera.fieldOfView
            };
            public void Restore(Camera camera)
            {
                if (camera == null) return;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographic = orthographic; camera.orthographicSize = size;
                camera.fieldOfView = fov; camera.enabled = enabled;
            }
        }

        private void OnEnable()
        {
            hasFocus = Application.isFocused;
            // -300只用于每帧先发布相机基；首次OnEnable会早于默认顺序的角色适配器启用。
            // Start保证所有Awake/OnEnable已完成，不能靠放宽适配器isActiveAndEnabled约束抢先绑定。
            if (started) BindScene();
        }

        private void Start()
        {
            started = true;
            BindScene();
        }

        private void BindScene()
        {
            if (cameraInput == null) cameraInput = GetComponent<MapResearchCameraInput>();
            participant = participantComponent as ICharacterViewParticipant;
            if (gameplayCamera == null || observerCamera == null || gameplayCamera == observerCamera || participant == null || cameraInput == null)
            {
                Debug.LogError("地图研究装配缺失：需要不同的正常/结构相机、角色视角端口及镜头Input组件。", this);
                return;
            }
            savedGameplay = CameraState.Capture(gameplayCamera);
            savedObserver = CameraState.Capture(observerCamera);
            orbit = new MapOrbitState(initialYaw);
            zoom = Mathf.Clamp(orthographic ? orthographicSize : distance, minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
            bound = participant.BindViewContext(this, this);
            if (!bound)
            {
                Debug.LogError("地图研究不能取得角色视角端口：已有其他owner或角色拒绝绑定。", this);
                return;
            }
            inputOwned = cameraInput.Begin(this);
            if (!inputOwned)
            {
                participant.ReleaseViewContext(this);
                bound = false;
                Debug.LogError("地图研究镜头输入已由其他owner持有或Action配置缺失。", this);
                return;
            }
            structureMode = false;
            observerCamera.enabled = false;
            gameplayCamera.enabled = true;
            ApplyGameplayCamera();
            PublishView();
            TickProps();
            UpdatePointerBlocker();
        }

        private void Update()
        {
            if (!bound || !inputOwned || participantComponent == null || gameplayCamera == null || observerCamera == null) return;
            // 本实验没有文本输入框；IMGUI拥有键盘/拖动时不把同一滚轮或快捷键再当镜头指令。
            bool blocked = !hasFocus || GUIUtility.keyboardControl != 0 || GUIUtility.hotControl != 0;
            MapResearchCameraInput.Frame frame = cameraInput.ReadFrame(blocked);
            if ((frame.structure || uiToggle) && hasFocus) SetStructureMode(!structureMode);
            if ((frame.side || uiSide) && hasFocus) { if (SetStructureMode(true)) observerTop = false; }
            if ((frame.top || uiTop) && hasFocus) { if (SetStructureMode(true)) observerTop = true; }
            if ((frame.reset || uiReset) && hasFocus) ResetView();
            uiToggle = uiSide = uiTop = uiReset = false;

            float delta = Time.unscaledDeltaTime;
            float maximum = Mathf.Max(minimumZoom, maximumZoom);
            if (structureMode)
            {
                observerOrbit.RequestStep(frame.rotation);
                observerOrbit.Tick(delta, degreesPerSecond);
                observerZoom = Mathf.Clamp(observerZoom + frame.zoom * zoomSpeed * delta, minimumZoom, maximum);
                ApplyObserverCamera();
                // 结构相机不发布基，也不修改正常相机；纸片继续沿进入结构时的真实显示平面。
            }
            else
            {
                orbit.RequestStep(frame.rotation);
                orbit.Tick(delta, degreesPerSecond);
                zoom = Mathf.Clamp(zoom + frame.zoom * zoomSpeed * delta, minimumZoom, maximum);
                ApplyGameplayCamera();
                PublishView();
            }
            TickProps();
            UpdatePointerBlocker();
        }

        private Vector3 CurrentTarget()
        {
            Transform character = participant != null && participantComponent != null ? participant.CharacterTransform : null;
            return (followParticipant && character != null ? character.position : fixedTarget) + targetOffset;
        }

        private void ApplyGameplayCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, orbit.ActualYaw, 0f);
            float orbitDistance = orthographic ? distance : zoom;
            gameplayCamera.transform.SetPositionAndRotation(CurrentTarget() - rotation * Vector3.forward * orbitDistance, rotation);
            gameplayCamera.orthographic = orthographic;
            gameplayCamera.orthographicSize = zoom;
            gameplayCamera.fieldOfView = fieldOfView;
        }

        private void PublishView()
        {
            viewRotation = gameplayCamera.transform.rotation;
            viewRight = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up).normalized;
            viewForward = Vector3.Cross(viewRight, Vector3.up).normalized;
        }

        private void TickProps()
        {
            if (props == null) return;
            double time = Time.timeAsDouble;
            foreach (ScenePropView prop in props) if (prop != null) prop.TickView(this, time);
        }

        public bool SetStructureMode(bool enabled)
        {
            if (!bound || participantComponent == null || gameplayCamera == null || observerCamera == null) return false;
            if (structureMode == enabled) return true;
            if (enabled)
            {
                // 不停用Root：一次性已受理动作沿原钟结算；依赖Held的持续能力沿原UI捕获规则结束。
                if (!participant.SetViewInputCapture(this, true)) return false;
                structureTarget = CurrentTarget();
                observerOrbit = new MapOrbitState(orbit.ActualYaw + 90f);
                observerZoom = Mathf.Clamp(zoom, minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
                observerTop = false;
                structureMode = true;
                gameplayCamera.enabled = false;
                observerCamera.enabled = true;
                ApplyObserverCamera();
            }
            else
            {
                participant.SetViewInputCapture(this, false);
                structureMode = false;
                observerCamera.enabled = false;
                gameplayCamera.enabled = true;
                ApplyGameplayCamera();
                PublishView();
            }
            if (cameraInput != null) cameraInput.DiscardPending();
            return true;
        }

        private void ApplyObserverCamera()
        {
            Quaternion rotation = Quaternion.Euler(observerTop ? 89.9f : 12f, observerOrbit.ActualYaw, 0f);
            observerCamera.orthographic = true;
            observerCamera.orthographicSize = observerZoom;
            observerCamera.transform.SetPositionAndRotation(structureTarget - rotation * Vector3.forward * distance * 1.5f, rotation);
        }

        public void ResetView()
        {
            if (orbit == null) return;
            if (structureMode)
            {
                observerOrbit = new MapOrbitState(orbit.ActualYaw + 90f);
                observerTop = false;
                observerZoom = Mathf.Clamp(orthographic ? orthographicSize : distance, minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
                ApplyObserverCamera();
            }
            else
            {
                orbit.Reset();
                zoom = Mathf.Clamp(orthographic ? orthographicSize : distance, minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
                ApplyGameplayCamera();
                PublishView();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (cameraInput != null) cameraInput.DiscardPending();
            if (!focused && structureMode) SetStructureMode(false);
        }

        private void Release()
        {
            if (participantComponent != null && participant != null && bound)
            {
                participant.SetViewInputCapture(this, false);
                participant.ReleaseViewContext(this);
            }
            if (cameraInput != null && inputOwned) cameraInput.End(this);
            if (bound) { savedGameplay.Restore(gameplayCamera); savedObserver.Restore(observerCamera); }
            structureMode = false; bound = false; inputOwned = false;
            if (props != null) foreach (ScenePropView prop in props) if (prop != null) prop.RestoreView();
            if (overlayPointerBlocker != null) overlayPointerBlocker.gameObject.SetActive(false);
            uiToggle = uiSide = uiTop = uiReset = false;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void UpdatePointerBlocker()
        {
            if (overlayPointerBlocker == null) return;
            overlayPointerBlocker.gameObject.SetActive(showOverlay && bound);
            overlayPointerBlocker.sizeDelta = new Vector2(Mathf.Min(750f, Screen.width - 24f), structureMode ? 188f : 93f);
        }

        private void OnGUI()
        {
            if (!showOverlay || !bound) return;
            float width = Mathf.Min(750f, Screen.width - 24f);
            GUI.Box(new Rect(12, 12, width, structureMode ? 188f : 93f), GUIContent.none);
            GUI.Label(new Rect(24, 20, width - 24, 22), "饥荒地图研究 · 现有角色控制链  |  观察角 " + ActualYaw.ToString("0.0") + "° → " + TargetYaw.ToString("0") + "°");
            GUI.Label(new Rect(24, 43, width - 24, 22), "滚轮/十字键左右：45°旋转　PageUp/Down：缩放　Home：恢复　Q/E技能与方向键移动保留");
            if (GUI.Button(new Rect(24, 68, 140, 22), structureMode ? "F6 返回正常游玩" : "F6 查看纸片结构")) uiToggle = true;
            if (GUI.Button(new Rect(170, 68, 90, 22), "F7 侧看")) uiSide = true;
            if (GUI.Button(new Rect(266, 68, 90, 22), "F8 俯视")) uiTop = true;
            if (GUI.Button(new Rect(362, 68, 90, 22), "恢复机位")) uiReset = true;
            if (!structureMode) return;
            GUI.Label(new Rect(24, 94, width - 24, 22), "正常相机与显示平面已冻结；滚轮只转结构相机。网格为参考；黄色脚点/占地，青色真实平面，红色碰撞包围框。");
            GUI.Label(new Rect(24, 116, width - 24, 46), participant != null ? participant.ViewDiagnostics : "角色端口已失效");
            GUI.Label(new Rect(24, 163, width - 24, 22), "紫色：人物真实世界方向　白色：正常相机方向；物件来源与比例见Inspector的Scene Prop View。");
            DrawStructure(observerCamera);
        }

        private void DrawStructure(Camera camera)
        {
            if (camera == null || Event.current.type != EventType.Repaint) return;
            int extent = Mathf.Clamp(gridExtent, 1, 32);
            float size = Mathf.Max(0.1f, gridSize);
            for (int i = -extent; i <= extent; i++)
            {
                DrawWorldLine(camera, gridOrigin + new Vector3(i * size, 0.025f, -extent * size), gridOrigin + new Vector3(i * size, 0.025f, extent * size), new Color(0.45f, 0.55f, 0.6f, 0.35f));
                DrawWorldLine(camera, gridOrigin + new Vector3(-extent * size, 0.025f, i * size), gridOrigin + new Vector3(extent * size, 0.025f, i * size), new Color(0.45f, 0.55f, 0.6f, 0.35f));
            }
            DrawWorldLine(camera, gridOrigin, gridOrigin + Vector3.right * 2, Color.red);
            DrawWorldLine(camera, gridOrigin, gridOrigin + Vector3.up * 2, Color.green);
            DrawWorldLine(camera, gridOrigin, gridOrigin + Vector3.forward * 2, Color.blue);
            if (props != null) foreach (ScenePropView prop in props)
            {
                if (prop == null) continue;
                Vector3 foot = prop.transform.position;
                DrawWorldLine(camera, foot - Vector3.right * 0.15f, foot + Vector3.right * 0.15f, Color.yellow);
                DrawWorldLine(camera, foot - Vector3.forward * 0.15f, foot + Vector3.forward * 0.15f, Color.yellow);
                DrawBounds(camera, new Bounds(foot, new Vector3(prop.footprint.x * 2, 0.025f, prop.footprint.y * 2)), Matrix4x4.identity, Color.yellow);
                if (prop.displayPlane == null) continue;
                MeshFilter filter = prop.displayPlane.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) DrawBounds(camera, filter.sharedMesh.bounds, prop.displayPlane.localToWorldMatrix, Color.cyan);
            }
            if (collisionVolumes != null) foreach (Collider volume in collisionVolumes)
                if (volume != null && volume.enabled) DrawBounds(camera, volume.bounds, Matrix4x4.identity, new Color(1f, 0.3f, 0.25f));
            if (participant != null && participantComponent != null && participant.CharacterTransform != null)
            {
                Vector3 foot = participant.CharacterTransform.position;
                DrawWorldLine(camera, foot, foot + participant.LogicalDirection * 2f, Color.magenta);
                DrawWorldLine(camera, foot + Vector3.up * 0.08f, foot + Vector3.up * 0.08f + viewForward * 1.5f, Color.white);
            }
        }

        private static void DrawBounds(Camera camera, Bounds bounds, Matrix4x4 matrix, Color color)
        {
            Vector3 min = bounds.min, max = bounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 a = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                for (int axis = 1; axis <= 4; axis *= 2)
                {
                    int j = i ^ axis;
                    if (j <= i) continue;
                    Vector3 b = new Vector3((j & 1) == 0 ? min.x : max.x, (j & 2) == 0 ? min.y : max.y, (j & 4) == 0 ? min.z : max.z);
                    DrawWorldLine(camera, matrix.MultiplyPoint3x4(a), matrix.MultiplyPoint3x4(b), color);
                }
            }
        }

        private static void DrawWorldLine(Camera camera, Vector3 worldA, Vector3 worldB, Color color)
        {
            Vector3 a = camera.WorldToScreenPoint(worldA), b = camera.WorldToScreenPoint(worldB);
            if (a.z <= 0f || b.z <= 0f) return;
            Vector2 start = new Vector2(a.x, Screen.height - a.y), end = new Vector2(b.x, Screen.height - b.y);
            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            GUI.color = color;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y, Vector2.Distance(start, end), 1f), Texture2D.whiteTexture);
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }
    }
}
