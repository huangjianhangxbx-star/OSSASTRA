/* 脚本：SfxContentSetSO
 * 功能：明确标识本机精选音效目录；真实内容与公共诊断样本分开。
 * 边界：受限PCM及引用它的本机资产不公开；缺失时必须展示内容状态。
 */
using UnityEngine;
namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName="OSSASTRA/Audio/SFX Content Set",fileName="OssastraSfxContent")]
    public sealed class SfxContentSetSO : ScriptableObject
    {
        public string contentLabel;
        public SfxCatalogSO catalog;
        public bool Validate(out string reason)
        {
            if(string.IsNullOrWhiteSpace(contentLabel)||catalog==null){reason="CONTENT_SET_INCOMPLETE";return false;}
            return catalog.Validate(out reason);
        }
    }
}
