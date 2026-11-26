/*
 * 脚本：BasicChainProfile
 * 功能：维护三项统一普攻参数及旧两段验证组件的时序。
 * 协作：角色、武器、旧组件解析同一个连段配置并交战斗状态机冻结。
 * 边界：提供不可变定义，不自己Update或产生第二动作时钟。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [Serializable]
    public struct BasicComboSettings
    {
        [SerializeField, HideInInspector] private int version;
        [Min(0), InspectorName("普攻预输入保留时间")]
        [Tooltip("秒。开放前记录最近一次有效按下，达到动作自身的攻击开放条件后才能接段。")]
        public float retentionSeconds;
        [Min(0), InspectorName("连段记忆时间")]
        [Tooltip("秒。从攻击开放起算，超时后下一次按下回到第一段。")]
        public float continuationSeconds;
        [InspectorName("进入下一段需要命中")]
        public bool requireHit;

        public bool IsMigrated => version != 0;

        public BasicComboSettings(float retentionSeconds, float continuationSeconds, bool requireHit)
        {
            this.retentionSeconds = retentionSeconds;
            this.continuationSeconds = continuationSeconds;
            this.requireHit = requireHit;
            version = 1;
        }

        public static BasicComboSettings FromLegacy(float preInput, float bufferLifetime, float continuationWindow, bool requireHit)
        {
            // 先核验两项原始输入和接续窗，负数、NaN 等无法被较小值计算隐藏。
            var retention = BasicChainState.LegacyRetention(preInput, bufferLifetime);
            if (float.IsNaN(continuationWindow) || float.IsInfinity(continuationWindow) || continuationWindow < 0)
                throw new ArgumentOutOfRangeException(nameof(continuationWindow));
            return new BasicComboSettings((float)retention, continuationWindow, requireHit);
        }

        public BasicChainState CreateState(int stages)
            => new BasicChainState(retentionSeconds, continuationSeconds, stages, requireHit);
    }

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
        [InspectorName("普攻连段")] public BasicComboSettings comboSettings;
        [SerializeField, HideInInspector] private float preInput = .12f, bufferLifetime = .17f, continuationWindow = .45f;

        public BasicComboSettings ComboSettings => comboSettings.IsMigrated
            ? comboSettings : BasicComboSettings.FromLegacy(preInput, bufferLifetime, continuationWindow, true);

        public bool MigrateComboSettings()
        {
            if (comboSettings.IsMigrated) return false;
            comboSettings = ComboSettings;
            return true;
        }

        public BasicChainState CreateState() => ComboSettings.CreateState(2);
    }
}
