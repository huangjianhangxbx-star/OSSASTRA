/* 脚本：SideViewTransitionDebugView
 * 功能：显示横版双场过门、共同存档、一次性资源和任务测试标记的运行读数。
 * 边界：调试按钮走同一入口请求，不代替实体门验收。
 */
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Characters;
using Ossastra.Inventory;
using Ossastra.Commerce.Runtime;
using Ossastra.WorldProps;
using Ossastra.WorldTransition;

namespace Ossastra.SideViewWorld
{
    /// <summary>Task-only scene and control readback, visible in both rooms.</summary>
    public sealed class SideViewTransitionDebugView : MonoBehaviour
    {
        public SideViewTransitionGate gate;
        public SideViewTransitionArrival arrival;
        public CharacterControlCoordinator party;
        public SideViewCameraFollow cameraFollow;
        public InventorySession inventory;
        public CommerceService commerce;
        public WorldPropsRoot worldProps;
        public SVWorld01QuestMarker questMarker;

        private void OnGUI()
        {
            if (Ossastra.UI.GameMenu.GameMenuRoot.IsDisplaying) return;
            if (!Application.isPlaying) return;
            var scene = SceneManager.GetActiveScene();
            var runtime = SideViewTransitionRuntime.Instance;
            var left = Screen.width - 465f;
            GUI.Box(new Rect(left, 10, 455, 235), "Tut_01 ↔ Tut_02 · 世界状态验证");
            GUI.Label(new Rect(left + 12, 34, 430, 20),
                "Scene: " + scene.name + "  handle=" + scene.handle);
            GUI.Label(new Rect(left + 12, 55, 430, 20),
                "Gate: " + (gate == null ? "none" : gate.gateId + " → " + gate.destinationGateId));
            GUI.Label(new Rect(left + 12, 76, 430, 20),
                "角色: " + (party == null || party.Current == null ? "none" : party.Current.ActorId)
                + "  库存就绪=" + (inventory != null && inventory.Ready));
            GUI.Label(new Rect(left + 12, 97, 430, 20),
                "镜头中心: " + (cameraFollow == null ? "none" : cameraFollow.TargetCenter.ToString("F1"))
                + "  落点: " + (arrival == null ? "none" : arrival.Diagnostic));
            GUI.Label(new Rect(left + 12, 118, 430, 20),
                "过门: " + (runtime == null ? "none" : runtime.Phase + " / " + runtime.Status));
            var save = commerce == null ? null : commerce.SaveStatus;
            GUI.Label(new Rect(left + 12, 139, 430, 20),
                "存档: " + (commerce == null ? "none" : commerce.mapId + " / r" + commerce.Revision)
                + "  可写=" + (save != null && save.CanEdit) + " 已保存=" + (save != null && save.IsSaved));
            GUI.Label(new Rect(left + 12, 160, 430, 20),
                "资源: " + (worldProps == null ? "none" : worldProps.props.Length + " 个 / 就绪=" + worldProps.Ready)
                + "  任务标记=" + (questMarker != null && questMarker.Reached));
            GUI.enabled = gate != null && party != null && inventory != null
                && inventory.Ready && commerce != null && commerce.Ready
                && save != null && save.CanEdit && (runtime == null || !runtime.Busy);
            if (GUI.Button(new Rect(left + 12, 190, 210, 25), "调试：经当前入口换区"))
                gate.TryEnter();
            GUI.enabled = true;
        }
    }
}
