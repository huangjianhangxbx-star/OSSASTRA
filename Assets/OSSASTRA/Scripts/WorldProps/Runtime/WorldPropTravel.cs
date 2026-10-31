/* 脚本：WorldPropTravel
 * 功能：验证场可操作的保存/跨区入口，先等待原Commerce单writer屏障，成功才卸载。
 * 边界：不另写存档、不新增交互键、不暂停；失败保留当前世界，重复点击不叠加切场。
 */
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Commerce.Runtime;

namespace Ossastra.WorldProps
{
    public sealed class WorldPropTravel : MonoBehaviour
    {
        public CommerceService commerce;
        public string destinationScene;
        public string buttonLabel = "保存并进入中转";
        public bool showPanel = true;
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "";

        public async void Travel()
        {
            if (Busy || commerce == null || !commerce.Ready || string.IsNullOrWhiteSpace(destinationScene))
                return;
            Busy = true;
            try
            {
                Status = "正在保存共同快照…";
                if (!await commerce.FlushBeforeSceneChangeAsync())
                {
                    Status = "保存失败，保留当前世界";
                    return;
                }

#if UNITY_EDITOR
                var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(destinationScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
    var operation=SceneManager.LoadSceneAsync(destinationScene,LoadSceneMode.Single);
#endif
                if (operation == null)
                {
                    Status = "区域加载未接受";
                    return;
                }

                Status = "正在进入区域…";
                // 加载请求被接受不等于卸载已完成；保持闸门覆盖异步加载，防止连点叠加。
                if (!operation.isDone)
                {
                    var completed = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    operation.completed += _ => completed.TrySetResult(true);
                    await completed.Task;
                }
            }
            catch (System.Exception ex)
            {
                Status = ex.Message;
            }
            finally
            {
                Busy = false;
            }
        }

        void OnGUI()
        {
            if (!showPanel || commerce == null)
                return;
            GUI.Box(new Rect(Screen.width - 282, Screen.height - 92, 274, 84), "场景物验证 · 共同存档");
            GUI.enabled = !Busy && commerce.Ready;
            if (GUI.Button(new Rect(Screen.width - 274, Screen.height - 66, 154, 24), buttonLabel))
                Travel();
            if (GUI.Button(new Rect(Screen.width - 114, Screen.height - 66, 100, 24), "保存检查点"))
                commerce.RequestSave();
            GUI.enabled = true;
            GUI.Label(new Rect(Screen.width - 272, Screen.height - 36, 258, 23), string.IsNullOrEmpty(Status) ? commerce.Diagnostic : Status);
        }
    }
}
