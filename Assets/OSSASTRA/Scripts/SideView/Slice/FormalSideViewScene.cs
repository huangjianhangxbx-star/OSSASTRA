/*
 * 正式横版场的接线和就绪检查。地图、角色和存档仍由原组件负责。
 */
using System;
using System.Linq;
using Ossastra.Audio;
using Ossastra.Characters;
using Ossastra.UserInterface;
using Ossastra.WorldRendering.SideView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class FormalSideViewScene : MonoBehaviour
    {
        [SerializeField] private PlayerInputLayoutInstaller input;
        [SerializeField] private InventoryHudBridge hud;
        [SerializeField] private SideViewInteractionPrompt prompt;
        [SerializeField] private CombatFeedbackView feedback;
        [SerializeField] private SideViewCameraFollow cameraFollow;
        [SerializeField] private BgmContextHost music;
        [SerializeField] private SideViewLocomotionAudioAdapter movementAudio;
        [SerializeField] private SideViewRenderComparison rendering;

        public const string StartingDiveActor = "temporary-b";

        internal static T FindActive<T>(Scene scene) where T : Behaviour
            => ActiveItems<T>(scene).FirstOrDefault();

        private static T[] ActiveItems<T>(Scene scene) where T : Behaviour
            => scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(false))
                .Where(value => value.isActiveAndEnabled).ToArray() : Array.Empty<T>();

        public static bool IsReady(Scene scene, out string reason)
        {
            reason = null;
            var assemblies = ActiveItems<FormalSideViewScene>(scene);
            if (assemblies.Length == 0 && !SliceSceneCatalog.GameplayScenes.Any(spec => spec.Path == scene.path)) return true;
            if (assemblies.Length != 1)
            {
                reason = scene.name + "：需要唯一启用的 FormalSideViewScene，实际 " + assemblies.Length;
                return false;
            }

            var assembly = assemblies[0];
            var failure = assembly.MissingBinding();
            if (failure == null) return true;
            reason = scene.name + "/" + assembly.name + "：" + failure;
            return false;
        }

        private string MissingBinding()
        {
            var scene = gameObject.scene;
            var party = input != null ? input.control : null;
            if (!Local(input) || !Local(party) || party.Current == null ||
                ActiveItems<PlayerInputLayoutInstaller>(scene).Length != 1 ||
                ActiveItems<CharacterControlCoordinator>(scene).Length != 1 ||
                ActiveItems<Ossastra.Characters.CharacterRoot>(scene).Length != 2 ||
                party.Characters.Count != 2 || party.Characters.Any(actor => !Local(actor) ||
                    actor.StateInput == null || actor.StateMovement?.IsSideView != true) ||
                party.Characters.Select(actor => actor.ActorId).Distinct(StringComparer.Ordinal).Count() != 2)
                return "双角色、横版运动或控制会话未就绪";

            var diver = party.Characters.FirstOrDefault(actor => actor.ActorId == StartingDiveActor);
            var mount = diver != null ? diver.GetComponent<SideViewDiveMount>() : null;
            if (!Local(mount) || !mount.OwnedAtStart || !mount.IsAvailable ||
                party.Characters.Any(actor => actor != diver && actor.TryGetComponent<SideViewDiveMount>(out var other) && other.IsAvailable))
                return "芙蕾茜娅的初始下冲配置无效";

            var menu = input.gameMenu;
            if (input.layout == null || !input.RequiresGameMenu || !input.menuAction.enabled ||
                !Local(menu) || !Local(menu.menu) || !menu.menu.IsConfigured ||
                ActiveItems<EventSystem>(scene).Length != 1)
                return "正式七页菜单或输入布局未就绪";
            if (!Local(input.inventory) || !Local(hud) || !Local(input.interaction) || !Local(prompt) || !prompt.IsConfigured)
                return "HUD、背包或交互提示未就绪";

            if (!Local(cameraFollow) || !Local(feedback) || !feedback.IsSideViewReady ||
                ActiveItems<CombatFeedbackView>(scene).Length != 1)
                return "当前场命中反馈或相机冲击未就绪";
            var audio = Ossastra.Audio.GameAudioRoot.Instance;
            if (!Local(music) || !Local(movementAudio) || audio == null || !audio.IsOwner || !audio.IsBoundHost(music))
                return "当前场 BGM 上下文或运动音频未绑定";
            if (!audio.SfxAvailable || ActiveItems<CombatSfxFactAdapter>(scene).Length != 1 ||
                ActiveItems<InterfaceAudioAdapter>(scene).Length != 1)
                return "当前场 SFX 事实适配器未就绪：" + audio.SfxStatus;
            if (FindObjectsOfType<AudioListener>().Count(listener => listener.isActiveAndEnabled) != 1)
                return "当前音频世界需要唯一启用的监听器";
            if (!Local(rendering) || !rendering.Active.HasValue)
                return "当前场渲染方案未就绪";
            return null;
        }

        private bool Local(Behaviour value)
            => value != null && value.isActiveAndEnabled && value.gameObject.scene == gameObject.scene;
    }
}
