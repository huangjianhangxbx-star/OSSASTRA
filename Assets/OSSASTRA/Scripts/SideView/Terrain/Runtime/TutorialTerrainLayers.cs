/* 教程地形组织的原创观察场。显示开关只作用于画面，预设假墙拥有独立碰撞。 */
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.SideView.TerrainWorkshop
{
    [DisallowMultipleComponent]
    public sealed class TutorialTerrainLayers : MonoBehaviour
    {
        public TilemapRenderer fill;
        public MeshRenderer[] edges = Array.Empty<MeshRenderer>();
        public MeshRenderer[] scenery = Array.Empty<MeshRenderer>();
        public GameObject collisionOutline;
        public GameObject blockerOutline;
        public BoxCollider blocker;
        public GameObject cover;

        private bool fillVisible = true;
        private bool edgesVisible = true;
        private bool sceneryVisible = true;
        private bool coverVisible = true;
        private bool preventedClose;
        private bool IsOpen => !blocker.enabled;

        public void SetFillVisible(bool visible)
        {
            fillVisible = visible;
            fill.enabled = visible;
        }

        public void SetEdgesVisible(bool visible)
        {
            edgesVisible = visible;
            SetVisible(edges, visible);
        }

        public void SetSceneryVisible(bool visible)
        {
            sceneryVisible = visible;
            SetVisible(scenery, visible);
        }

        public void SetCoverVisible(bool visible)
        {
            coverVisible = visible;
            cover.SetActive(visible && !IsOpen);
        }

        public bool SetPassageOpen(bool open)
        {
            if (open == IsOpen) return false;
            preventedClose = !open && ContainsCharacter();
            if (preventedClose) return false;

            blocker.enabled = !open;
            blockerOutline.SetActive(!open);
            cover.SetActive(coverVisible && !open);
            Physics.SyncTransforms();
            return true;
        }

        private bool ContainsCharacter()
        {
            var pose = blocker.transform;
            Vector3 half = blocker.size * .49f;
            Vector3 scale = pose.lossyScale;
            half = Vector3.Scale(half, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Physics.SyncTransforms();
            foreach (var hit in Physics.OverlapBox(pose.TransformPoint(blocker.center), half, pose.rotation,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (hit is CharacterController) return true;
            return false;
        }

        private static void SetVisible(MeshRenderer[] renderers, bool visible)
        {
            foreach (var renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = !visible;
        }

        private void OnGUI()
        {
            if (fill == null || blocker == null || cover == null || collisionOutline == null || blockerOutline == null)
                return;

            const float width = 454f;
            GUILayout.BeginArea(new Rect(Mathf.Max(8f, (Screen.width - width) * .5f),
                Mathf.Max(8f, Screen.height - 181f), width, 105f), GUI.skin.box);
            GUILayout.Label("沉钟水渠 · 观察地形分层");
            GUILayout.BeginHorizontal();
            bool nextFill = GUILayout.Toggle(fillVisible, "黑底格");
            bool nextEdges = GUILayout.Toggle(edgesVisible, "石边");
            bool nextScenery = GUILayout.Toggle(sceneryVisible, "布景");
            bool nextOutlines = GUILayout.Toggle(collisionOutline.activeSelf, "碰撞轮廓");
            GUILayout.EndHorizontal();

            if (nextFill != fillVisible) SetFillVisible(nextFill);
            if (nextEdges != edgesVisible) SetEdgesVisible(nextEdges);
            if (nextScenery != sceneryVisible) SetSceneryVisible(nextScenery);
            if (nextOutlines != collisionOutline.activeSelf) collisionOutline.SetActive(nextOutlines);
            GUILayout.BeginHorizontal();
            bool nextCover = GUILayout.Toggle(coverVisible, "显示假墙遮挡");
            if (nextCover != coverVisible) SetCoverVisible(nextCover);
            if (GUILayout.Button(IsOpen ? "关上假墙" : "打开假墙")) SetPassageOpen(!IsOpen);
            GUILayout.EndHorizontal();
            GUILayout.Label(preventedClose ? "缺口有人，离开后可关闭。" : IsOpen ? "通道开放" : "独立碰撞挡路");
            GUILayout.EndArea();
        }
    }
}
