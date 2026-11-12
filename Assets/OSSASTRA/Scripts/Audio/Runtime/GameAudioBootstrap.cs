/* 脚本：GameAudioBootstrap
 * 功能：为有正式消费者的场景补齐唯一音频根和只读事实适配器。
 * 边界：旧场以战斗视图触发；横版场以明确持久的运动音效适配器触发，不创建假视图。
 * 协作：加载现有 Resources 配置；音频根晚于场景宿主时补绑 BGM 上下文。
 */
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ossastra.Audio
{
    public static class GameAudioBootstrap
    {
        public static GameAudioBootstrapSO LoadConfiguration() => Resources.Load<GameAudioBootstrapSO>("OssastraAudioBootstrap");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var views = Array.FindAll(Object.FindObjectsOfType<Ossastra.UserInterface.CombatFeedbackView>(), v => v.gameObject.scene == scene);
            var sideMotion = Array.Find(Object.FindObjectsOfType<SideViewLocomotionAudioAdapter>(), a => a.isActiveAndEnabled && a.gameObject.scene == scene);
            var root = GameAudioRoot.Instance;
            if (views.Length == 0 && sideMotion == null)
            {
                if (mode == LoadSceneMode.Single && root != null) root.BeginSfxWorld(scene.path + "#" + scene.handle);
                return;
            }
            if (root == null)
            {
                var config = LoadConfiguration();
                if (config == null || !config.Validate(out _))
                {
                    Debug.LogWarning("AUDIO_BOOTSTRAP_UNAVAILABLE：检查 OssastraAudioBootstrap 既有配置。");
                    return;
                }
                root = new GameObject("GameAudioRoot [Unified Bootstrap]").AddComponent<GameAudioRoot>();
            }
            if (!root.IsOwner) return;
            if (mode == LoadSceneMode.Single) root.BeginSfxWorld(scene.path + "#" + scene.handle);
            if (sideMotion != null && scene == SceneManager.GetActiveScene())
            {
                var context = Array.Find(Object.FindObjectsOfType<BgmContextHost>(), value => value.isActiveAndEnabled && value.gameObject.scene == scene);
                if (context != null) context.AttachAudioRoot(root);
                else Debug.LogWarning("SIDE_AUDIO_CONTEXT_MISSING：横版场需要 BgmContextHost，不能仅凭音频根存在判定音乐就绪。");
            }

            EnsureAdapter<CombatSfxFactAdapter>(scene, "Combat SFX Facts [Audio Owned]");
            EnsureAdapter<InterfaceAudioAdapter>(scene, "Interface Facts [Audio Owned]");
            if (sideMotion == null) EnsureAdapter<LocomotionAudioAdapter>(scene, "Locomotion Facts [Audio Owned]");
            else
            {
                foreach (var legacy in Object.FindObjectsOfType<LocomotionAudioAdapter>())
                    if (legacy.gameObject.scene == scene) legacy.enabled = false;
                sideMotion.Discover();
            }
        }

        private static void EnsureAdapter<T>(Scene scene, string name) where T : MonoBehaviour
        {
            if (Array.Exists(Object.FindObjectsOfType<T>(), item => item.gameObject.scene == scene)) return;
            var owned = new GameObject(name);
            SceneManager.MoveGameObjectToScene(owned, scene);
            owned.AddComponent<T>();
        }
    }
}
