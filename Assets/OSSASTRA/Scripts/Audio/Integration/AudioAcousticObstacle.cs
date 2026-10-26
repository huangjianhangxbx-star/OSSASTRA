/* 脚本：AudioAcousticObstacle
 * 功能：标记真实场景碰撞几何可遮挡声音，不把人物/Trigger或地表自动当墙。
 * 边界：只读声明，无播放权，不修改碰撞、寻路或战斗。
 */
using UnityEngine;
namespace Ossastra.Audio{[DisallowMultipleComponent]public sealed class AudioAcousticObstacle:MonoBehaviour{}}
