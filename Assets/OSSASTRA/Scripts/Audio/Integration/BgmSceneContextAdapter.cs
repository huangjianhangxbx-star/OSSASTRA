/* 脚本：BgmSceneContextAdapter
 * 功能：声明本场是否为标题，保持场景事实入口独立。
 * 边界：只声明标题场身份，曲目和混音由音频根及规则表决定。
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
