/* 脚本：GameAudioBootstrap
 * 功能：在确有战斗反馈消费者的场景补齐同一音频根，并安装只读事实适配器。
 * 边界：不修改场景磁盘/Prefab，不创建第二播放器；无消费者的编辑/其他场景不自动接管。
 * 协作：旧场无需保存迁移；配置来自唯一明确Resources入口，缺失则显式诊断。
 */
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ossastra.Audio
{
    public static class GameAudioBootstrap
    {
        public static GameAudioBootstrapSO LoadConfiguration()=>Resources.Load<GameAudioBootstrapSO>("OssastraAudioBootstrap");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;}
        private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var views=System.Array.FindAll(Object.FindObjectsOfType<Ossastra.UserInterface.CombatFeedbackView>(),v=>v.gameObject.scene==scene);
            var root=GameAudioRoot.Instance;
            if(views.Length==0)
            {if(mode==LoadSceneMode.Single&&root!=null)root.BeginSfxWorld(scene.path+"#"+scene.handle);return;}
            if(root==null)
            {
                var config=LoadConfiguration();
                if(config==null||!config.Validate(out _)){Debug.LogWarning("AUDIO_M02_BOOTSTRAP_UNAVAILABLE：战斗声音未接管，检查OssastraAudioBootstrap配置。");return;}
                var go=new GameObject("GameAudioRoot [Unified Bootstrap]");
                root=go.AddComponent<GameAudioRoot>();
            }
            if(!root.IsOwner)return;
            if(mode==LoadSceneMode.Single)root.BeginSfxWorld(scene.path+"#"+scene.handle);
            // 适配器观察整个自有场景；多个HUD视图不应各订阅同一角色而重复发声。
            bool hasAdapter=System.Array.Exists(Object.FindObjectsOfType<CombatSfxFactAdapter>(),a=>a.gameObject.scene==scene);
            if(!hasAdapter)
            {
                var host=new GameObject("Combat SFX Facts [Audio Owned]");SceneManager.MoveGameObjectToScene(host,scene);
                host.AddComponent<CombatSfxFactAdapter>();
            }
        }
    }
}
