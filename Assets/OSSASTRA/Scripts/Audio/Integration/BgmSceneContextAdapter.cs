/* 脚本：BgmSceneContextAdapter
 * 功能：声明本场是否为标题，保持场景事实入口独立。
 * 边界：当前没有正式标题流程，本轮测试场配置false；诊断标题不伪称生产接线。
 */
using UnityEngine;
namespace Ossastra.Audio
{
    public sealed class BgmSceneContextAdapter:MonoBehaviour
    {
        [SerializeField] private BgmContextHost host;
        [SerializeField] private bool titleScene;
        private void OnEnable(){if(host!=null)host.SetTitle(titleScene);}
        private void OnDisable(){if(host!=null)host.SetTitle(false);}
    }
}
