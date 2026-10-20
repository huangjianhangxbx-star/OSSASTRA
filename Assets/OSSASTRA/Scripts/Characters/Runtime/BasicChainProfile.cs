/*
 * 脚本：BasicChainProfile
 * 功能：可选两段SAMPLE参数；仅新验证场装配，旧场保留Root单段参数。
 * 边界：提供不可变定义，不自己Update或产生第二动作时钟。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class BasicStageParameters
    {
        public float releaseAt=.3f, moveReadyAt=.5f, attackReadyAt=.8f, finishAt=1.1f;
        public int damage=12;
        public float postureImpact=35;
        public BasicAttackDefinition Freeze()=>new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact);
    }
    public sealed class BasicChainProfile : MonoBehaviour
    {
        [Header("两段手动普攻 SAMPLE")]
        public BasicStageParameters stage1=new BasicStageParameters();
        public BasicStageParameters stage2=new BasicStageParameters{releaseAt=.35f,moveReadyAt=.55f,attackReadyAt=.9f,finishAt=1.2f,damage=18,postureImpact=45};
        [Min(0)] public float preInput=.12f, bufferLifetime=.17f, continuationWindow=.45f;
        public BasicChainState CreateState()=>new BasicChainState(preInput,bufferLifetime,continuationWindow);
    }
}
