/* 脚本：CombatSoundProfileSO
 * 功能：把已冻结的动作/能力语义映射为声音内容，按阶段和伤害通道细分。
 * 边界：不读取动画名、不接受动作、不保存运行状态；未配置就是留白，不补提示音。
 */
using System;
using System.Collections.Generic;
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
        public string cue;
    }

    [CreateAssetMenu(menuName="OSSASTRA/Audio/Combat Sound Profile",fileName="OssastraCombatSoundProfile")]
    public sealed class CombatSoundProfileSO : ScriptableObject
    {
        public CombatSoundBinding[] bindings = Array.Empty<CombatSoundBinding>();

        public bool Validate(out string reason)
        {
            var keys = new HashSet<(string,string,int,string,string,string)>();
            if(bindings==null||bindings.Length==0){reason="EMPTY_SOUND_PROFILE";return false;}
            foreach(var row in bindings)
            {
                if(row==null||string.IsNullOrWhiteSpace(row.selector)||string.IsNullOrWhiteSpace(row.kind)||
                    string.IsNullOrWhiteSpace(row.cue)||row.stage<0){reason="INVALID_SOUND_BINDING";return false;}
                if(!keys.Add((row.selector,row.kind,row.stage,row.channel??"",row.deliveryMode??"",row.role??""))){reason="DUPLICATE_SOUND_BINDING";return false;}
            }
            reason="VALID";return true;
        }

        public bool TryResolve(string selector,string kind,int stage,string channel,out string cue,string deliveryMode=null,string role=null)
        {
            cue=null;if(!Validate(out _))return false;
            int best=-1;
            foreach(var row in bindings)
            {
                if(row.kind!=kind||row.selector!=selector&&row.selector!="*"||row.stage!=0&&row.stage!=stage||
                    !string.IsNullOrEmpty(row.channel)&&row.channel!=(channel??"")||
                    !string.IsNullOrEmpty(row.deliveryMode)&&row.deliveryMode!=(deliveryMode??"")||
                    !string.IsNullOrEmpty(row.role)&&row.role!=(role??""))continue;
                int score=(row.selector==selector?16:0)+(row.stage!=0?8:0)+(!string.IsNullOrEmpty(row.channel)?4:0)+(!string.IsNullOrEmpty(row.deliveryMode)?2:0)+(!string.IsNullOrEmpty(row.role)?1:0);
                if(score<=best)continue;best=score;cue=row.cue;
            }
            return cue!=null;
        }
    }
}
