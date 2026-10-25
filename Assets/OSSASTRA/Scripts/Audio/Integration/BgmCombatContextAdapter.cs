/* 脚本：BgmCombatContextAdapter
 * 功能：被动转送现有CombatEngagementSession.Engaged。
 * 边界：不推断攻击/威胁，不改原3秒平静脱战，不向玩法写入状态。
 */
using UnityEngine;
using Ossastra.Combat;
namespace Ossastra.Audio
{
    public sealed class BgmCombatContextAdapter:MonoBehaviour
    {
        [SerializeField] private CombatEngagementSession session;
        [SerializeField] private BgmContextHost host;
        public CombatEngagementSession Session=>session;
        private void Update(){if(host!=null)host.SetEngaged(session!=null&&session.IsAvailable&&session.Engaged);}
        private void OnDisable(){if(host!=null)host.SetEngaged(false);}
    }
}
