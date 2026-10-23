using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimationConfigManager : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;

    public AnimationPlanMappingSO baseMap;
    public List<AnimationPlanOverrideRuleSetSO> itemOverrides = new List<AnimationPlanOverrideRuleSetSO>();
    public AnimationPlanOverrideRuleSetSO statusOverrides;
    public AnimationLogicRouterSO logicRouter;
    private bool _isInitialized;

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
        return itemOverrides;
    }

    public AnimationPlanOverrideRuleSetSO GetStatusOverrides()
    {
        return statusOverrides;
    }

    public void LoadCharacterBasicAnimation()
    {
        if (characterRoot == null || characterRoot.characterDataBus == null)
            return;

        baseMap = characterRoot.characterDataBus.basicAnimationPlan;
        itemOverrides = characterRoot.characterDataBus.overrideAnimationPlan;
    }
    /// <summary>
    /// ע�⣺
    /// ���ﲻ�����ն����ٲá�
    /// ����ֻ�ǰѵ��� logicID �����ɵ�ǰ�������µľ��� CharacterAnimationPlan��
    /// 
    /// ���ն��״̬���������������ϣ��� CharacterAnimationComposer ������
    /// </summary>
    public CharacterAnimationPlan ResolvePlan(EAnimationLogicState logicState)
    {
        return AnimationArbitrator.Resolve(
            logicState,
            baseMap,
            itemOverrides,
            statusOverrides,
            characterRoot
        );
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

        return ConvertSemanticToLogicState(intent.semantic);
    }


    private EAnimationLogicState ConvertSemanticToLogicState(EAnimSemantic semantic)
    {
        switch (semantic)
        {
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






    public void RefreshWeaponAnimationOverride()
    {
        if (characterRoot == null || characterRoot.characterDataBus == null)
            return;

        WeaponRuntimeData weapon = characterRoot.characterDataBus.currentWeaponRuntimeData;

        if (itemOverrides == null)
            itemOverrides = new List<AnimationPlanOverrideRuleSetSO>();

        itemOverrides.Clear();

        if (weapon != null && weapon.AnimationOverride != null)
        {
            itemOverrides.Add(weapon.AnimationOverride);
            Debug.Log($"[AnimationConfigManager] �Ѽ����������� Override: {weapon.AnimationOverride.name}");
        }
        else
        {
            Debug.Log("[AnimationConfigManager] ��ǰû���������� Override");
        }
    }








}