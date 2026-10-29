/*
 * 脚本：GroundResearchView
 * 功能：独立研究场的折叠观察面板；对照面积、图层、来源与alpha遮罩。
 * 边界：仅临时改变受控Renderer和现有敌人攻击开关，不改作者数据、角色、材质或输入绑定。
 * 协作：从GroundLayer读取来源/底纹；使用各角色既有指针区域避免点面板同时攻击。
 * 生命周期：进入观察保存完整Renderer属性块/显隐和敌人许可，禁用、销毁或重绑精确恢复。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using C = Ossastra.Characters;
using E = Ossastra.Enemies;

namespace Ossastra.GroundResearch
{
    [DefaultExecutionOrder(-380)]
    [DisallowMultipleComponent]
    public sealed class GroundResearchView : MonoBehaviour
    {
        public GroundResearchSurface surface;
        public E.EnemyRoot encounterEnemy;
        [Tooltip("显式指定本场的角色输入；仅登记此面板区域，不捕获移动或暂停世界。")]
        public C.CharacterInputHandler[] playerInputs = Array.Empty<C.CharacterInputHandler>();
        [Min(0)] public float referenceArea;
        [Min(0)] public int selectionIndex;
        public bool panelExpanded;
        [Min(240)] public float panelWidth = 430;
        [Min(180)] public float expandedHeight = 660;

        private sealed class RendererState
        {
            public TilemapRenderer renderer;
            public bool enabled;
            public readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock working = new MaterialPropertyBlock();
        }
        private static readonly int DebugMaskId = Shader.PropertyToID("_DebugMask");
        private readonly List<RendererState> states = new List<RendererState>();
        private readonly List<C.CharacterInputHandler> registered = new List<C.CharacterInputHandler>();
        private GroundResearchSurface boundSurface;
        private E.EnemyRoot boundEnemy;
        private bool enemyOriginalAttacks, observing, maskDebug;
        private Vector2 scroll;
        private GUIStyle wrappedLabel;

        public Rect PanelRect
        {
            get
            {
                float width = Mathf.Min(panelWidth, Mathf.Max(1, Screen.width - 24));
                float height = Mathf.Min(panelExpanded ? expandedHeight : 36, Mathf.Max(1, Screen.height - 24));
                return new Rect(Mathf.Max(0, Screen.width - width - 12), 12, width, height);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            panelExpanded = false;
            BeginObservation();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!observing || boundSurface != surface || boundEnemy != encounterEnemy) BeginObservation();
            RefreshPointerRegions();
        }

        /// <summary>新场在绑定完成后开始观察；重复绑定先还原自己的旧状态，绝不把临时值当原值。</summary>
        public void BeginObservation()
        {
            RestoreView();
            boundSurface = surface;
            if (surface != null && surface.layers != null)
            {
                for (int i = 0; i < surface.layers.Length; i++)
                {
                    TilemapRenderer renderer = ResolveRenderer(i);
                    if (renderer == null || FindState(renderer) != null) continue;
                    var state = new RendererState { renderer = renderer, enabled = renderer.enabled };
                    renderer.GetPropertyBlock(state.original);
                    states.Add(state);
                }
            }
            boundEnemy = encounterEnemy;
            if (boundEnemy != null)
            {
                enemyOriginalAttacks = boundEnemy.AttacksEnabled;
                boundEnemy.AttacksEnabled = false;
            }
            observing = true;
            RefreshPointerRegions();
        }

        public void SetEnemyAttacksAllowed(bool value)
        {
            if (!observing) BeginObservation();
            if (boundEnemy != null) boundEnemy.AttacksEnabled = value;
        }

        public void SetMaskDebug(bool value)
        {
            if (!observing) BeginObservation();
            maskDebug = value;
            foreach (RendererState state in states)
            {
                if (state.renderer == null) continue;
                // 读回当前块再覆盖单个值，保留无关字段；不访问Renderer.material或改sharedMaterial。
                state.renderer.GetPropertyBlock(state.working);
                state.working.SetFloat(DebugMaskId, value ? 1 : 0);
                state.renderer.SetPropertyBlock(state.working);
            }
        }

        public bool SetLayerVisible(int index, bool visible)
        {
            if (!observing) BeginObservation();
            RendererState state = FindState(ResolveRenderer(index));
            if (state == null) return false;
            state.renderer.enabled = visible;
            return true;
        }

        public void ShowAllLayers()
        {
            if (!observing) BeginObservation();
            foreach (RendererState state in states) if (state.renderer != null) state.renderer.enabled = true;
        }

        public void ShowBaseLayer()
        {
            if (!observing) BeginObservation();
            TilemapRenderer lowest = null;
            foreach (RendererState state in states)
            {
                var renderer = state.renderer;
                if (renderer == null) continue;
                int layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                int previous = lowest == null ? 0 : SortingLayer.GetLayerValueFromID(lowest.sortingLayerID);
                if (lowest == null || layer < previous || (layer == previous && renderer.sortingOrder < lowest.sortingOrder)) lowest = renderer;
            }
            if (lowest != null) ShowOnly(lowest);
        }

        public bool ShowSingleLayer(int index)
        {
            if (!observing) BeginObservation();
            RendererState selected = FindState(ResolveRenderer(index));
            if (selected == null) return false;
            ShowOnly(selected.renderer);
            selectionIndex = index;
            return true;
        }

        private void ShowOnly(TilemapRenderer selected)
        {
            foreach (RendererState state in states) if (state.renderer != null) state.renderer.enabled = state.renderer == selected;
        }

        private TilemapRenderer ResolveRenderer(int index)
        {
            if (surface == null || surface.layers == null || index < 0 || index >= surface.layers.Length) return null;
            GroundLayer layer = surface.layers[index];
            return layer != null && surface.Owns(layer.tilemap) ? layer.tilemap.GetComponent<TilemapRenderer>() : null;
        }

        private RendererState FindState(TilemapRenderer renderer)
        {
            if (renderer == null) return null;
            foreach (RendererState state in states) if (state.renderer == renderer) return state;
            return null;
        }

        private void RefreshPointerRegions()
        {
            for (int i = registered.Count - 1; i >= 0; i--)
            {
                var input = registered[i];
                if (input != null && ContainsInput(input)) continue;
                if (input != null) input.RemovePointerRegion(this);
                registered.RemoveAt(i);
            }
            if (!observing || playerInputs == null) return;
            foreach (var input in playerInputs)
            {
                if (input == null) continue;
                if (!registered.Contains(input)) registered.Add(input);
                input.RegisterPointerRegion(this, PanelRect);
            }
        }

        private bool ContainsInput(C.CharacterInputHandler input)
        {
            if (!observing || playerInputs == null) return false;
            foreach (var candidate in playerInputs) if (candidate == input) return true;
            return false;
        }

        public void RestoreView()
        {
            foreach (RendererState state in states)
            {
                if (state.renderer == null) continue;
                state.renderer.enabled = state.enabled;
                state.renderer.SetPropertyBlock(state.original.isEmpty ? null : state.original);
            }
            states.Clear();
            if (boundEnemy != null) boundEnemy.AttacksEnabled = enemyOriginalAttacks;
            boundEnemy = null;
            boundSurface = null;
            foreach (var input in registered) if (input != null) input.RemovePointerRegion(this);
            registered.Clear();
            observing = false;
            maskDebug = false;
        }

        private void OnDisable() => RestoreView();
        private void OnDestroy() => RestoreView();

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (wrappedLabel == null) wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            if (GUILayout.Button(panelExpanded ? "收起地表研究" : "地表研究 / 图层与来源", GUILayout.Height(24)))
            {
                panelExpanded = !panelExpanded;
                RefreshPointerRegions();
            }
            if (panelExpanded)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                float area = surface == null ? 0 : surface.WorldArea;
                GUILayout.Label("占地 " + area.ToString("0.##") + " m²" + (referenceArea > 0 ? "  / 对照 " + (area / referenceArea).ToString("0.##") + " 倍" : ""), wrappedLabel);
                GUILayout.Label("F6 不暂停世界。默认只观察；仅下方开关允许本场敌人攻击。", wrappedLabel);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && boundEnemy != null;
                bool attacks = boundEnemy != null && boundEnemy.AttacksEnabled;
                bool nextAttacks = GUILayout.Toggle(attacks, "允许本场敌人攻击");
                if (nextAttacks != attacks) SetEnemyAttacksAllowed(nextAttacks);
                GUI.enabled = oldEnabled;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("全部图层")) ShowAllLayers();
                if (GUILayout.Button("仅底层")) ShowBaseLayer();
                GUILayout.EndHorizontal();
                bool nextMask = GUILayout.Toggle(maskDebug, "只看 alpha 遮罩");
                if (nextMask != maskDebug) SetMaskDebug(nextMask);
                DrawLayers();
                DrawSource();
                GUILayout.Label("世界底纹采样为本工程适配（本样板16m）；原作 Lit 与精确 UV 公式未知。", wrappedLabel);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        private void DrawLayers()
        {
            if (surface == null || surface.layers == null) { GUILayout.Label("未配置研究地表"); return; }
            for (int i = 0; i < surface.layers.Length; i++)
            {
                GroundLayer layer = surface.layers[i];
                TilemapRenderer renderer = ResolveRenderer(i);
                if (layer == null || renderer == null) continue;
                GUILayout.BeginHorizontal();
                bool visible = GUILayout.Toggle(renderer.enabled, string.IsNullOrEmpty(layer.displayName) ? "图层 " + i : layer.displayName);
                if (visible != renderer.enabled) SetLayerVisible(i, visible);
                if (GUILayout.Button("来源", GUILayout.Width(48))) selectionIndex = i;
                if (GUILayout.Button("只看", GUILayout.Width(48))) ShowSingleLayer(i);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawSource()
        {
            if (surface == null || surface.layers == null || surface.layers.Length == 0) return;
            selectionIndex = Mathf.Clamp(selectionIndex, 0, surface.layers.Length - 1);
            GroundLayer layer = surface.layers[selectionIndex];
            if (layer == null) return;
            GUILayout.Space(6);
            GUILayout.Label("来源：" + layer.displayName, wrappedLabel);
            if (layer.detailTexture != null)
            {
                Rect preview = GUILayoutUtility.GetRect(112, 112, GUILayout.Width(112));
                GUI.DrawTexture(preview, layer.detailTexture, ScaleMode.ScaleToFit);
            }
            GUILayout.Label("ID：" + layer.sourceId + "\n对象：" + layer.sourceObjectId + "\n容器：" + layer.sourceContainer, wrappedLabel);
            GUILayout.Label("当前世界重复周期：" + layer.worldRepeatPeriod.ToString("0.###") + "m（Unity适配）", wrappedLabel);
            if (!string.IsNullOrEmpty(layer.evidence)) GUILayout.Label(layer.evidence, wrappedLabel);
        }
    }
}
