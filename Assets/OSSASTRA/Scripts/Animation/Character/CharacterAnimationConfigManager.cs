/* AM02：原算法适配当前只读角色事实与来源；不引用旧角色/Bus/武器运行态。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimationConfigManager : MonoBehaviour
{
    private CharacterRoot characterRoot;

    public AnimationPlanMappingSO baseMap;
    public List<AnimationPlanOverrideRuleSetSO> itemOverrides = new List<AnimationPlanOverrideRuleSetSO>();
    public AnimationPlanOverrideRuleSetSO statusOverrides;
    public AnimationLogicRouterSO logicRouter;
    private bool _isInitialized;
    private int configurationEpoch;
    public int ConfigurationVersion
    {
        get
        {
            unchecked
            {
                int v = configurationEpoch * 397;
                v = v * 31 + (baseMap == null ? 0 : baseMap.GetInstanceID() ^ baseMap.Revision);
                v = v * 31 + (logicRouter == null ? 0 : logicRouter.GetInstanceID() ^ logicRouter.Revision);
                v = v * 31 + (statusOverrides == null ? 0 : statusOverrides.GetInstanceID() ^ statusOverrides.Revision);
                if (itemOverrides != null) foreach (var item in itemOverrides) v = v * 31 + (item == null ? 0 : item.GetInstanceID() ^ item.Revision);
                return v;
            }
        }
    }
    private void OnValidate() { configurationEpoch++; }


    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;

        _isInitialized = true;
    }

    public AnimationPlanMappingSO GetBaseMap()
    {
        return baseMap;
    }

    public List<AnimationPlanOverrideRuleSetSO> GetItemOverrides()
    {
        return new List<AnimationPlanOverrideRuleSetSO>(itemOverrides ?? new List<AnimationPlanOverrideRuleSetSO>());
    }

    public AnimationPlanOverrideRuleSetSO GetStatusOverrides()
    {
        return statusOverrides;
    }

    public void LoadCharacterBasicAnimation()
    {
        // 作者配置直接在本组件；无旧Bus列表别名，不清空共享SO。
        if (itemOverrides == null) itemOverrides = new List<AnimationPlanOverrideRuleSetSO>();
    }
    /// <summary>
    /// ע�⣺
    /// ���ﲻ�����ն����ٲá�
    /// ����ֻ�ǰѵ��� logicID �����ɵ�ǰ�������µľ��� CharacterAnimationPlan��
    ///
    /// ���ն��״̬���������������ϣ��� CharacterAnimationComposer ������
    /// </summary>
    public CharacterAnimationPlan ResolvePlan(EAnimationLogicState logicState, AnimationComposeContext context = null)
    {
        var effective = new List<AnimationPlanOverrideRuleSetSO>(itemOverrides ?? new List<AnimationPlanOverrideRuleSetSO>());
        if (context?.sourceEquipment?.bodyAnimationOverride != null && context.target == "body")
            effective.Add(context.sourceEquipment.bodyAnimationOverride);
        var plan = AnimationArbitrator.Resolve(logicState, baseMap, effective, statusOverrides, characterRoot, context);
        if (plan != null) plan.runtimeLogicId = logicState;
        return plan;
    }

    /// <summary>
    /// ��״̬������·�ɳɾ��嶯��ID
    /// </summary>
    public EAnimationLogicState RouteIntent(AnimationIntent intent, AnimationComposeContext context)
    {
        //���·�ɱ����У��ͷ���·�ɽ����
        //���û���У�����Ĭ��ת����
        if (intent == null)
            return EAnimationLogicState.None;

        if (logicRouter != null)
        {
            EAnimationLogicState routed = logicRouter.Route(intent, context);
            if (routed != EAnimationLogicState.None)
                return routed;
        }

        return intent.logicId!=EAnimationLogicState.None?intent.logicId:ConvertSemanticToLogicState(intent.semantic);
    }


    private EAnimationLogicState ConvertSemanticToLogicState(EAnimSemantic semantic)
    {
        switch (semantic)
        {
            case EAnimSemantic.Attack_Normal_4: return EAnimationLogicState.Attack_Normal_4;
            case EAnimSemantic.Dodge: return EAnimationLogicState.Dodge;
            case EAnimSemantic.Guard: return EAnimationLogicState.Guard;
            case EAnimSemantic.HitReact: return EAnimationLogicState.HitReact;
            case EAnimSemantic.Stagger: return EAnimationLogicState.Stagger;
            case EAnimSemantic.Death: return EAnimationLogicState.Death;
            case EAnimSemantic.Ability: return EAnimationLogicState.Ability;
            case EAnimSemantic.Idle:
                return EAnimationLogicState.Idle;

            case EAnimSemantic.Walk:
                return EAnimationLogicState.Walk;

            case EAnimSemantic.Jog:
                return EAnimationLogicState.Jog;

            case EAnimSemantic.JogFast:
                return EAnimationLogicState.JogFast;

            case EAnimSemantic.Walk_Back:
                return EAnimationLogicState.WalkBack;

            case EAnimSemantic.Jog_Back:
                return EAnimationLogicState.JogBack;

            case EAnimSemantic.Gun_AimPose:
                return EAnimationLogicState.Aim;

            case EAnimSemantic.Gun_Fire:
                return EAnimationLogicState.Fire;

            case EAnimSemantic.Gun_Reload:
                return EAnimationLogicState.Reload;

            case EAnimSemantic.Attack_Normal_1:
                return EAnimationLogicState.Attack_Normal_1;

            case EAnimSemantic.Attack_Normal_2:
                return EAnimationLogicState.Attack_Normal_2;

            case EAnimSemantic.Attack_Normal_3:
                return EAnimationLogicState.Attack_Normal_3;
            case EAnimSemantic.Weapon_LowReady:
                return EAnimationLogicState.Weapon_LowReady;
            case EAnimSemantic.Gun_HipFirePose:
                return EAnimationLogicState.NotAimShootReady;

            case EAnimSemantic.Jump_Start:
                return EAnimationLogicState.Jump_Start_Full;
            case EAnimSemantic.Jump_Rising:
                return EAnimationLogicState.Jump_Rising_Full;
            case EAnimSemantic.Jump_Falling:
                return EAnimationLogicState.Jump_Fall_Full;
            case EAnimSemantic.Jump_Landing:
                return EAnimationLogicState.Jump_Land_Full;

        }

        return EAnimationLogicState.None;
    }






    public void RefreshWeaponAnimationOverride() { configurationEpoch++; }








}
}
