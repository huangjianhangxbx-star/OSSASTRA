/*
 * 脚本：CompanionFollowDebugView
 * 功能：展示跟随来源、目标、距离/阈值、状态与拒绝原因，提供可达开发入口。
 * 边界：只读业务状态；切换/禁启通过真实入口，不伪造GUI成功、不移动角色。
 * 协作：直接观察各角色Follow与分配源；登记自己的GUI阻挡区域，禁用只清理自己。
 */
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.Development
{
    public sealed class CompanionFollowDebugView : MonoBehaviour
    {
        [SerializeField] private CompanionFollowDirector director;
        private static readonly Rect panel = new Rect(12,294,465,300);
        private void OnEnable() { Register(true); }
        private void OnDisable() { Register(false); }
        private void Register(bool add)
        {
            if (director == null || director.Coordinator == null) return;
            foreach (var character in director.Coordinator.Characters)
            {
                var input=character == null ? null : character.GetComponent<CharacterInputHandler>();
                if (input == null) continue;
                if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this);
            }
        }
        private void OnGUI()
        {
            if (director == null || director.Coordinator == null) return;
            var coordinator=director.Coordinator;
            GUILayout.BeginArea(panel,GUI.skin.box);
            GUILayout.Label("最小伙伴跟随候选 · 临时同素材 · Tab/Select切换");
            GUILayout.Label("Current: " + (coordinator.Current == null ? "无" : coordinator.Current.ActorId) + " | " + director.Status);
            foreach (var character in coordinator.Characters)
            {
                if (character == null) { GUILayout.Label("成员引用缺失"); continue; }
                var follow=character.Follow;
                if (follow == null) { GUILayout.Label(character.ActorId+" | 无Follow组件"); continue; }
                var observation=follow.Observe();
                GUILayout.Label(character.ActorId + " | " + character.EffectiveIntentKind + " | " + observation.State + " | 距离 " + observation.Distance.ToString("F2"));
                GUILayout.Label("目标 " + observation.TargetId + " | 起停 " + follow.StartDistance.ToString("F2")+"/"+follow.StopDistance.ToString("F2") + " | " + observation.Reason);
                if (GUILayout.Button(character.ActorId+"：切换本体Follow启用")) follow.enabled=!follow.enabled;
            }
            if (GUILayout.Button("合法路径：切换实控角色")) coordinator.SwitchNext();
            if (GUILayout.Button("非法/恢复：切换跟随启用")) director.enabled=!director.enabled;
            if (GUILayout.Button("清理：禁用后重新启用跟随，重新采样")) { director.enabled=false; director.enabled=true; }
            if (GUILayout.Button("检查：输出当前权威与状态")) Debug.Log(coordinator.Status + " | " + director.Status,this);
            GUILayout.EndArea();
        }
    }
}
