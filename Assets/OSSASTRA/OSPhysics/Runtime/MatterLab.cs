// 职责：维护实验室的固定步进、相机和 Input System 画笔，向旧视图及手绘视图发布同一状态。
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.OSPhysics
{
    public enum MatterLabPreset { Empty, Flow, Layering, Combustion, AssetStudy, AssetEmpty }
    public enum MatterBrush { Water, Oil, Sand, Grass, Wood, Heat, Erase, Barrier }

    [DisallowMultipleComponent]
    public sealed partial class MatterLab : MonoBehaviour
    {
        private const int Width = 256;
        private const int Height = 128;
        private static readonly Vector2 Origin = new Vector2(-12.8f, -6.4f);
        private const float CellSize = 0.1f;

        [SerializeField] private Camera labCamera;
        [SerializeField] private MatterCatalogAsset catalog;
        [SerializeField] private MatterLabPreset opening = MatterLabPreset.Flow;
        [SerializeField] private int seed = 6821;

        private MatterVisual visual;
        private MatterLabPreset currentPreset;
        private MatterDisplay display = MatterDisplay.Painted;
        private MatterPaintStyle paintStyle = MatterPaintStyle.Ruins;
        private MatterPaintLayers paintLayers = MatterPaintLayers.All;
        private float paintMood = 1, waterOpacity = 1, effectGain = 1, lampX;
        private bool privateArt;

        public MatterPaintStyle PaintStyle => paintStyle;
        public MatterPaintLayers PaintLayers => paintLayers;
        public MatterVisual Visual => visual;

        public void SetPaintStyle(MatterPaintStyle style) => paintStyle = style;
        public void SetPrivateArt(bool enabled) => privateArt = enabled;
        public void SetPaintMood(float value) => paintMood = Mathf.Clamp(value, 0, 2);
        public void SetPreviewLamp(float worldX) => lampX = worldX;
        public void TogglePaintLayer(MatterPaintLayers layer) => paintLayers ^= layer;
        private MatterBrush brush = MatterBrush.Water;
        private Func<int, int, int, int> strokeStamp;
        private double simDebt;
        private double brushDebt;
        private float speed = 1;
        private bool running = true;
        private bool worldGesture;
        private bool paintHeld;
        private bool pendingStroke;
        private bool strokeHasStart;
        private Vector2Int strokeTarget;
        private Vector2Int previousStamp;
        private Vector2 lastScreenSize;
        private string feedback;
        private float feedbackUntil;
        private float fps;
        private float actualStepRate;
        private int brushRadius = 4;
        private int brushStrength = 160;

        public MatterWorld World { get; private set; }
        public bool Ready => World != null;
        public bool Running
        {
            get => running;
            set
            {
                if (running == value) return;
                running = value;
                simDebt = brushDebt = 0;
            }
        }
        public bool GateOpen { get; private set; }
        public MatterDisplay Display => display;
        public MatterBrush Brush => brush;
        public MatterLabPreset Preset => currentPreset;
        public int PaintedBarrierCount { get; private set; }
        public event Action<MatterMetrics> Stepped;

        public void Configure(Camera camera, MatterCatalogAsset materialCatalog = null)
        {
            if (Ready) throw new InvalidOperationException("Configure the lab before entering Play.");
            labCamera = camera;
            catalog = materialCatalog;
        }

        private void Awake()
        {
            if (labCamera == null) labCamera = Camera.main;
            if (labCamera == null)
            {
                Debug.LogError("物质实验室缺少实验相机。", this);
                enabled = false;
                return;
            }

            World = new MatterWorld(Width, Height, seed, catalog == null ? null : catalog.BuildSettings());
            strokeStamp = StampSelected;
            BuildTerrain();
            var view = new GameObject("Matter View");
            view.transform.SetParent(transform, false);
            visual = view.AddComponent<MatterVisual>();
            visual.Bind(World, Origin, CellSize);
            SelectPreset(opening);
            FrameWorld();
        }

        private void Update()
        {
            // 编辑器运行态重载会清空未序列化的网格。重新进入 Play 之前暂停驱动。
            if (!Ready || visual == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 1f - Mathf.Exp(-dt * 3));
            if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height) FrameWorld();
            ReadControls(dt);

            int steps = MatterLabClock.Take(dt, speed, Running, out simDebt, simDebt);
            if (dt > 0f)
                actualStepRate = Running ? Mathf.Lerp(actualStepRate, steps / dt, 1f - Mathf.Exp(-dt * 2)) : 0;
            for (int i = 0; i < steps; i++)
            {
                PaintTick();
                Advance();
            }

            if (!Running && MatterLabClock.Take(dt, 1, pendingStroke || paintHeld, out brushDebt, brushDebt) > 0)
                PaintTick();
            if (barrierColliderDirty) RefreshPaintedCollider();
            // 以固定步 Tick 作为纹理和火烟的时钟，暂停后切图仍是同一时刻。
            float appearanceTime = (float)(World.Tick * MatterLabClock.Interval);
            visual.Refresh(display, appearanceTime);
            visual.PresentPainted(labCamera, display, paintStyle, privateArt, paintLayers, appearanceTime,
                paintMood, waterOpacity, effectGain, lampX);
            PresentAssets();
        }

        public void StepOnce()
        {
            if (!Ready || Running) return;
            PaintTick();
            Advance();
        }

        private void Advance()
        {
            World.Step();
            assetStudy?.Fuel.UpdateDamage();
            Stepped?.Invoke(World.Metrics);
        }

        public void SetDisplay(MatterDisplay value)
        {
            if (display != value) display = value;
        }

        public bool WorldToCell(Vector2 point, out int x, out int y)
        {
            Vector2 grid = (point - Origin) / CellSize;
            x = Mathf.FloorToInt(grid.x);
            y = Mathf.FloorToInt(grid.y);
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public Vector2 CellToWorld(int x, int y) => Origin + new Vector2(x + .5f, y + .5f) * CellSize;

        public bool TryScreenPoint(Vector2 pixels, out Vector2 worldPoint)
        {
            var ray = labCamera.ScreenPointToRay(pixels);
            if (Mathf.Abs(ray.direction.z) < 1e-6f)
            {
                worldPoint = default;
                return false;
            }
            worldPoint = ray.GetPoint(-ray.origin.z / ray.direction.z);
            return true;
        }

        public int ApplyBrush(Vector2 worldPoint, MatterBrush tool)
        {
            if (!Ready || !WorldToCell(worldPoint, out int x, out int y)) return 0;
            int accepted = Stamp(x, y, tool, Strength(tool));
            if (tool == MatterBrush.Erase) assetStudy?.Fuel.UpdateDamage();
            if (barrierColliderDirty) RefreshPaintedCollider();
            return accepted;
        }

        private void FrameWorld()
        {
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            float usableFraction = Mathf.Clamp01((PanelRect.xMin - 20f) / Mathf.Max(1, Screen.width));
            float span = Mathf.Max(0.24f, usableFraction);
            labCamera.orthographic = true;
            labCamera.orthographicSize = Mathf.Max(Height * CellSize * .57f,
                Width * CellSize * .52f / (Mathf.Max(.1f, labCamera.aspect) * span));
            labCamera.orthographicSize = Mathf.Min(28f, labCamera.orthographicSize);
            float worldHalfWidth = labCamera.orthographicSize * labCamera.aspect;
            labCamera.transform.position = new Vector3((1f - span) * worldHalfWidth, 0, -10);
        }

        private void ReadControls(float dt)
        {
            paintHeld = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (assetBrush != MatterAssetBrush.Matter)
                    for (int i = 0; i < 8; i++)
                        if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                        {
                            SetAssetBrush(MatterAssetBrush.Matter);
                            break;
                        }
                if (keyboard.digit1Key.wasPressedThisFrame) brush = MatterBrush.Water;
                else if (keyboard.digit2Key.wasPressedThisFrame) brush = MatterBrush.Oil;
                else if (keyboard.digit3Key.wasPressedThisFrame) brush = MatterBrush.Sand;
                else if (keyboard.digit4Key.wasPressedThisFrame) brush = MatterBrush.Grass;
                else if (keyboard.digit5Key.wasPressedThisFrame) brush = MatterBrush.Wood;
                else if (keyboard.digit6Key.wasPressedThisFrame) brush = MatterBrush.Heat;
                else if (keyboard.digit7Key.wasPressedThisFrame) brush = MatterBrush.Erase;
                else if (keyboard.digit8Key.wasPressedThisFrame) brush = MatterBrush.Barrier;
                if (keyboard.spaceKey.wasPressedThisFrame) Running = !Running;
                if (keyboard.nKey.wasPressedThisFrame) StepOnce();
                if (keyboard.gKey.wasPressedThisFrame) ToggleGate();
                if (keyboard.homeKey.wasPressedThisFrame) FrameWorld();

                Vector2 pan = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                labCamera.transform.position += (Vector3)(pan.normalized * (labCamera.orthographicSize * dt));
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pixels = mouse.position.ReadValue();
            Vector2 guiPoint = new Vector2(pixels.x, Screen.height - pixels.y);
            bool insidePanel = PanelRect.Contains(guiPoint);
            bool onScreen = pixels.x >= 0 && pixels.y >= 0 && pixels.x < Screen.width && pixels.y < Screen.height;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                worldGesture = onScreen && !insidePanel;
                strokeHasStart = false;
                if (insidePanel)
                {
                    pendingStroke = false;
                    HandlePanelClick(guiPoint);
                }
            }
            if (mouse.leftButton.isPressed && activeSlider.HasValue) UpdateSlider(guiPoint);
            if (!mouse.leftButton.isPressed) activeSlider = null;
            if (!insidePanel && onScreen && mouse.middleButton.isPressed)
            {
                float unitsPerPixel = 2 * labCamera.orthographicSize / Mathf.Max(1, Screen.height);
                labCamera.transform.position -= (Vector3)(mouse.delta.ReadValue() * unitsPerPixel);
            }

            if (!insidePanel && onScreen)
            {
                Zoom(pixels, mouse.scroll.ReadValue().y);
            }
            if (HandleAssetInput(pixels, !insidePanel && onScreen, mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame)) return;
            if (!insidePanel && onScreen)
            {
                if (worldGesture && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame) &&
                    TryScreenPoint(pixels, out Vector2 position) && WorldToCell(position, out int x, out int y))
                {
                    strokeTarget = new Vector2Int(x, y);
                    if (!strokeHasStart)
                    {
                        previousStamp = strokeTarget;
                        strokeHasStart = true;
                    }
                    pendingStroke = true;
                    paintHeld = mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame;
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        // Commit a tap even when a press and release fit between two solver ticks.
                        brushDebt = 0;
                        PaintTick();
                    }
                }
                else if (worldGesture && mouse.leftButton.isPressed) strokeHasStart = false;
            }
            if (insidePanel || !onScreen)
            {
                pendingStroke = false;
                strokeHasStart = false;
            }
            if (!mouse.leftButton.isPressed) worldGesture = false;
        }

        private void Zoom(Vector2 pixels, float delta)
        {
            if (Mathf.Approximately(delta, 0f) || !TryScreenPoint(pixels, out Vector2 before)) return;
            float steps = Mathf.Clamp(delta / 120f, -2f, 2f);
            labCamera.orthographicSize = Mathf.Clamp(labCamera.orthographicSize * Mathf.Pow(.85f, steps), 1f, 28f);
            if (TryScreenPoint(pixels, out Vector2 after))
                labCamera.transform.position += (Vector3)(before - after);
        }

        private void PaintTick()
        {
            if (!pendingStroke && !paintHeld) return;
            MatterStroke.Trace(previousStamp.x, previousStamp.y, strokeTarget.x, strokeTarget.y,
                Mathf.Max(1, brushRadius / 2), Strength(brush), strokeStamp);
            if (brush == MatterBrush.Erase) assetStudy?.Fuel.UpdateDamage();
            previousStamp = strokeTarget;
            pendingStroke = false;
        }

        private int Strength(MatterBrush tool) => tool == MatterBrush.Heat ? brushStrength * 4 : brushStrength;

        private int StampSelected(int x, int y, int amount) => Stamp(x, y, brush, amount);

        private int Stamp(int x, int y, MatterBrush tool, int amount)
        {
            if (amount <= 0) return 0;
            int accepted = 0;
            int radius = brushRadius - 1;
            for (int py = Mathf.Max(0, y - radius); py <= Mathf.Min(Height - 1, y + radius); py++)
            for (int px = Mathf.Max(0, x - radius); px <= Mathf.Min(Width - 1, x + radius); px++)
            {
                int dx = px - x;
                int dy = py - y;
                if (dx * dx + dy * dy > radius * radius) continue;
                switch (tool)
                {
                    case MatterBrush.Heat:
                        accepted += World.AddHeat(px, py, amount);
                        break;
                    case MatterBrush.Erase:
                        accepted += World.Remove(px, py, amount);
                        if (PaintBarrier(px, py, false)) accepted++;
                        break;
                    case MatterBrush.Barrier:
                        if (PaintBarrier(px, py, true)) accepted++;
                        break;
                    default:
                        if ((tool == MatterBrush.Grass || tool == MatterBrush.Wood) &&
                            assetStudy != null && assetStudy.Fuel.Owns(px, py)) break;
                        accepted += World.Add(px, py, (Matter)((int)tool + (int)Matter.Water), amount);
                        break;
                }
            }
            return accepted;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            assetGesture = assetRectangle = assetHasPoint = false;
            pendingStroke = paintHeld = strokeHasStart = worldGesture = false;
            activeSlider = null;
        }

        private void Notify(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + 4;
        }
    }
}
