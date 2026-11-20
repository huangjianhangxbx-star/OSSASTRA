/* 脚本：CombatSoundProfileSO
 * 功能：把已冻结的动作/能力语义映射为声音内容，按阶段和伤害通道细分。
 * 边界：不读取动画名、不接受动作、不保存运行状态；未配置就是留白，不补提示音。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.Audio
{
    [Serializable]
    public sealed class CombatSoundBinding
    {
        [Tooltip("稳定定义ID；body.basic为默认普攻，main:定义ID为动作组。*仅用于明确的公共反馈。")]
        public string selector;
        public string kind;
        [Min(0), Tooltip("0匹配任意阶段；正数匹配冻结的连段阶段。")]
        public int stage;
        [Tooltip("空白匹配该定义的所有通道；填写后严格匹配。")]
        public string channel;
        [Tooltip("空白不限制；如Projectile / RayLandingArc可区分同一技能的不同投递。")]
        public string deliveryMode;
        [Tooltip("空白不限制；player / companion / enemy。可以有意省略不适合该实体的角色呼痛。")]
        public string role;
        [Tooltip("空白通用；仅来自真实目标AudioSurfaceFact的明确材质，不由攻击位置猜测。")]
        public string material;
        public string cue;

        internal CombatSoundBinding Copy() => (CombatSoundBinding)MemberwiseClone();
    }

    [CreateAssetMenu(menuName="OSSASTRA/Audio/Combat Sound Profile",fileName="OssastraCombatSoundProfile")]
    public sealed class CombatSoundProfileSO : ScriptableObject
    {
        public CombatSoundBinding[] bindings = Array.Empty<CombatSoundBinding>();

        public bool Validate(out string reason)
        {
            var keys = new HashSet<(string,string,int,string,string,string,string)>();
            if(bindings==null||bindings.Length==0){reason="EMPTY_SOUND_PROFILE";return false;}
            foreach(var row in bindings)
            {
                if(row==null||string.IsNullOrWhiteSpace(row.selector)||string.IsNullOrWhiteSpace(row.kind)||
                    string.IsNullOrWhiteSpace(row.cue)||row.stage<0){reason="INVALID_SOUND_BINDING";return false;}
                if(!keys.Add((row.selector,row.kind,row.stage,row.channel??"",row.deliveryMode??"",row.role??"",row.material??""))){reason="DUPLICATE_SOUND_BINDING";return false;}
            }
            reason="VALID";return true;
        }

        public bool TryResolve(string selector,string kind,int stage,string channel,out string cue,string deliveryMode=null,string role=null,string material=null)
        {
            cue=null;if(!Validate(out _))return false;
            return TryResolve(bindings,selector,kind,stage,channel,out cue,deliveryMode,role,material);
        }

        internal CombatSoundBinding[] FreezeFor(string selector)
            => Validate(out _) ? bindings.Where(row => row.selector == selector || row.selector == "*").Select(row => row.Copy()).ToArray()
                : Array.Empty<CombatSoundBinding>();

        internal static bool TryResolve(CombatSoundBinding[] rows, string selector, string kind, int stage, string channel,
            out string cue, string deliveryMode, string role, string material)
        {
            cue = null;
            int best=-1;
            foreach(var row in rows)
            {
                if (row.kind != kind) continue;
                int score = MatchScore(row.selector, row.stage, row.channel, row.deliveryMode, row.role, row.material,
                    selector, stage, channel, deliveryMode, role, material);
                if(score<=best)continue;best=score;cue=row.cue;
            }
            return cue!=null;
        }

        internal static int MatchScore(string rowSelector, int rowStage, string rowChannel, string rowDelivery,
            string rowRole, string rowMaterial, string selector, int stage, string channel, string delivery,
            string role, string material)
        {
            bool Matches(string expected, string actual) => string.IsNullOrEmpty(expected) || expected == actual;
            if (rowSelector != selector && rowSelector != "*" || rowStage != 0 && rowStage != stage ||
                !Matches(rowChannel, channel) || !Matches(rowDelivery, delivery) ||
                !Matches(rowRole, role) || !Matches(rowMaterial, material)) return -1;
            // 明确材质优先于通用连段，同一套规则也用于角色覆盖。
            return (rowSelector == selector ? 64 : 0) + (!string.IsNullOrEmpty(rowMaterial) ? 32 : 0) +
                (rowStage != 0 ? 16 : 0) + (!string.IsNullOrEmpty(rowChannel) ? 8 : 0) +
                (!string.IsNullOrEmpty(rowDelivery) ? 4 : 0) + (!string.IsNullOrEmpty(rowRole) ? 2 : 0);
        }
    }
}
