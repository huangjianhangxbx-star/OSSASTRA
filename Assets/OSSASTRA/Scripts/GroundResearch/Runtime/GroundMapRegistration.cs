/* 脚本：GroundMapRegistration
 * 功能：登记自有地图身份、准确保存路径与独占生成资产目录；Editor用它核对写入资格。
 * 边界：此SO本身不授予任意Assets写权限；Editor同时校验实际资产身份、场景和保护路径。
 */
using UnityEngine;
namespace Ossastra.GroundResearch
{
    public sealed class GroundMapRegistration : ScriptableObject
    {
        public int schemaVersion = 1;
        public string owner = "Ossastra.GroundWorkshop";
        public string mapId, scenePath, assetFolder;
    }
}
