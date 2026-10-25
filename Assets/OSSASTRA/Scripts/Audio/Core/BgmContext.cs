/*
 * 脚本：BgmContext
 * 功能：向音乐仲裁器提供一份已经成立的场景事实快照。
 * 边界：不自行检测交战，不持有角色；普通对话仅影响混音，因此没有对话选曲条件。
 * 协作：独立适配器／BgmContextHost → BgmArbitrator。
 */
namespace Ossastra.Audio
{
    [System.Serializable]
    public struct BgmContext
    {
        public bool boss;
        public bool engaged;
        public bool title;
        public bool far;
    }
}
