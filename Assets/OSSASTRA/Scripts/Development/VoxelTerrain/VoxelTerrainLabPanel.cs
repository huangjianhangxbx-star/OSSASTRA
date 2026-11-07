/* 功能：已烘焙体素样板的定位、总览和来源统计入口。
 * 边界：只调用现有 RoomVisibilityLabPanel.PlaceCurrent 与 MapResearchRoot，不造角色控制器或地形更新器。
 * 输入：两个角色的指针区域和协调者/镜头捕获均按本窗口 owner 管理；失焦/隐藏/禁用精确释放。
 * 生命周期：不暂停世界、不改角度、动作、照明或存档；总览退出恢复进入前的镜头距离与缩放上限。
 */
using System;
using System.Collections.Generic;
using Ossastra.MapResearch;
using UnityEngine;
using UnityEngine.InputSystem;
using Coordinator = Ossastra.Characters.CharacterControlCoordinator;
using CharacterInput = Ossastra.Characters.CharacterInputHandler;
using Installation = Ossastra.VoxelTerrain.VoxelTerrainInstallation;
using Source = Ossastra.VoxelTerrain.VoxelTerrainSource;
using Navigation = Ossastra.Development.SceneVisibility.RoomVisibilityLabPanel;

namespace Ossastra.Development.VoxelTerrain
{
    [DefaultExecutionOrder(-220), DisallowMultipleComponent]
    public sealed class VoxelTerrainLabPanel : MonoBehaviour
    {
        public Installation installation;
        public Navigation navigation;
        public MapResearchRoot map;
        public Coordinator coordinator;
        public bool showPanel = true;
        [SerializeField] private bool expanded;
        public bool Expanded => expanded;
        public bool IsOverview => overviewMap != null;
        public bool IsCapturingInput => captured;
        public string Status { get; private set; } = "定位只用于样板检查；正常体验请自行行走。";
        public Rect PanelRect => new Rect(12, 70, Mathf.Min(350, Mathf.Max(180, Screen.width - 24)), expanded ? 290 : 34);

        private Coordinator regionCoordinator, capturedCoordinator;
        private MapResearchRoot capturedMap, overviewMap;
        private readonly List<CharacterInput> registeredInputs = new List<CharacterInput>();
        private bool captured, dragging, focusAllowed = true;
        private float savedDistance, savedMaximumZoom;
        private Vector3 savedFixedTarget;
        private Vector2 scroll;
        private GUIStyle wrappingLabel;
        private Installation statisticsInstallation;
        private TextAsset statisticsSource, statisticsSurface;
        private string statistics = "未读取源统计。";

        public bool GoTo(int index)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || installation == null || navigation == null || coordinator == null || navigation.visibility == null || navigation.visibility.coordinator != coordinator)
                return Refuse("定位未接线：需要体素装配与原房间导航入口。");
            Vector3 position;
            switch (index)
            {
                case 0: position = installation.arrival; break;
                case 1:
                    if (installation.timberHouse == null) return Refuse("未登记木屋定位点。");
                    position = installation.timberHouse.position + Vector3.up * .15f; break;
                case 2: position = installation.hillA; break;
                case 3: position = installation.hillB; break;
                case 4: position = installation.pond + Vector3.back * 3 + Vector3.up * .6f; break;
                default: return Refuse("定位编号无效。");
            }
            if (!Finite(position)) return Refuse("定位坐标无效。");
            bool placed = navigation.PlaceCurrent(position);
            Status = placed ? "已到达样板点位 " + (index + 1) + "；当前仍走原角色控制链。" : "原角色导航拒绝定位：请检查主控存活与显式接线。";
            return placed;
        }

        public bool ViewAll()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || map == null || !map.IsValid)
                return Refuse("总览未接线：需要原地图镜头。");
            if (overviewMap != map)
            {
                RestoreOverview();
                overviewMap = map; savedDistance = map.distance; savedMaximumZoom = map.maximumZoom; savedFixedTarget = map.fixedTarget;
            }
            map.followParticipant = false; map.fixedTarget = Vector3.zero; map.distance = 90; map.maximumZoom = 110;
            map.ResetView(); Status = "地图总览；返回跟随会恢复刚才的距离与缩放上限。"; return true;
        }

        public bool Follow()
        {
            if (overviewMap != null) { RestoreOverview(); Status = "已恢复原距离并返回主控跟随。"; return true; }
            if (!Application.isPlaying || !isActiveAndEnabled || map == null || !map.IsValid)
                return Refuse("跟随未接线：需要原地图镜头。");
            map.followParticipant = true; map.ResetView(); Status = "已返回主控跟随。"; return true;
        }

        public void SetExpanded(bool value)
        {
            expanded = value;
            if (!value) { dragging = false; Capture(false); }
            RegisterRegions(Application.isPlaying && isActiveAndEnabled && showPanel && focusAllowed);
        }

        private void RestoreOverview()
        {
            var owned = overviewMap; overviewMap = null;
            if (owned == null) return;
            owned.distance = savedDistance; owned.maximumZoom = savedMaximumZoom; owned.fixedTarget = savedFixedTarget;
            owned.followParticipant = true;
            if (owned.IsValid) owned.ResetView();
        }
        private bool Refuse(string message) { Status = message; return false; }
        private static bool Finite(Vector3 p) { return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)); }

        private void Update()
        {
            bool visible = Application.isPlaying && isActiveAndEnabled && showPanel && focusAllowed;
            RegisterRegions(visible);
            if (!visible) { dragging = false; Capture(false); return; }
            var pointer = Pointer.current;
            Vector2 screen = pointer == null ? new Vector2(-1, -1) : pointer.position.ReadValue();
            bool hover = pointer != null && PanelRect.Contains(new Vector2(screen.x, Screen.height - screen.y));
            bool pressed = pointer != null && pointer.press.isPressed;
            if (hover && pressed) dragging = true;
            if (!pressed) dragging = false;
            if (captured && (capturedCoordinator != coordinator || capturedMap != map)) Capture(false);
            Capture(hover || dragging);
        }

        private void RegisterRegions(bool add)
        {
            if (!add || regionCoordinator != coordinator)
            {
                foreach (var input in registeredInputs) if (input != null) input.RemovePointerRegion(this);
                registeredInputs.Clear(); regionCoordinator = null;
            }
            if (!add || coordinator == null) return;
            if (regionCoordinator == null)
            {
                regionCoordinator = coordinator;
                foreach (var actor in coordinator.Characters)
                    if (actor != null) { var input = actor.GetComponent<CharacterInput>(); if (input != null) registeredInputs.Add(input); }
            }
            foreach (var input in registeredInputs) if (input != null) input.RegisterPointerRegion(this, PanelRect);
        }

        private void Capture(bool value)
        {
            if (captured == value) return;
            if (!value)
            {
                if (capturedCoordinator != null) capturedCoordinator.SetInputCapture(this, false);
                if (capturedMap != null) capturedMap.SetUiCameraCapture(this, false);
                capturedCoordinator = null; capturedMap = null; captured = false; return;
            }
            capturedCoordinator = coordinator; capturedMap = map;
            if (capturedCoordinator != null) capturedCoordinator.SetInputCapture(this, true);
            if (capturedMap != null) capturedMap.SetUiCameraCapture(this, true);
            captured = true;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showPanel || !isActiveAndEnabled || !focusAllowed) return;
            if (wrappingLabel == null) wrappingLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var area = PanelRect;
            GUILayout.BeginArea(area, GUI.skin.box);
            if (GUILayout.Button((expanded ? "▼ " : "▶ ") + "体素地图样板", GUILayout.Height(25))) SetExpanded(!expanded);
            if (expanded)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("房屋门外")) GoTo(0);
                if (GUILayout.Button("木屋室内")) GoTo(1);
                GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
                if (GUILayout.Button("小坡 A")) GoTo(2);
                if (GUILayout.Button("小坡 B")) GoTo(3);
                if (GUILayout.Button("水洼岸边")) GoTo(4);
                GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
                if (GUILayout.Button("地图总览")) ViewAll();
                if (GUILayout.Button("恢复跟随")) Follow();
                GUILayout.EndHorizontal();
                EnsureStatistics(); GUILayout.Label(statistics, wrappingLabel);
                GUILayout.Label(Status, wrappingLabel);
                GUILayout.Label("只切观察与样板定位，世界持续运行。", wrappingLabel);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            var current = Event.current;
            if (area.Contains(current.mousePosition) && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.ScrollWheel)) current.Use();
        }

        private void EnsureStatistics()
        {
            var author = installation == null ? null : installation.editableSource;
            var surface = installation == null ? null : installation.frozenSurfaces;
            if (statisticsInstallation == installation && statisticsSource == author && statisticsSurface == surface) return;
            statisticsInstallation = installation; statisticsSource = author; statisticsSurface = surface;
            if (author == null || surface == null) { statistics = "未接线可编辑母体与冻结表面。"; return; }
            try
            {
                var source = Source.Parse(author.text, surface.text);
                statistics = "母体 " + source.CellCount.ToString("N0") + " cells · " + source.QuadCount.ToString("N0") + " 表面片\n" + source.AssetId;
            }
            catch (Exception exception) { statistics = "源校验未通过：" + exception.Message; }
        }

        private void OnApplicationFocus(bool value)
        {
            focusAllowed = value;
            if (!value) { dragging = false; RegisterRegions(false); Capture(false); }
        }
        private void OnDisable() { dragging = false; RegisterRegions(false); Capture(false); RestoreOverview(); }
        private void OnDestroy() { RegisterRegions(false); Capture(false); RestoreOverview(); }
    }
}
