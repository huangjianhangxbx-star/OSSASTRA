# ProjectScript Code Documentation - Part 4

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationComposeContext.cs` | 34 | - | class AnimationComposeContext | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationHandleSystem.cs` | 471 | - | class AnimationHandleSystem, class TrackRuntimeInfo | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationIntent.cs` | 110 | - | class AnimationIntent, class AnimationIntentSource | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationLogicRouterSO.cs` | 364 | - | class AnimationLogicRouterSO, enum ConditionType, class Condition, class RouteRule | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanMappingSO.cs` | 218 | - | class AnimationPlanMappingSO, class Entry | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanOverrideRuleSetSO.cs` | 349 | - | class AnimationPlanOverrideRuleSetSO, enum ConditionType, class Rule | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationTransitionRouter.cs` | 789 | - | class AnimationTransitionRouter, class TransitionDecision | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationClip.cs` | 88 | - | class CharacterAnimationClip | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationComposer.cs` | 877 | - | class CharacterAnimationComposer, enum TrackOperationType, class TrackOperation, class ResolvedAnimationPlan | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationConfigManager.cs` | 178 | - | class CharacterAnimationConfigManager | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationPlan.cs` | 105 | - | class CharacterAnimationPlan | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationQueuedClip.cs` | 28 | - | class CharacterAnimationQueuedClip | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterSpineEventSystem.cs` | 146 | - | enum CharacterSpineSignalType, struct CharacterSpineAnimationSignal, class CharacterSpineEventSystem | 0 |
| `Assets/Script/Character/Core/Systems/AnimationSystem/IAnimationProvider.cs` | 9 | - | interface IAnimationProvider | 0 |
| `Assets/Script/Character/Core/Systems/CameraSystem/CharacterCameraTargetSystem.cs` | 279 | - | class CharacterCameraTargetSystem | 0 |
| `Assets/Script/Character/Core/Systems/CameraSystem/ProCameraRuntimeBinder.cs` | 40 | - | class ProCameraRuntimeBinder | 0 |
| `Assets/Script/Character/Core/Systems/InputHandler.cs` | 788 | - | class InputHandler | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/InventorySlotData.cs` | 20 | - | class InventorySlotData | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemCategory.cs` | 12 | - | enum ItemCategory | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemDefinitionSO.cs` | 23 | - | class ItemDefinitionSO | 0 |

## Public API

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationComposeContext.cs`
- `public class AnimationComposeContext
{`
- `public CharacterRoot root;`
- `public CharacterDataBus dataBus;`
- `public IReadOnlyDictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents;`
- `public bool HasWeapon
    {`
- `public bool HasIntent(AnimationIntentSourceStateMachine source)
    {`
- `public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationHandleSystem.cs`
- `public class AnimationHandleSystem : MonoBehaviour, ICharacterSubsystem
{`
- `public CharacterAnimationPlan currentPlan;`
- `public string animName;`
- `public bool loop;`
- `public AnimationIntentSourceStateMachine sourceStateMachine;`
- `public int replayId;`
- `public string queuedAnimName;`
- `public bool queuedLoop;`
- `public AnimationIntentSourceStateMachine queuedSource;`
- `public int queuedReplayId;`
- `public void Initialize(CharacterRoot root)
    {`
- `public TrackEntry PlayPrepAnimation(CharacterAnimationClip clip, System.Action onPrepComplete)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationIntent.cs`
- `public class AnimationIntent
{`
- `public CharacterRoot root;`
- `public AnimationIntentSourceStateMachine intentSourceStateMatine;`
- `public EAnimSemantic semantic;`
- `public AnimationLayer layer;`
- `public int priority;`
- `public bool exclusiveFullBody;`
- `public bool oneShot;`
- `public float duration;`
- `public float expireTime;`
- `public bool forceReplayConsumed;`
- `public int replayId;`
- `public AnimationIntent Clone()
    {`
- `public static class AnimationIntentSource
{`
- `public const string Movement = "Movement";`
- `public const string AutoAttack = "AutoAttack";`
- `public const string Aim = "Aim";`
- `public const string Hurt = "Hurt";`
- `public const string Skill = "Skill";`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationLogicRouterSO.cs`
- `public class AnimationLogicRouterSO : ScriptableObject
{`
- `public enum ConditionType
    {`
- `public class Condition
    {`
- `public ConditionType type = ConditionType.Always;`
- `public AnimationIntentSourceStateMachine intentSource = AnimationIntentSourceStateMachine.None;`
- `public string dataBusFieldName;`
- `public string compareString;`
- `public class RouteRule
    {`
- `public bool matchSource = false;`
- `public AnimationIntentSourceStateMachine source = AnimationIntentSourceStateMachine.None;`
- `public EAnimSemantic inputSemantic;`
- `public EAnimationLogicState outputID;`
- `public int priority = 0;`
- `public List<Condition> conditions = new List<Condition>();`
- `public List<RouteRule> rulesMelee = new List<RouteRule>();`
- `public List<RouteRule> rulesMovement = new List<RouteRule>();`
- `public List<RouteRule> rulesFirearm = new List<RouteRule>();`
- `public List<RouteRule> rulesJump = new List<RouteRule>();`
- `public List<RouteRule> rulesTransition = new List<RouteRule>();`
- `public List<RouteRule> rulesSkill = new List<RouteRule>();`
- `public List<RouteRule> rulesOther = new List<RouteRule>();`
- `public List<RouteRule> rules = new List<RouteRule>();`
- `public void MigrateRules()
    {`
- `public EAnimationLogicState Route(AnimationIntent intent, AnimationComposeContext context)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanMappingSO.cs`
- `public class AnimationPlanMappingSO : ScriptableObject
{`
- `public SkeletonDataAsset skeletonDataAsset;`
- `public class Entry
    {`
- `public EAnimationLogicState logicID;`
- `public CharacterAnimationPlan plan = new CharacterAnimationPlan();`
- `public List<Entry> plansMelee = new List<Entry>();`
- `public List<Entry> plansMovement = new List<Entry>();`
- `public List<Entry> plansFirearm = new List<Entry>();`
- `public List<Entry> plansJump = new List<Entry>();`
- `public List<Entry> plansTransition = new List<Entry>();`
- `public List<Entry> plansSkill = new List<Entry>();`
- `public List<Entry> plansOther = new List<Entry>();`
- `public List<Entry> defaultPlans = new List<Entry>();`
- `public void MigrateDefaultPlans()
    {`
- `public CharacterAnimationPlan GetDefault(EAnimationLogicState id)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanOverrideRuleSetSO.cs`
- `public class AnimationPlanOverrideRuleSetSO : ScriptableObject
{`
- `public SkeletonDataAsset skeletonDataAsset;`
- `public enum ConditionType
    {`
- `public class Rule
    {`
- `public EAnimationLogicState targetID;`
- `public CharacterAnimationPlan replacementPlan = new CharacterAnimationPlan();`
- `public ConditionType condition = ConditionType.Always;`
- `public string dataBusFieldName;`
- `public float threshold;`
- `public string compareString;`
- `public List<Rule> rulesMelee = new List<Rule>();`
- `public List<Rule> rulesMovement = new List<Rule>();`
- `public List<Rule> rulesFirearm = new List<Rule>();`
- `public List<Rule> rulesJump = new List<Rule>();`
- `public List<Rule> rulesTransition = new List<Rule>();`
- `public List<Rule> rulesSkill = new List<Rule>();`
- `public List<Rule> rulesOther = new List<Rule>();`
- `public List<Rule> rules = new List<Rule>();`
- `public void MigrateRules()
    {`
- `public CharacterAnimationPlan GetOverride(EAnimationLogicState id, CharacterDataBus dataBus)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationTransitionRouter.cs`
- `public class AnimationTransitionRouter
{`
- `public bool hasTransitionIntent;`
- `public bool shouldApply;`
- `public EAnimationLogicState transitionLogicState = EAnimationLogicState.None;`
- `public TransitionSuppressReason suppressReason = TransitionSuppressReason.None;`
- `public bool shouldWarnWhenSkipped = true;`
- `public string reason;`
- `public CharacterAnimationPlan BuildTransitionedPlan(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    CharacterAnimationPlan targetPlan,
    AnimationComposeContext context,
    CharacterAnimationConfigManager configManager)
    {`
- `public bool TryResolveMovementTransition(
    EAnimSemantic from,
    EAnimSemantic to,
    out EAnimSemantic transitionSemantic)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationClip.cs`
- `public class CharacterAnimationClip
{`
- `public string animaName;`
- `public int track = 0;`
- `public bool loop = true;`
- `public float mixDuration = 0f;`
- `public bool isAdditive = false;`
- `public SkeletonDataAsset skeletonDataAsset;`
- `public AnimationIntentSourceStateMachine runtimeSourceStateMachine;`
- `public int runtimeReplayId;`
- `public CharacterAnimationClip() {`
- `public CharacterAnimationClip(string name, int track = 0)
    {`
- `public CharacterAnimationClip Clone()
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationComposer.cs`
- `public class CharacterAnimationComposer : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{`
- `public CharacterAnimationPlan CurrentOutputPlan => currentOutputPlan;`
- `public bool debugCompose = true;`
- `public bool debugEveryRebuild = true;`
- `public enum TrackOperationType
    {`
- `public void Initialize(CharacterRoot root)
    {`
- `public TrackOperationType type;`
- `public int track;`
- `public int priority;`
- `public CharacterAnimationClip clip;`
- `public AnimationIntentSourceStateMachine sourceStateMachine;`
- `public int replayId;`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void SubmitIntent(AnimationIntent intent)
    {`
- `public void RemoveIntent(AnimationIntentSourceStateMachine source)
    {`
- `public void ClearAllIntents()
    {`
- `public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {`
- `public int resolvedIndex;`
- `public AnimationIntentSourceStateMachine sourceStateMachine;`
- `public EAnimSemantic inputSemantic;`
- `public EAnimationLogicState routedLogicID;`
- `public int priority;`
- `public int replayId;`
- `public bool exclusiveFullBody;`
- `public CharacterAnimationPlan plan;`
- `public void ClearTracks(params int[] tracks)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationConfigManager.cs`
- `public class CharacterAnimationConfigManager : MonoBehaviour, ICharacterSubsystem
{`
- `public AnimationPlanMappingSO baseMap;`
- `public List<AnimationPlanOverrideRuleSetSO> itemOverrides = new List<AnimationPlanOverrideRuleSetSO>();`
- `public AnimationPlanOverrideRuleSetSO statusOverrides;`
- `public AnimationLogicRouterSO logicRouter;`
- `public void Initialize(CharacterRoot root)
    {`
- `public AnimationPlanMappingSO GetBaseMap()
    {`
- `public List<AnimationPlanOverrideRuleSetSO> GetItemOverrides()
    {`
- `public AnimationPlanOverrideRuleSetSO GetStatusOverrides()
    {`
- `public void LoadCharacterBasicAnimation()
    {`
- `public CharacterAnimationPlan ResolvePlan(EAnimationLogicState logicState)
    {`
- `public EAnimationLogicState RouteIntent(AnimationIntent intent, AnimationComposeContext context)
    {`
- `public void RefreshWeaponAnimationOverride()
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationPlan.cs`
- `public class CharacterAnimationPlan
{`
- `public List<CharacterAnimationClip> clips = new List<CharacterAnimationClip>();`
- `public List<CharacterAnimationQueuedClip> queuedClips = new List<CharacterAnimationQueuedClip>();`
- `public List<int> clearTracks = new List<int>();`
- `public static IEnumerable<ValueDropdownItem<int>> GetTrackOptions()
    {`
- `public CharacterAnimationPlan CloneFor(CharacterRoot root)
    {`
- `public void SetSkeletonDataAsset(SkeletonDataAsset skeletonDataAsset)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationQueuedClip.cs`
- `public class CharacterAnimationQueuedClip
{`
- `public CharacterAnimationClip clip;`
- `public float delay = 0f;`
- `public CharacterAnimationQueuedClip Clone()
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterSpineEventSystem.cs`
- `public enum CharacterSpineSignalType
{`
- `public readonly struct CharacterSpineAnimationSignal
{`
- `public readonly CharacterSpineSignalType type;`
- `public readonly string animationName;`
- `public readonly string eventName;`
- `public readonly int trackIndex;`
- `public readonly float trackTime;`
- `public CharacterSpineAnimationSignal(
        CharacterSpineSignalType type,
        string animationName,
        string eventName,
        int trackIndex,
        float trackTime
    )
    {`
- `public class CharacterSpineEventSystem : MonoBehaviour, ICharacterSubsystem
{`
- `public event Action<CharacterSpineAnimationSignal> OnSpineSignal;`
- `public void Initialize(CharacterRoot root)
    {`
### `Assets/Script/Character/Core/Systems/AnimationSystem/IAnimationProvider.cs`
- `public interface IAnimationProvider
{`
### `Assets/Script/Character/Core/Systems/CameraSystem/CharacterCameraTargetSystem.cs`
- `public class CharacterCameraTargetSystem : MonoBehaviour, ICharacterSubsystem
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public Transform GetCameraTarget()
    {`
### `Assets/Script/Character/Core/Systems/CameraSystem/ProCameraRuntimeBinder.cs`
- `public class ProCameraRuntimeBinder : MonoBehaviour
{`
- `public void BindTarget(Transform target)
    {`
### `Assets/Script/Character/Core/Systems/InputHandler.cs`
- `public class InputHandler : MonoBehaviour,ICharacterSubsystem, ICharacterTickable,ICharacterLateTickable
{`
- `public InputAction movementAction;`
- `public InputAction jumpAction;`
- `public InputAction forceSlowWalkAction;`
- `public InputAction crouchAction;`
- `public InputAction equipOrReloadAction;`
- `public InputAction dushOrAvoidAction;`
- `public InputAction avoidAction;`
- `public InputAction mouseAction;`
- `public InputAction gamePad_MouseAction;`
- `public InputAction HunterWeaponAttack_Action;`
- `public InputAction HoundWeaponAttack_Action;`
- `public InputAction AimAction;`
- `public InputAction weaponSlot1Action;`
- `public InputAction weaponSlot2Action;`
- `public InputAction weaponSlot3Action;`
- `public InputAction weaponCycleNextAction;`
- `public InputAction pickupAction;`
- `public InputAction packageToggleAction;`
- `public bool useNewCursorSystem = true;`
- `public Vector2 currentMousePos {`
- `public Vector2 gamepadInput {`
- `public Vector2 GamepadScreenPosition;`
- `public Vector2 ActiveScreenPosition;`
- `public void Initialize(CharacterRoot root)
    {`
- `public void SetGameplayInputEnabled(bool enabled)
    {`
- `public bool IsGameplayInputEnabled => _isGameplayInputEnabled;`
- `public void ManagedUpdate(float deltaTime)
    {`
- `public void ManagedLateUpdate(float deltaTime)//在LateUpdate中清理这些只需要存活一帧的标记，确保它们不会被下一帧误用
    {`
- `public void Input_Movement()
    {`
- `public void Input_ForceSlowWalk(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press）按下后强制慢走，松开或冲刺时取消
    {`
- `public void Input_Crouch(InputAction.CallbackContext context)
    {`
- `public void Input_DashOrAvoid(InputAction.CallbackContext context)
    {`
- `public void Input_Aim(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press)
    {`
- `public void SyncVirtualMouseToPosition(Vector2 screenPosition)
    {`
- `public void Input_EquipOrReload(InputAction.CallbackContext context)
    {`
- `public void Input_Jump(InputAction.CallbackContext context)
    {`
- `public void HunterWeapon_Attack(InputAction.CallbackContext context)
    {`
- `public void HoundWeapon_Attack(InputAction.CallbackContext context)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/InventorySlotData.cs`
- `public class InventorySlotData
{`
- `public int slotIndex;`
- `public ItemInstance item;`
- `public bool IsEmpty => item == null || item.definition == null;`
- `public InventorySlotData(int index)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemCategory.cs`
- `public enum ItemCategory
{`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemDefinitionSO.cs`
- `public abstract class ItemDefinitionSO : ScriptableObject
{`
- `public string ItemId => itemId;`
- `public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;`
- `public ItemCategory Category => category;`
- `public Sprite InventoryIcon => inventoryIcon;`
- `public bool Stackable => stackable;`
- `public int MaxStack => Mathf.Max(1, maxStack);`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationComposeContext.cs`

```cs
using System.Collections.Generic;

public class AnimationComposeContext
{
    public CharacterRoot root;
    public CharacterDataBus dataBus;

    public IReadOnlyDictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents;

    public bool HasWeapon
    {
        get
        {
            return dataBus != null && dataBus.currentWeaponRuntimeData != null;
        }
    }

    public bool HasIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return false;

        return activeIntents != null && activeIntents.ContainsKey(source);
    }

    public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return null;

        activeIntents.TryGetValue(source, out AnimationIntent intent);
        return intent;
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationHandleSystem.cs`

```cs
using Spine;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

public class AnimationHandleSystem : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private SkeletonAnimation skeletonAnimation;

    public CharacterAnimationPlan currentPlan;
    private bool _isInitialized;

    private readonly Dictionary<int, TrackRuntimeInfo> trackRuntimeInfos =
    new Dictionary<int, TrackRuntimeInfo>();

    private class TrackRuntimeInfo
    {
        public string animName;
        public bool loop;
        public AnimationIntentSourceStateMachine sourceStateMachine;
        public int replayId;

        public string queuedAnimName;
        public bool queuedLoop;
        public AnimationIntentSourceStateMachine queuedSource;
        public int queuedReplayId;
    }

    private void Awake()
    {
        EventCenter.Instance.AddEventListener<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            OnAnimationPlanReceived
        );
    }

    private void OnDestroy()
    {
        if (EventCenter.Instance == null)
            return;

        EventCenter.Instance.RemoveEventListener<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            OnAnimationPlanReceived
        );
    }

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;
        skeletonAnimation = root.skeletonAnimation;
        _isInitialized = true;
        Debug.Log($"[AnimHandle] skeleton={skeletonAnimation.name}, id={skeletonAnimation.GetInstanceID()}", this);
    }

    private void OnAnimationPlanReceived(CharacterAnimationPlan plan)
    {
        //1.收集 used tracks
        //2.处理 clearTracks
        //3.清 stale tracks
        //4.Play clips
        //5.Queue queuedClips
        //6.Apply skeleton

        if (plan == null)
        {
            Debug.LogWarning($"[{name}] 收到空动画计划");
            return;
        }

        if (plan.characterRoot != characterRoot)
            return;

        if (skeletonAnimation == null)
            return;

        ValidatePlan(plan);
        currentPlan = plan;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        HashSet<int> tracksUsedThisFrame = new HashSet<int>();
        HashSet<int> tracksExplicitlyCleared = new HashSet<int>();

        // 1. 收集计划中明确要播放的轨道
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip == null)
                    continue;

                if (clip.track < 0)
                    continue;

                tracksUsedThisFrame.Add(clip.track);
            }
        }

        // 2. 收集计划中明确要排队播放的轨道
        if (plan.queuedClips != null)
        {
            for (int i = 0; i < plan.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queuedClip = plan.queuedClips[i];

                if (queuedClip == null || queuedClip.clip == null)
                    continue;

                CharacterAnimationClip clip = queuedClip.clip;

                if (clip.track < 0)
                    continue;

                tracksUsedThisFrame.Add(clip.track);
            }
        }

        // 3. 收集显式清空轨道
        if (plan.clearTracks != null)
        {
            for (int i = 0; i < plan.clearTracks.Count; i++)
            {
                int track = plan.clearTracks[i];

                if (track < 0)
                    continue;

                if (track == 0)
                {
                    Debug.LogWarning(
                        $"[{name}] 试图 ClearTrack(0)，Track 0 是基础全身层，不应清空。",
                        this
                    );
                    continue;
                }

                tracksExplicitlyCleared.Add(track);
            }
        }

        // 4. 处理显式要清空的轨道
        foreach (int track in tracksExplicitlyCleared)
        {
            ClearTrackImmediate(track);
        }

        // 5. 清理 stale tracks
        List<int> staleTracks = null;

        foreach (KeyValuePair<int, TrackRuntimeInfo> pair in trackRuntimeInfos)
        {
            int track = pair.Key;

            if (track == 0)
                continue;

            if (tracksUsedThisFrame.Contains(track))
                continue;

            if (tracksExplicitlyCleared.Contains(track))
                continue;

            staleTracks ??= new List<int>();
            staleTracks.Add(track);
        }

        if (staleTracks != null)
        {
            for (int i = 0; i < staleTracks.Count; i++)
            {
                ClearTrackImmediate(staleTracks[i]);
            }
        }

        // 6. SetAnimation
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                PlayClipInternal(plan.clips[i]);
            }
        }

        // 7. AddAnimation
        if (plan.queuedClips != null)
        {
            for (int i = 0; i < plan.queuedClips.Count; i++)
            {
                QueueClipInternal(plan.queuedClips[i]);
            }
        }

        skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
    }


    private void ValidatePlan(CharacterAnimationPlan plan)
    {
        if (plan == null)
            return;

        // 1. 警告：清 Track 0
        if (plan.clearTracks != null)
        {
            for (int i = 0; i < plan.clearTracks.Count; i++)
            {
                int track = plan.clearTracks[i];

                if (track == 0)
                {
                    Debug.LogWarning(
                        $"[AnimationHandleSystem] 危险操作：FinalPlan 包含 Clear Track 0，Track 0 是基础全身层，通常不应该被清空。",
                        this
                    );
                }
            }
        }

        // 2. 警告：Movement 动画放到 Track 0 以外
        if (plan.clips != null)
        {
            for (int i = 0; i < plan.clips.Count; i++)
            {
                CharacterAnimationClip clip = plan.clips[i];

                if (clip == null)
                    continue;

                if (!string.IsNullOrEmpty(clip.animaName) &&
                    clip.animaName.StartsWith("Movement/") &&
                    clip.track != 0)
                {
                    Debug.LogWarning(
                        $"[AnimationHandleSystem] 危险操作：移动动画 {clip.animaName} 被放到 Track {clip.track}，通常所有 Movement 动画应该使用 Track 0。",
                        this
                    );
                }
            }
        }
    }



    private void PlayClipInternal(CharacterAnimationClip clip)
    {
        if (clip == null)
            return;

        if (string.IsNullOrEmpty(clip.animaName))
        {
            Debug.LogWarning($"[{name}] 收到空动画名");
            return;
        }

        if (clip.track < 0)
        {
            Debug.LogWarning($"[{name}] 动画 Track 不合法: {clip.track}");
            return;
        }

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        int track = clip.track;

        bool sameRuntimeCommand = false;

        if (trackRuntimeInfos.TryGetValue(track, out TrackRuntimeInfo info))
        {
            sameRuntimeCommand =
                info.animName == clip.animaName &&
                info.loop == clip.loop &&
                info.sourceStateMachine == clip.runtimeSourceStateMachine &&
                info.replayId == clip.runtimeReplayId;
        }

        TrackEntry currentTrack = state.GetTrack(track);

        bool spineActuallySame =
            currentTrack != null &&
            currentTrack.Animation != null &&
            currentTrack.Animation.Name == clip.animaName &&
            currentTrack.Loop == clip.loop;

        // 关键：
        // 同一个 Track、同一个动画、同一个来源、同一个 replayId，
        // 说明只是 Plan 重新分发，不需要重新播放。
        if (sameRuntimeCommand && spineActuallySame)
        {
            currentTrack.MixDuration = clip.mixDuration;
            currentTrack.Additive = clip.isAdditive;
            return;
        }

        // replayId 不同，或者动画不同，都需要 SetAnimation
        TrackEntry trackEntry = state.SetAnimation(
            track,
            clip.animaName,
            clip.loop
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        trackRuntimeInfos[track] = new TrackRuntimeInfo
        {
            animName = clip.animaName,
            loop = clip.loop,
            sourceStateMachine = clip.runtimeSourceStateMachine,
            replayId = clip.runtimeReplayId,
            queuedAnimName = null,
            queuedLoop = false,
            queuedSource = AnimationIntentSourceStateMachine.None,
            queuedReplayId = 0
        };
    }

    public TrackEntry PlayPrepAnimation(CharacterAnimationClip clip, System.Action onPrepComplete)
    {
        if (clip == null || string.IsNullOrEmpty(clip.animaName) || skeletonAnimation == null)
            return null;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        TrackEntry trackEntry = state.SetAnimation(
            clip.track,
            clip.animaName,
            clip.loop
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        trackRuntimeInfos[clip.track] = new TrackRuntimeInfo
        {
            animName = clip.animaName,
            loop = clip.loop,
            sourceStateMachine = clip.runtimeSourceStateMachine,
            replayId = clip.runtimeReplayId,
            queuedAnimName = null,
            queuedLoop = false,
            queuedSource = AnimationIntentSourceStateMachine.None,
            queuedReplayId = 0
        };

        trackEntry.Complete += _ =>
        {
            onPrepComplete?.Invoke();
        };

        return trackEntry;
    }

    private void DebugPrintTracks()
    {
        if (skeletonAnimation == null)
            return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        for (int i = 0; i < 4; i++)
        {
            TrackEntry entry = state.GetTrack(i);

            string animName = entry != null && entry.Animation != null
                ? entry.Animation.Name
                : "NULL";

            Debug.Log(
                $"[Spine Track Debug] Track {i}: {animName}, " +
                $"Loop={(entry != null ? entry.Loop.ToString() : "-")}, " +
                $"TrackTime={(entry != null ? entry.TrackTime.ToString("F2") : "-")}",
                this
            );
        }
    }

    private void ClearTrackImmediate(int track)
    {
        if (skeletonAnimation == null)
            return;

        if (track <= 0)
            return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        TrackEntry current = state.GetTrack(track);

        bool hasRuntimeInfo = trackRuntimeInfos.ContainsKey(track);

        // 如果 Spine 层已经没有动画，且记录也没有，
        // 就不需要重复 SetEmptyAnimation。
        if (current == null && !hasRuntimeInfo)
            return;

        TrackEntry empty = state.SetEmptyAnimation(track, 0f);

        if (empty != null)
        {
            empty.TrackEnd = 0f;
        }

        state.Update(0f);
        state.Apply(skeletonAnimation.Skeleton);

        trackRuntimeInfos.Remove(track);
    }

    private void QueueClipInternal(CharacterAnimationQueuedClip queuedClip)
    {
        if (queuedClip == null || queuedClip.clip == null)
            return;

        CharacterAnimationClip clip = queuedClip.clip;

        if (string.IsNullOrEmpty(clip.animaName))
        {
            Debug.LogWarning($"[{name}] 收到空排队动画名");
            return;
        }

        if (clip.track < 0)
        {
            Debug.LogWarning($"[{name}] 排队动画 Track 不合法: {clip.track}");
            return;
        }

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        // 去重：如果同一轨道上已经排队了相同的动画（同名、同 loop、同来源、同 replayId），
        // 就不再重复 AddAnimation，避免多次排队导致动画被重启。
        if (trackRuntimeInfos.TryGetValue(clip.track, out TrackRuntimeInfo qInfo))
        {
            bool sameQueued =
                qInfo.queuedAnimName == clip.animaName &&
                qInfo.queuedLoop == clip.loop &&
                qInfo.queuedSource == clip.runtimeSourceStateMachine &&
                qInfo.queuedReplayId == clip.runtimeReplayId;

            if (sameQueued)
                return;
        }

        TrackEntry trackEntry = state.AddAnimation(
            clip.track,
            clip.animaName,
            clip.loop,
            queuedClip.delay
        );

        trackEntry.MixDuration = clip.mixDuration;
        trackEntry.Additive = clip.isAdditive;

        // 记录已排队的动画信息，供下次去重判断使用。
        if (!trackRuntimeInfos.TryGetValue(clip.track, out TrackRuntimeInfo qRec))
        {
            qRec = new TrackRuntimeInfo();
            trackRuntimeInfos[clip.track] = qRec;
        }
        qRec.queuedAnimName = clip.animaName;
        qRec.queuedLoop = clip.loop;
        qRec.queuedSource = clip.runtimeSourceStateMachine;
        qRec.queuedReplayId = clip.runtimeReplayId;
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationIntent.cs`

```cs
using UnityEngine;

public class AnimationIntent
{
    /// <summary>
    /// 这个请求属于哪个角色。
    /// Composer 会过滤非本角色请求。
    /// </summary>
    public CharacterRoot root;

    /// <summary>
    /// 请求来源。
    /// 例如 "Movement" / "AutoAttack" / "Aim" / "Hurt"。
    /// 同一个 source 后提交的 intent 会覆盖旧 intent。
    /// </summary>
    public AnimationIntentSourceStateMachine intentSourceStateMatine;

    /// <summary>
    /// 逻辑动画 ID。
    /// 例如 Jog_Full / Attack_Normal_1。
    /// </summary>
    public EAnimSemantic semantic;

    /// <summary>
    /// 动画逻辑层。
    /// 第一阶段主要使用 BaseFullBody 和 FullBodyAction。
    /// </summary>
    public AnimationLayer layer;

    /// <summary>
    /// 优先级。
    /// 数字越大越优先。
    /// </summary>
    public int priority;

    /// <summary>
    /// 是否独占全身。
    /// true 时会压掉移动层、上下半身层。
    /// 第一阶段攻击动作建议 true。
    /// </summary>
    public bool exclusiveFullBody;

    /// <summary>
    /// 是否是一次性动作。
    /// 例如普通攻击、受击、翻滚。
    /// </summary>
    public bool oneShot;

    /// <summary>
    /// oneShot 存活时间。
    /// 小于等于 0 时表示不自动过期。
    /// </summary>
    public float duration;

    /// <summary>
    /// Composer 内部用。
    /// 表示这个 intent 什么时候过期。
    /// </summary>
    [System.NonSerialized]
    public float expireTime;

    /// <summary>
    /// oneShot 是否强制重播。
    /// 例如连续两次 Attack_Normal_1，如果不清 Track，Spine 可能不会重新播放。
    /// </summary>
    //public bool forceReplay;
    // 新增：forceReplay 是否已经被这次 Intent 消费过
    public bool forceReplayConsumed;
    /// <summary>
    /// 同一个动画名的第几次播放。
    /// 只有 replayId 改变时，HandleSystem 才真正重播。
    /// </summary>
    public int replayId;

    public AnimationIntent Clone()
    {
        return new AnimationIntent
        {
            root = root,
            intentSourceStateMatine = intentSourceStateMatine,
            semantic = semantic,

            layer = layer,
            priority = priority,
            exclusiveFullBody = exclusiveFullBody,

            oneShot = oneShot,
            duration = duration,
            expireTime = expireTime,

            //forceReplay = forceReplay,
            replayId = replayId,

            forceReplayConsumed = forceReplayConsumed
        };
    }



}

[SerializeField]
public static class AnimationIntentSource
{
    public const string Movement = "Movement";
    public const string AutoAttack = "AutoAttack";
    public const string Aim = "Aim";
    public const string Hurt = "Hurt";
    public const string Skill = "Skill";
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationLogicRouterSO.cs`

```cs
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationLogicRouter", menuName = "ScriptableObject/Animation/AnimationLogicRouter")]
public class AnimationLogicRouterSO : ScriptableObject
{
    public enum ConditionType
    {
        Always,

        HasWeapon,
        NoWeapon,

        HasIntent,
        NotHasIntent,

        DataBusBoolTrue,
        DataBusBoolFalse,

        DataBusEnumMatch,
        DataBusStringMatch,
    }

    [System.Serializable]
    public class Condition
    {
        [HorizontalGroup("Cond", Width = 130)]
        [LabelWidth(45)]
        public ConditionType type = ConditionType.Always;

        [HorizontalGroup("Cond", Width = 160)]
        [LabelWidth(55)]
        [Tooltip("HasIntent / NotHasIntent use this. e.g. AutoAttack / Aim / Skill")]
        [ShowIf("@type == ConditionType.HasIntent || type == ConditionType.NotHasIntent")]
        public AnimationIntentSourceStateMachine intentSource = AnimationIntentSourceStateMachine.None;

        [HorizontalGroup("Cond", Width = 160)]
        [LabelWidth(50)]
        [Tooltip("DataBus field name")]
        [ShowIf("@type == ConditionType.DataBusBoolTrue || type == ConditionType.DataBusBoolFalse || type == ConditionType.DataBusEnumMatch || type == ConditionType.DataBusStringMatch")]
        public string dataBusFieldName;

        [HorizontalGroup("Cond", Width = 120)]
        [LabelWidth(35)]
        [Tooltip("Enum/String compare value")]
        [ShowIf("@type == ConditionType.DataBusEnumMatch || type == ConditionType.DataBusStringMatch")]
        public string compareString;
    }

    [System.Serializable]
    public class RouteRule
    {
        [HorizontalGroup("Rule", Width = 70)]
        [LabelWidth(40)]
        [Tooltip("Match the Intent source? Uncheck = any source")]
        public bool matchSource = false;

        [HorizontalGroup("Rule", Width = 170)]
        [LabelWidth(50)]
        [Tooltip("Which state machine submitted the Intent, e.g. Movement / AutoAttack")]
        [ShowIf("matchSource")]
        public AnimationIntentSourceStateMachine source = AnimationIntentSourceStateMachine.None;

        [HorizontalGroup("Rule", Width = 170)]
        [LabelWidth(90)]
        [Tooltip("Input semantic, e.g. Jog")]
        public EAnimSemantic inputSemantic;

        [HorizontalGroup("Rule", Width = 190)]
        [LabelWidth(65)]
        [ValueDropdown("GetOutputIDLabels", IsUniqueList = false)]
        [Tooltip("Output logic state, e.g. Jog_LowerOnly_Attack")]
        public EAnimationLogicState outputID;

        [HorizontalGroup("Rule", Width = 80)]
        [LabelWidth(45)]
        [Tooltip("Higher = higher priority")]
        public int priority = 0;

        [FoldoutGroup("Rule/Conditions", false)]
        [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 3)]
        [Tooltip("All conditions must be met")]
        public List<Condition> conditions = new List<Condition>();

        private static IEnumerable<ValueDropdownItem<EAnimationLogicState>> GetOutputIDLabels()
        {
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Melee ——", Value = (EAnimationLogicState)(-1) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 1", Value = EAnimationLogicState.Attack_Normal_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 2", Value = EAnimationLogicState.Attack_Normal_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 3", Value = EAnimationLogicState.Attack_Normal_3 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Movement ——", Value = (EAnimationLogicState)(-2) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk", Value = EAnimationLogicState.Walk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk Back", Value = EAnimationLogicState.WalkBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog", Value = EAnimationLogicState.Jog };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Back", Value = EAnimationLogicState.JogBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Fast", Value = EAnimationLogicState.JogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Dash", Value = EAnimationLogicState.Dash };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle", Value = EAnimationLogicState.Idle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Crouch", Value = EAnimationLogicState.Crouch };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Firearm ——", Value = (EAnimationLogicState)(-3) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim", Value = EAnimationLogicState.Aim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Fire", Value = EAnimationLogicState.Fire };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Reload", Value = EAnimationLogicState.Reload };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Weapon LowReady", Value = EAnimationLogicState.Weapon_LowReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAim ShootReady", Value = EAnimationLogicState.NotAimShootReady };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Avoid", Value = EAnimationLogicState.Avoid_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Jump ——", Value = (EAnimationLogicState)(-4) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump PreStart", Value = EAnimationLogicState.Jump_PreStart_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Start", Value = EAnimationLogicState.Jump_Start_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Rising", Value = EAnimationLogicState.Jump_Rising_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Peak", Value = EAnimationLogicState.Jump_Peak_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump 2nd Air", Value = EAnimationLogicState.JumpSecond__Air_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Fall", Value = EAnimationLogicState.Jump_Fall_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Land", Value = EAnimationLogicState.Jump_Land_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Transition ——", Value = (EAnimationLogicState)(-5) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog End", Value = EAnimationLogicState.Jog_End_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Turn Around", Value = EAnimationLogicState.TurnAround };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim>Gundown", Value = EAnimationLogicState.AimToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>Aim", Value = EAnimationLogicState.GundownToAim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>NotAimShoot", Value = EAnimationLogicState.GundownToNotAimShoot };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAimShoot>Gundown", Value = EAnimationLogicState.NotAimShootToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogFast", Value = EAnimationLogicState.IdleToJogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogSlow", Value = EAnimationLogicState.IdleToJogSlow };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>Walk", Value = EAnimationLogicState.IdleToWalk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogFast>Idle", Value = EAnimationLogicState.JogFastToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogSlow>Idle", Value = EAnimationLogicState.JogSlowToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk>Idle", Value = EAnimationLogicState.WalkToIdle };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Combat/Skill ——", Value = (EAnimationLogicState)(-6) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Combat Idle", Value = EAnimationLogicState.CombatIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Notice", Value = EAnimationLogicState.SkillNotice };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Ready", Value = EAnimationLogicState.SkillReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 1", Value = EAnimationLogicState.Skill_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 2", Value = EAnimationLogicState.Skill_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 3", Value = EAnimationLogicState.Skill_3 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 4", Value = EAnimationLogicState.Skill_4 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 5", Value = EAnimationLogicState.Skill_5 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 6", Value = EAnimationLogicState.Skill_6 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 7", Value = EAnimationLogicState.Skill_7 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 8", Value = EAnimationLogicState.Skill_8 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 9", Value = EAnimationLogicState.Skill_9 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 10", Value = EAnimationLogicState.Skill_10 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 11", Value = EAnimationLogicState.Skill_11 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 12", Value = EAnimationLogicState.Skill_12 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 13", Value = EAnimationLogicState.Skill_13 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 14", Value = EAnimationLogicState.Skill_14 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 15", Value = EAnimationLogicState.Skill_15 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 16", Value = EAnimationLogicState.Skill_16 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "None", Value = EAnimationLogicState.None };
        }
    }

    [TabGroup("Route Rules", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesMelee = new List<RouteRule>();

    [TabGroup("Route Rules", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesMovement = new List<RouteRule>();

    [TabGroup("Route Rules", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesFirearm = new List<RouteRule>();

    [TabGroup("Route Rules", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesJump = new List<RouteRule>();

    [TabGroup("Route Rules", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesTransition = new List<RouteRule>();

    [TabGroup("Route Rules", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesSkill = new List<RouteRule>();

    [TabGroup("Route Rules", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<RouteRule> rulesOther = new List<RouteRule>();

    [HideInInspector]
    public List<RouteRule> rules = new List<RouteRule>();

    [TabGroup("Route Rules", "Migration")]
    [Button("Migrate old rules to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateRules()
    {
        if (rules == null || rules.Count == 0)
        {
            Debug.Log("[LogicRouter] rules is empty, nothing to migrate.");
            return;
        }

        int migrated = 0;
        foreach (var rule in rules)
        {
            if (rule == null) continue;
            var list = GetListForRule(rule);
            list.Add(rule);
            migrated++;
        }

        rules.Clear();
        Debug.Log($"[LogicRouter] Migration complete, {migrated} entries moved.");
    }

    private List<RouteRule> GetListForRule(RouteRule rule)
    {
        int v = (int)rule.outputID;
        if (v >= 11 && v <= 13) return rulesMelee;
        if (v >= 21 && v <= 30) return rulesMovement;
        if (v >= 31 && v <= 40) return rulesFirearm;
        if (v >= 41 && v <= 60) return rulesJump;
        if (v >= 501 && v <= 510) return rulesTransition;
        if (v >= 60) return rulesSkill;
        return rulesOther;
    }

    private IEnumerable<RouteRule> GetAllRules()
    {
        if (rulesMelee != null) foreach (var r in rulesMelee) if (r != null) yield return r;
        if (rulesMovement != null) foreach (var r in rulesMovement) if (r != null) yield return r;
        if (rulesFirearm != null) foreach (var r in rulesFirearm) if (r != null) yield return r;
        if (rulesJump != null) foreach (var r in rulesJump) if (r != null) yield return r;
        if (rulesTransition != null) foreach (var r in rulesTransition) if (r != null) yield return r;
        if (rulesSkill != null) foreach (var r in rulesSkill) if (r != null) yield return r;
        if (rulesOther != null) foreach (var r in rulesOther) if (r != null) yield return r;
        if (rules != null) foreach (var r in rules) if (r != null) yield return r;
    }

    public EAnimationLogicState Route(AnimationIntent intent, AnimationComposeContext context)
    {
        if (intent == null)
            return EAnimationLogicState.None;

        RouteRule bestRule = null;

        foreach (var rule in GetAllRules())
        {
            if (rule == null)
                continue;

            if (rule.matchSource && rule.source != intent.intentSourceStateMatine)
                continue;

            if (!rule.inputSemantic.Equals(intent.semantic))
                continue;

            if (!CheckConditions(rule, context))
                continue;

            if (bestRule == null || rule.priority > bestRule.priority)
            {
                bestRule = rule;
            }
        }

        if (bestRule != null)
            return bestRule.outputID;

        return EAnimationLogicState.None;
    }

    private bool CheckConditions(RouteRule rule, AnimationComposeContext context)
    {
        if (rule.conditions == null || rule.conditions.Count <= 0)
            return true;

        for (int i = 0; i < rule.conditions.Count; i++)
        {
            Condition condition = rule.conditions[i];

            if (!CheckCondition(condition, context))
                return false;
        }

        return true;
    }

    private bool CheckCondition(Condition condition, AnimationComposeContext context)
    {
        if (condition == null)
            return true;

        switch (condition.type)
        {
            case ConditionType.Always:
                return true;

            case ConditionType.HasWeapon:
                return context != null && context.HasWeapon;

            case ConditionType.NoWeapon:
                return context != null && !context.HasWeapon;

            case ConditionType.HasIntent:
                return context != null && context.HasIntent(condition.intentSource);

            case ConditionType.NotHasIntent:
                return context != null && !context.HasIntent(condition.intentSource);

            case ConditionType.DataBusBoolTrue:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object trueValue)
                       && trueValue is bool b1
                       && b1;

            case ConditionType.DataBusBoolFalse:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object falseValue)
                       && falseValue is bool b2
                       && !b2;

            case ConditionType.DataBusEnumMatch:
            case ConditionType.DataBusStringMatch:
                return ReadDataBusValue(context, condition.dataBusFieldName, out object value)
                       && value != null
                       && value.ToString() == condition.compareString;
        }

        return false;
    }

    private bool ReadDataBusValue(AnimationComposeContext context, string fieldName, out object value)
    {
        value = null;

        if (context == null || context.dataBus == null)
            return false;

        if (string.IsNullOrEmpty(fieldName))
            return false;

        var fieldInfo = context.dataBus.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        if (fieldInfo == null)
        {
            Debug.LogWarning($"[AnimationLogicRouterSO] CharacterDataBus field not found: {fieldName}", this);
            return false;
        }

        value = fieldInfo.GetValue(context.dataBus);
        return true;
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanMappingSO.cs`

```cs
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimPlanMapping_Base", menuName = "ScriptableObject/Animation/AnimPlanMapping")]
public class AnimationPlanMappingSO : ScriptableObject
{
    [BoxGroup("Skeleton", ShowLabel = false)]
    [LabelText("Skeleton Data")]
    public SkeletonDataAsset skeletonDataAsset;

    [System.Serializable]
    public class Entry
    {
        [HorizontalGroup("Entry", MaxWidth = 180, MinWidth = 120)]
        [LabelWidth(55)]
        [ValueDropdown("GetLogicStateLabels", IsUniqueList = true, ExcludeExistingValuesInList = true)]
        public EAnimationLogicState logicID;

        [HorizontalGroup("Entry")]
        [HideLabel]
        public CharacterAnimationPlan plan = new CharacterAnimationPlan();

        private static IEnumerable<ValueDropdownItem<EAnimationLogicState>> GetLogicStateLabels()
        {
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Melee ——", Value = (EAnimationLogicState)(-1) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 1", Value = EAnimationLogicState.Attack_Normal_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 2", Value = EAnimationLogicState.Attack_Normal_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 3", Value = EAnimationLogicState.Attack_Normal_3 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Movement ——", Value = (EAnimationLogicState)(-2) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk", Value = EAnimationLogicState.Walk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk Back", Value = EAnimationLogicState.WalkBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog", Value = EAnimationLogicState.Jog };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Back", Value = EAnimationLogicState.JogBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Fast", Value = EAnimationLogicState.JogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Dash", Value = EAnimationLogicState.Dash };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle", Value = EAnimationLogicState.Idle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Crouch", Value = EAnimationLogicState.Crouch };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Firearm ——", Value = (EAnimationLogicState)(-3) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim", Value = EAnimationLogicState.Aim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Fire", Value = EAnimationLogicState.Fire };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Reload", Value = EAnimationLogicState.Reload };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Weapon LowReady", Value = EAnimationLogicState.Weapon_LowReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAim ShootReady", Value = EAnimationLogicState.NotAimShootReady };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Avoid", Value = EAnimationLogicState.Avoid_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Jump ——", Value = (EAnimationLogicState)(-4) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump PreStart", Value = EAnimationLogicState.Jump_PreStart_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Start", Value = EAnimationLogicState.Jump_Start_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Rising", Value = EAnimationLogicState.Jump_Rising_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Peak", Value = EAnimationLogicState.Jump_Peak_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump 2nd Air", Value = EAnimationLogicState.JumpSecond__Air_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Fall", Value = EAnimationLogicState.Jump_Fall_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Land", Value = EAnimationLogicState.Jump_Land_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Transition ——", Value = (EAnimationLogicState)(-5) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog End", Value = EAnimationLogicState.Jog_End_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Turn Around", Value = EAnimationLogicState.TurnAround };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim>Gundown", Value = EAnimationLogicState.AimToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>Aim", Value = EAnimationLogicState.GundownToAim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>NotAimShoot", Value = EAnimationLogicState.GundownToNotAimShoot };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAimShoot>Gundown", Value = EAnimationLogicState.NotAimShootToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogFast", Value = EAnimationLogicState.IdleToJogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogSlow", Value = EAnimationLogicState.IdleToJogSlow };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>Walk", Value = EAnimationLogicState.IdleToWalk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogFast>Idle", Value = EAnimationLogicState.JogFastToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogSlow>Idle", Value = EAnimationLogicState.JogSlowToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk>Idle", Value = EAnimationLogicState.WalkToIdle };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Combat/Skill ——", Value = (EAnimationLogicState)(-6) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Combat Idle", Value = EAnimationLogicState.CombatIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Notice", Value = EAnimationLogicState.SkillNotice };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Ready", Value = EAnimationLogicState.SkillReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 1", Value = EAnimationLogicState.Skill_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 2", Value = EAnimationLogicState.Skill_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 3", Value = EAnimationLogicState.Skill_3 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 4", Value = EAnimationLogicState.Skill_4 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 5", Value = EAnimationLogicState.Skill_5 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 6", Value = EAnimationLogicState.Skill_6 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 7", Value = EAnimationLogicState.Skill_7 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 8", Value = EAnimationLogicState.Skill_8 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 9", Value = EAnimationLogicState.Skill_9 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 10", Value = EAnimationLogicState.Skill_10 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 11", Value = EAnimationLogicState.Skill_11 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 12", Value = EAnimationLogicState.Skill_12 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 13", Value = EAnimationLogicState.Skill_13 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 14", Value = EAnimationLogicState.Skill_14 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 15", Value = EAnimationLogicState.Skill_15 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 16", Value = EAnimationLogicState.Skill_16 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "None", Value = EAnimationLogicState.None };
        }
    }

    [TabGroup("Animation Plans", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansMelee = new List<Entry>();

    [TabGroup("Animation Plans", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansMovement = new List<Entry>();

    [TabGroup("Animation Plans", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansFirearm = new List<Entry>();

    [TabGroup("Animation Plans", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansJump = new List<Entry>();

    [TabGroup("Animation Plans", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansTransition = new List<Entry>();

    [TabGroup("Animation Plans", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansSkill = new List<Entry>();

    [TabGroup("Animation Plans", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Entry> plansOther = new List<Entry>();

    [HideInInspector]
    public List<Entry> defaultPlans = new List<Entry>();

    [TabGroup("Animation Plans", "Migration")]
    [Button("Migrate old defaultPlans to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateDefaultPlans()
    {
        if (defaultPlans == null || defaultPlans.Count == 0)
        {
            Debug.Log("[AnimPlanMapping] defaultPlans is empty, nothing to migrate.");
            return;
        }

        int migrated = 0;
        foreach (var entry in defaultPlans)
        {
            if (entry == null) continue;
            var list = GetListForEntry(entry);
            list.Add(entry);
            migrated++;
        }

        defaultPlans.Clear();
        Debug.Log($"[AnimPlanMapping] Migration complete, {migrated} entries moved.");
    }

    private List<Entry> GetListForEntry(Entry entry)
    {
        int v = (int)entry.logicID;
        if (v >= 11 && v <= 13) return plansMelee;
        if (v >= 21 && v <= 30) return plansMovement;
        if (v >= 31 && v <= 40) return plansFirearm;
        if (v >= 41 && v <= 60) return plansJump;
        if (v >= 501 && v <= 510) return plansTransition;
        if (v >= 60) return plansSkill;
        return plansOther;
    }

    private IEnumerable<Entry> GetAllEntries()
    {
        if (plansMelee != null) foreach (var e in plansMelee) if (e != null) yield return e;
        if (plansMovement != null) foreach (var e in plansMovement) if (e != null) yield return e;
        if (plansFirearm != null) foreach (var e in plansFirearm) if (e != null) yield return e;
        if (plansJump != null) foreach (var e in plansJump) if (e != null) yield return e;
        if (plansTransition != null) foreach (var e in plansTransition) if (e != null) yield return e;
        if (plansSkill != null) foreach (var e in plansSkill) if (e != null) yield return e;
        if (plansOther != null) foreach (var e in plansOther) if (e != null) yield return e;
        if (defaultPlans != null) foreach (var e in defaultPlans) if (e != null) yield return e;
    }

    public CharacterAnimationPlan GetDefault(EAnimationLogicState id)
    {
        foreach (var entry in GetAllEntries())
        {
            if (!entry.logicID.Equals(id))
                continue;

            if (entry.plan == null)
                continue;

            entry.plan.SetSkeletonDataAsset(skeletonDataAsset);
            return entry.plan;
        }

        return null;
    }

    private void OnValidate()
    {
        foreach (var entry in GetAllEntries())
        {
            if (entry == null)
                continue;

            if (entry.plan == null)
            {
                entry.plan = new CharacterAnimationPlan();
            }

            entry.plan.SetSkeletonDataAsset(skeletonDataAsset);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationPlanOverrideRuleSetSO.cs`

```cs
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "OverridePlan_Weapon", menuName = "ScriptableObject/Animation/AnimOverrideRuleSet")]
public class AnimationPlanOverrideRuleSetSO : ScriptableObject
{
    [BoxGroup("Skeleton", ShowLabel = false)]
    [LabelText("Skeleton Data")]
    public SkeletonDataAsset skeletonDataAsset;

    public enum ConditionType
    {
        Always,
        Bool,
        FloatGreater,
        FloatLess,
        EnumMatch,
        StringMatch
    }

    [System.Serializable]
    public class Rule
    {
        [HorizontalGroup("Entry", MaxWidth = 180, MinWidth = 120)]
        [LabelWidth(55)]
        [ValueDropdown("GetLogicStateLabels", IsUniqueList = true, ExcludeExistingValuesInList = true)]
        public EAnimationLogicState targetID;

        [HorizontalGroup("Entry")]
        [HideLabel]
        public CharacterAnimationPlan replacementPlan = new CharacterAnimationPlan();

        [FoldoutGroup("Entry/Condition", false)]
        [HorizontalGroup("Entry/Condition/Row", Width = 130)]
        [LabelWidth(60)]
        public ConditionType condition = ConditionType.Always;

        [HorizontalGroup("Entry/Condition/Row", Width = 150)]
        [LabelWidth(50)]
        [Tooltip("Always 条件下可留空。其余填写 CharacterDataBus 字段名")]
        public string dataBusFieldName;

        [HorizontalGroup("Entry/Condition/Row", Width = 80)]
        [LabelWidth(40)]
        [ShowIf("@condition == ConditionType.FloatGreater || condition == ConditionType.FloatLess")]
        public float threshold;

        [HorizontalGroup("Entry/Condition/Row", Width = 120)]
        [LabelWidth(35)]
        [ShowIf("@condition == ConditionType.EnumMatch || condition == ConditionType.StringMatch")]
        public string compareString;

        private static IEnumerable<ValueDropdownItem<EAnimationLogicState>> GetLogicStateLabels()
        {
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Melee ——", Value = (EAnimationLogicState)(-1) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 1", Value = EAnimationLogicState.Attack_Normal_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 2", Value = EAnimationLogicState.Attack_Normal_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Attack 3", Value = EAnimationLogicState.Attack_Normal_3 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Movement ——", Value = (EAnimationLogicState)(-2) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk", Value = EAnimationLogicState.Walk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk Back", Value = EAnimationLogicState.WalkBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog", Value = EAnimationLogicState.Jog };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Back", Value = EAnimationLogicState.JogBack };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog Fast", Value = EAnimationLogicState.JogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Dash", Value = EAnimationLogicState.Dash };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle", Value = EAnimationLogicState.Idle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Crouch", Value = EAnimationLogicState.Crouch };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Firearm ——", Value = (EAnimationLogicState)(-3) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim", Value = EAnimationLogicState.Aim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Fire", Value = EAnimationLogicState.Fire };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Reload", Value = EAnimationLogicState.Reload };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Weapon LowReady", Value = EAnimationLogicState.Weapon_LowReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAim ShootReady", Value = EAnimationLogicState.NotAimShootReady };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Avoid", Value = EAnimationLogicState.Avoid_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Jump ——", Value = (EAnimationLogicState)(-4) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump PreStart", Value = EAnimationLogicState.Jump_PreStart_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Start", Value = EAnimationLogicState.Jump_Start_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Rising", Value = EAnimationLogicState.Jump_Rising_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Peak", Value = EAnimationLogicState.Jump_Peak_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump 2nd Air", Value = EAnimationLogicState.JumpSecond__Air_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Fall", Value = EAnimationLogicState.Jump_Fall_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jump Land", Value = EAnimationLogicState.Jump_Land_Full };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Transition ——", Value = (EAnimationLogicState)(-5) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Jog End", Value = EAnimationLogicState.Jog_End_Full };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Turn Around", Value = EAnimationLogicState.TurnAround };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Aim>Gundown", Value = EAnimationLogicState.AimToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>Aim", Value = EAnimationLogicState.GundownToAim };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Gundown>NotAimShoot", Value = EAnimationLogicState.GundownToNotAimShoot };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "NotAimShoot>Gundown", Value = EAnimationLogicState.NotAimShootToGundown };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogFast", Value = EAnimationLogicState.IdleToJogFast };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>JogSlow", Value = EAnimationLogicState.IdleToJogSlow };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Idle>Walk", Value = EAnimationLogicState.IdleToWalk };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogFast>Idle", Value = EAnimationLogicState.JogFastToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "JogSlow>Idle", Value = EAnimationLogicState.JogSlowToIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Walk>Idle", Value = EAnimationLogicState.WalkToIdle };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "—— Combat/Skill ——", Value = (EAnimationLogicState)(-6) };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Combat Idle", Value = EAnimationLogicState.CombatIdle };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Notice", Value = EAnimationLogicState.SkillNotice };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill Ready", Value = EAnimationLogicState.SkillReady };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 1", Value = EAnimationLogicState.Skill_1 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 2", Value = EAnimationLogicState.Skill_2 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 3", Value = EAnimationLogicState.Skill_3 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 4", Value = EAnimationLogicState.Skill_4 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 5", Value = EAnimationLogicState.Skill_5 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 6", Value = EAnimationLogicState.Skill_6 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 7", Value = EAnimationLogicState.Skill_7 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 8", Value = EAnimationLogicState.Skill_8 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 9", Value = EAnimationLogicState.Skill_9 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 10", Value = EAnimationLogicState.Skill_10 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 11", Value = EAnimationLogicState.Skill_11 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 12", Value = EAnimationLogicState.Skill_12 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 13", Value = EAnimationLogicState.Skill_13 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 14", Value = EAnimationLogicState.Skill_14 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 15", Value = EAnimationLogicState.Skill_15 };
            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "Skill 16", Value = EAnimationLogicState.Skill_16 };

            yield return new ValueDropdownItem<EAnimationLogicState> { Text = "None", Value = EAnimationLogicState.None };
        }
    }

    [TabGroup("Override Rules", "Melee")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesMelee = new List<Rule>();

    [TabGroup("Override Rules", "Movement")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesMovement = new List<Rule>();

    [TabGroup("Override Rules", "Firearm")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesFirearm = new List<Rule>();

    [TabGroup("Override Rules", "Jump")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesJump = new List<Rule>();

    [TabGroup("Override Rules", "Transition")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesTransition = new List<Rule>();

    [TabGroup("Override Rules", "Skill")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesSkill = new List<Rule>();

    [TabGroup("Override Rules", "Other")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
    [PropertySpace(4)]
    public List<Rule> rulesOther = new List<Rule>();

    [HideInInspector]
    public List<Rule> rules = new List<Rule>();

    [TabGroup("Override Rules", "Migration")]
    [Button("Migrate old rules to tabs", ButtonSizes.Large)]
    [PropertySpace(8)]
    public void MigrateRules()
    {
        if (rules == null || rules.Count == 0)
        {
            Debug.Log("[OverrideRuleSet] rules is empty, nothing to migrate.");
            return;
        }

        int migrated = 0;
        foreach (var rule in rules)
        {
            if (rule == null) continue;
            var list = GetListForRule(rule);
            list.Add(rule);
            migrated++;
        }

        rules.Clear();
        Debug.Log($"[OverrideRuleSet] Migration complete, {migrated} entries moved.");
    }

    private List<Rule> GetListForRule(Rule rule)
    {
        int v = (int)rule.targetID;
        if (v >= 11 && v <= 13) return rulesMelee;
        if (v >= 21 && v <= 30) return rulesMovement;
        if (v >= 31 && v <= 40) return rulesFirearm;
        if (v >= 41 && v <= 60) return rulesJump;
        if (v >= 501 && v <= 510) return rulesTransition;
        if (v >= 60) return rulesSkill;
        return rulesOther;
    }

    private IEnumerable<Rule> GetAllRules()
    {
        if (rulesMelee != null) foreach (var r in rulesMelee) if (r != null) yield return r;
        if (rulesMovement != null) foreach (var r in rulesMovement) if (r != null) yield return r;
        if (rulesFirearm != null) foreach (var r in rulesFirearm) if (r != null) yield return r;
        if (rulesJump != null) foreach (var r in rulesJump) if (r != null) yield return r;
        if (rulesTransition != null) foreach (var r in rulesTransition) if (r != null) yield return r;
        if (rulesSkill != null) foreach (var r in rulesSkill) if (r != null) yield return r;
        if (rulesOther != null) foreach (var r in rulesOther) if (r != null) yield return r;
        if (rules != null) foreach (var r in rules) if (r != null) yield return r;
    }

    public CharacterAnimationPlan GetOverride(EAnimationLogicState id, CharacterDataBus dataBus)
    {
        foreach (var rule in GetAllRules())
        {
            if (!rule.targetID.Equals(id))
                continue;

            if (rule.replacementPlan == null)
                continue;

            if (!CheckCondition(rule, dataBus))
                continue;

            if (!HasUsablePlan(rule.replacementPlan))
            {
                Debug.LogWarning(
                    $"[AnimationPlanOverrideRuleSetSO] Override rule ReplacementPlan is empty, skipping. TargetID: {id}",
                    this
                );
                continue;
            }

            rule.replacementPlan.SetSkeletonDataAsset(skeletonDataAsset);
            return rule.replacementPlan;
        }

        return null;
    }

    private bool HasUsablePlan(CharacterAnimationPlan plan)
    {
        if (plan == null)
            return false;

        if (plan.clips == null || plan.clips.Count <= 0)
            return false;

        for (int i = 0; i < plan.clips.Count; i++)
        {
            CharacterAnimationClip clip = plan.clips[i];

            if (clip == null)
                continue;

            if (!string.IsNullOrEmpty(clip.animaName))
                return true;
        }

        return false;
    }

    private bool CheckCondition(Rule rule, CharacterDataBus bus)
    {
        if (rule.condition == ConditionType.Always)
            return true;

        if (bus == null)
            return false;

        if (string.IsNullOrEmpty(rule.dataBusFieldName))
            return false;

        FieldInfo fieldInfo = bus.GetType().GetField(
            rule.dataBusFieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (fieldInfo == null)
        {
            Debug.LogWarning($"[AnimationPlanOverrideRuleSetSO] CharacterDataBus field not found: {rule.dataBusFieldName}", this);
            return false;
        }

        object value = fieldInfo.GetValue(bus);

        if (value == null)
            return false;

        switch (rule.condition)
        {
            case ConditionType.Bool:
                return value is bool boolValue && boolValue;

            case ConditionType.FloatGreater:
                return TryConvertToFloat(value, out float greaterValue) && greaterValue > rule.threshold;

            case ConditionType.FloatLess:
                return TryConvertToFloat(value, out float lessValue) && lessValue < rule.threshold;

            case ConditionType.EnumMatch:
                return value.ToString() == rule.compareString;

            case ConditionType.StringMatch:
                return value.ToString() == rule.compareString;
        }

        return false;
    }

    private bool TryConvertToFloat(object value, out float result)
    {
        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0f;
        return false;
    }

    private void OnValidate()
    {
        foreach (var rule in GetAllRules())
        {
            if (rule == null)
                continue;

            if (rule.replacementPlan == null)
            {
                rule.replacementPlan = new CharacterAnimationPlan();
            }

            rule.replacementPlan.SetSkeletonDataAsset(skeletonDataAsset);
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/AnimationTransitionRouter.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

public class AnimationTransitionRouter
{
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();

    private float turnAroundActiveUntil;
    private float turnAroundDuration = 0.25f;
    private const float TurnAroundFallbackDuration = 0.15f;

    [SerializeField, ReadOnly] private const float TransitionAnimCombineDuration = 0.05f;

    private class TransitionDecision
    {
        public bool hasTransitionIntent;
        public bool shouldApply;

        public EAnimationLogicState transitionLogicState = EAnimationLogicState.None;

        public TransitionSuppressReason suppressReason = TransitionSuppressReason.None;
        public bool shouldWarnWhenSkipped = true;


        public string reason;
    }

    public CharacterAnimationPlan BuildTransitionedPlan(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    CharacterAnimationPlan targetPlan,
    AnimationComposeContext context,
    CharacterAnimationConfigManager configManager)
    {
        if (intent == null || targetPlan == null || context == null || configManager == null)
            return targetPlan;

        targetPlan = NormalizeMoveBackTargetWhileMoveTurnPending(
    intent,
    targetLogicState,
    targetPlan,
    context,
    configManager,
    out targetLogicState
);

        TransitionDecision decision = EvaluateTransition(
    intent,
    previousLogicState,
    targetLogicState,
    context
        );

        /*
         * 没有任何过渡请求
         * 这种情况下不需要 Warning。
         */
        if (decision == null || !decision.hasTransitionIntent)
            return targetPlan;

        /*
         * 有过渡请求，但是被挡掉了。
         * 这种情况下只想看到的话给色提示。
         */
        if (!decision.shouldApply)
        {
            if (decision.shouldWarnWhenSkipped)
            {
                WarnTransitionSkippedOnce(
                    context,
                    intent,
                    targetLogicState,
                    decision.transitionLogicState,
                    decision.reason
                );
            }

            return targetPlan;
        }

        CharacterAnimationPlan transitionPlan =
            configManager.ResolvePlan(decision.transitionLogicState);

        /*
         * 有过渡请求也不需要播放过渡，
         * 如果没有找到对应的映射
         */
        if (transitionPlan == null)
        {
            WarnTransitionSkippedOnce(
                context,
                intent,
                targetLogicState,
                decision.transitionLogicState,
                $"缺少过渡动画映射: {decision.transitionLogicState}"
            );

            return targetPlan;
        }

        if (decision.transitionLogicState == EAnimationLogicState.TurnAround)
        {
            turnAroundDuration = ResolveAnimationDuration(transitionPlan, context);
            turnAroundActiveUntil = Time.time + turnAroundDuration;

            EAnimationLogicState fixedTargetLogicState =
                NormalizeTargetAfterTurn(targetLogicState);

            if (fixedTargetLogicState != targetLogicState)
            {
                CharacterAnimationPlan fixedTargetPlan =
                    configManager.ResolvePlan(fixedTargetLogicState);

                if (fixedTargetPlan != null)
                {
                    targetPlan = fixedTargetPlan;
                }
                else
                {
                    WarnTransitionSkippedOnce(
                        context,
                        intent,
                        targetLogicState,
                        decision.transitionLogicState,
                        $"完成后目标动画映射缺失: {fixedTargetLogicState}，继续使用原目标: {targetLogicState}"
                    );
                }
            }
        }

//        Debug.Log(
//    $"[TransitionRouter] Source={intent.intentSourceStateMatine}, " +
//    $"Semantic={intent.semantic}, " +
//    $"Previous={previousLogicState}, Target={targetLogicState}",
//    context.root
//);
        return CombineTransitionAndTarget(transitionPlan, targetPlan);
    }
    private CharacterAnimationPlan NormalizeMoveBackTargetWhileMoveTurnPending(
    AnimationIntent intent,
    EAnimationLogicState targetLogicState,
    CharacterAnimationPlan targetPlan,
    AnimationComposeContext context,
    CharacterAnimationConfigManager configManager,
    out EAnimationLogicState normalizedTargetLogicState)
    {
        normalizedTargetLogicState = targetLogicState;

        if (intent == null || context == null || context.dataBus == null || configManager == null)
            return targetPlan;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return targetPlan;

        CharacterDataBus bus = context.dataBus;

        if (bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Move)
            return targetPlan;

        if (!bus.isFacingChanging)
            return targetPlan;

        switch (targetLogicState)
        {
            case EAnimationLogicState.WalkBack:
                normalizedTargetLogicState = EAnimationLogicState.Walk;
                break;

            case EAnimationLogicState.JogBack:
                normalizedTargetLogicState = EAnimationLogicState.Jog;
                break;

            default:
                return targetPlan;
        }

        CharacterAnimationPlan normalizedPlan =
            configManager.ResolvePlan(normalizedTargetLogicState);

        if (normalizedPlan == null)
            return targetPlan;

        return normalizedPlan;
    }

    private TransitionDecision EvaluateTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        /*
         * 以后可以自己扩展：
         *
         * Idle -> StartMove
         * Move -> Stop
         * Run -> Brake
         * Ground -> JumpStart
         * Air -> Land
         * DrawWeapon -> Aim
         * ReloadStart -> ReloadLoop
         */

        TransitionDecision weaponPoseDecision =
            EvaluateWeaponPoseTransition(intent, previousLogicState, targetLogicState, context);

        if (weaponPoseDecision != null && weaponPoseDecision.hasTransitionIntent)
            return weaponPoseDecision;

        //TransitionDecision turnAroundDecision =
        //    EvaluateTurnAround(intent, targetLogicState, context);

        //if (turnAroundDecision != null && turnAroundDecision.hasTransitionIntent)
        //    return turnAroundDecision;

        TransitionDecision movementDecision =
            EvaluateMovementTransition(intent, previousLogicState, targetLogicState, context);

        if (movementDecision != null && movementDecision.hasTransitionIntent)
            return movementDecision;

        return null;
    }

    private TransitionDecision EvaluateTurnAround(
        AnimationIntent intent,
        EAnimationLogicState targetLogicState,
        AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();
        decision.transitionLogicState = EAnimationLogicState.TurnAround;

        if (intent == null || context == null || context.dataBus == null)
            return decision;
        CharacterDataBus bus = context.dataBus;

        /*
         * 第一层：TurnAround 只处理 Movement。
         * 不满足来源触发"转身"的 intent，这里不 Warning。
         */
        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return decision;

        /*
         * 只有移动方向下的朝向变化，才触发移动转身过渡。
         * Aim / Attack 时不触发 TurnAround。
         */
        if (bus.requestedFacingSource != CharacterDataBus.RequestedFacingSource.Move)
            return decision;

        if (Time.time < turnAroundActiveUntil)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.reason = "TurnAround grace period active";
            return decision;
        }
        /*
         * 当前朝向与目标朝向不同，
         * 说明已经处于"转身请求中"。
         */
        if (bus.currentFacing != bus.targetFacing)
        {
            decision.hasTransitionIntent = true;
        }
        else
        {
            return decision;
        }

        /*
         * 到这里说明有意图
         * 但移动转身请求被卡住了
         * 这里本应 return false 但应该给角色提示
         */

        if (!bus.isFacingChanging)
        {
            decision.shouldApply = false;
            decision.reason =
                "检测到 currentFacing != targetFacing，但 isFacingChanging == false，说明 FacingController 没有正确写入 DataBus，骨架可能被前面吞了。";
            return decision;
        }
//        Debug.Log(
//    $"[TurnAroundTest] APPLY TurnAround. " +
//    $"currentFacing={bus.currentFacing}, targetFacing={bus.targetFacing}, " +
//    $"isFacingChanging={bus.isFacingChanging}, " +
//    $"speed={bus.currentMoveSpeed:F2}, " +
//    $"targetLogicState={targetLogicState}",
//    context.root
//);

        decision.shouldApply = true;
        decision.reason = "需要播放 TurnAround 过渡。";

        return decision;
    }

    private EAnimationLogicState NormalizeTargetAfterTurn(EAnimationLogicState targetLogicState)
    {
        switch (targetLogicState)
        {
            case EAnimationLogicState.WalkBack:
                return EAnimationLogicState.Walk;

            case EAnimationLogicState.JogBack:
                return EAnimationLogicState.Jog;

            default:
                return targetLogicState;
        }
    }

    private float ResolveAnimationDuration(
        CharacterAnimationPlan plan,
        AnimationComposeContext context)
    {
        if (plan?.clips == null || plan.clips.Count == 0)
            return TurnAroundFallbackDuration;

        string animName = plan.clips[0]?.animaName;
        if (string.IsNullOrEmpty(animName))
            return TurnAroundFallbackDuration;

        if (context?.root?.skeletonAnimation?.Skeleton?.Data == null)
            return TurnAroundFallbackDuration;

        Spine.Animation anim = context.root.skeletonAnimation.Skeleton.Data.FindAnimation(animName);
        if (anim == null)
            return TurnAroundFallbackDuration;

        return anim.Duration;
    }

    private CharacterAnimationPlan CombineTransitionAndTarget(
        CharacterAnimationPlan transitionPlan,
        CharacterAnimationPlan targetPlan)
    {
        CharacterAnimationPlan result = new CharacterAnimationPlan();
        result.characterRoot = targetPlan.characterRoot;

        if (transitionPlan.clearTracks != null)
        {
            for (int i = 0; i < transitionPlan.clearTracks.Count; i++)
            {
                int track = transitionPlan.clearTracks[i];

                if (!result.clearTracks.Contains(track))
                    result.clearTracks.Add(track);
            }
        }

        if (targetPlan.clearTracks != null)
        {
            for (int i = 0; i < targetPlan.clearTracks.Count; i++)
            {
                int track = targetPlan.clearTracks[i];

                if (!result.clearTracks.Contains(track))
                    result.clearTracks.Add(track);
            }
        }

        if (transitionPlan.clips != null)
        {
            for (int i = 0; i < transitionPlan.clips.Count; i++)
            {
                CharacterAnimationClip clip = transitionPlan.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip copy = clip.Clone();
                copy.characterRoot = result.characterRoot;

                result.clips.Add(copy);
            }
        }

        if (targetPlan.clips != null)
        {
            for (int i = 0; i < targetPlan.clips.Count; i++)
            {
                CharacterAnimationClip clip = targetPlan.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip copy = clip.Clone();
                copy.characterRoot = result.characterRoot;

                result.queuedClips.Add(new CharacterAnimationQueuedClip
                {
                    clip = copy,
                    delay = TransitionAnimCombineDuration// 这里可以根据需要调整延迟时间，确保过渡动画播放完毕后再进入目标动画
                });
            }
        }

        return result;
    }

    private void WarnTransitionSkippedOnce(
        AnimationComposeContext context,
        AnimationIntent intent,
        EAnimationLogicState targetLogicState,
        EAnimationLogicState transitionLogicState,
        string reason)
    {
        string characterName = context != null && context.root != null
            ? context.root.name
            : "NULL";

        AnimationIntentSourceStateMachine source =
            intent != null
                ? intent.intentSourceStateMatine
                : AnimationIntentSourceStateMachine.None;

        EAnimSemantic semantic =
            intent != null
                ? intent.semantic
                : EAnimSemantic.None;

        string key =
            $"{characterName}|{source}|{semantic}|{targetLogicState}|{transitionLogicState}|{reason}";

        if (warnedKeys.Contains(key))
            return;

        warnedKeys.Add(key);

        Object contextObject = context != null && context.root != null
            ? context.root
            : null;

        Debug.LogWarning(
            $"[AnimationTransitionRouter] 检测到过渡请求，但未播放过渡。\n" +
            $"Character: {characterName}\n" +
            $"Source: {source}\n" +
            $"Semantic: {semantic}\n" +
            $"TargetLogicState: {targetLogicState}\n" +
            $"Transition: {transitionLogicState}\n" +
            $"Reason: {reason}",
            contextObject
        );
    }
    private TransitionDecision EvaluateWeaponPoseTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();

        if (intent == null || context == null)
            return decision;

        // 只处理攻击/瞄准的来源，不处理 Movement 也不是枪的过渡。
        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.HoundWeaponAction &&
            intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.HoundWeaponPose)
        {
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.None)
            return decision;

        if (previousLogicState == targetLogicState)
            return decision;

        if (previousLogicState == EAnimationLogicState.Aim &&
            targetLogicState == EAnimationLogicState.Weapon_LowReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.AimToGundown;
            decision.reason = "Aim -> GunDown";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.Weapon_LowReady &&
            targetLogicState == EAnimationLogicState.Aim)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.GundownToAim;
            decision.reason = "GunDown -> Aim";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.Weapon_LowReady &&
            targetLogicState == EAnimationLogicState.NotAimShootReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.GundownToNotAimShoot;
            decision.reason = "GunDown -> NotAimShoot";
            return decision;
        }

        if (previousLogicState == EAnimationLogicState.NotAimShootReady &&
            targetLogicState == EAnimationLogicState.Weapon_LowReady)
        {
            decision.hasTransitionIntent = true;
            decision.shouldApply = true;
            decision.transitionLogicState = EAnimationLogicState.NotAimShootToGundown;
            decision.reason = "NotAimShoot -> GunDown";
            return decision;
        }
        return decision;
    }


    //=======================移动过渡解析逻辑=======================//
    public bool TryResolveMovementTransition(
    EAnimSemantic from,
    EAnimSemantic to,
    out EAnimSemantic transitionSemantic)
    {
        transitionSemantic = EAnimSemantic.None;

        if (from == to)
            return false;

        if (from == EAnimSemantic.Idle && IsMoving(to))
        {
            transitionSemantic = EAnimSemantic.MoveStart;
            return true;
        }

        if (IsMoving(from) && to == EAnimSemantic.Idle)
        {
            transitionSemantic = EAnimSemantic.MoveStop;
            return true;
        }

        if (IsOppositeDirection(from, to))
        {
            transitionSemantic = EAnimSemantic.TurnAround;
            return true;
        }

        return false;
    }

    private TransitionDecision EvaluateMovementTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    AnimationComposeContext context)
    {
        TransitionDecision decision = new TransitionDecision();

        if (intent == null || context == null)
            return decision;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return decision;

        if (previousLogicState == EAnimationLogicState.None)
            return decision;

        if (previousLogicState == targetLogicState)
            return decision;

        EAnimationLogicState transitionLogicState;

        if (!TryResolveMoveTransitionLogicState(
            previousLogicState,
            targetLogicState,
            out transitionLogicState))
        {
            return decision;
        }

        decision.hasTransitionIntent = true;
        decision.transitionLogicState = transitionLogicState;

        if (ShouldSuppressTransition(
      intent,
      previousLogicState,
      targetLogicState,
      transitionLogicState,
      context,
      out TransitionSuppressReason suppressReason,
      out string suppressText))
        {
            decision.shouldApply = false;
            decision.shouldWarnWhenSkipped = false;
            decision.suppressReason = suppressReason;
            decision.reason = suppressText;
            return decision;
        }



        decision.shouldApply = true;

        decision.reason = $"MoveTransition: {previousLogicState} -> {targetLogicState}";

        return decision;
    }

    private bool TryResolveMoveTransitionLogicState(
    EAnimationLogicState from,
    EAnimationLogicState to,
    out EAnimationLogicState transitionLogicState)
    {
        transitionLogicState = EAnimationLogicState.None;

        // Idle -> Move起步
        if (from == EAnimationLogicState.Idle)
        {
            switch (to)
            {
                case EAnimationLogicState.Walk:
                case EAnimationLogicState.WalkBack:
                    transitionLogicState = EAnimationLogicState.IdleToWalk;
                    return false;//步幅太小不需要过渡

                case EAnimationLogicState.Jog:
                case EAnimationLogicState.JogBack:
                    transitionLogicState = EAnimationLogicState.IdleToJogSlow;
                    return true;

                case EAnimationLogicState.JogFast:
                case EAnimationLogicState.Dash:
                    transitionLogicState = EAnimationLogicState.IdleToJogFast;
                    return true;
            }
        }

        // Move -> Idle停步
        if (to == EAnimationLogicState.Idle)
        {
            switch (from)
            {
                case EAnimationLogicState.Walk:
                case EAnimationLogicState.WalkBack:
                    transitionLogicState = EAnimationLogicState.WalkToIdle;
                    return false;//步幅太小不需要过渡

                case EAnimationLogicState.Jog:
                case EAnimationLogicState.JogBack:
                    transitionLogicState = EAnimationLogicState.JogSlowToIdle;
                    return true;

                case EAnimationLogicState.JogFast:
                case EAnimationLogicState.Dash:
                    transitionLogicState = EAnimationLogicState.JogFastToIdle;
                    return true;
            }
        }

        return false;
    }
    private bool IsMoving(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk
            || semantic == EAnimSemantic.Jog
            || semantic == EAnimSemantic.JogFast
            || semantic == EAnimSemantic.Walk_Back
            || semantic == EAnimSemantic.Jog_Back
            || semantic == EAnimSemantic.Dash;
    }

    private bool IsForward(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk
            || semantic == EAnimSemantic.Jog
            || semantic == EAnimSemantic.JogFast
            || semantic == EAnimSemantic.Dash;
    }

    private bool IsBackward(EAnimSemantic semantic)
    {
        return semantic == EAnimSemantic.Walk_Back
            || semantic == EAnimSemantic.Jog_Back;
    }

    private bool IsOppositeDirection(EAnimSemantic from, EAnimSemantic to)
    {
        return IsForward(from) && IsBackward(to)
            || IsBackward(from) && IsForward(to);
    }
    private bool IsMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.Walk
        || state == EAnimationLogicState.Jog
        || state == EAnimationLogicState.JogFast
        || state == EAnimationLogicState.WalkBack
        || state == EAnimationLogicState.JogBack
        || state == EAnimationLogicState.Dash;
}

private bool IsForwardMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.Walk
        || state == EAnimationLogicState.Jog
        || state == EAnimationLogicState.JogFast
        || state == EAnimationLogicState.Dash;
}




    private bool IsBackwardMoveLogicState(EAnimationLogicState state)
{
    return state == EAnimationLogicState.WalkBack
        || state == EAnimationLogicState.JogBack;
}

    private bool IsOppositeMoveLogicDirection(
    EAnimationLogicState from,
    EAnimationLogicState to)
    {
        return IsForwardMoveLogicState(from) && IsBackwardMoveLogicState(to)
            || IsBackwardMoveLogicState(from) && IsForwardMoveLogicState(to);
    }


    //通用抑制入口
    private bool ShouldSuppressTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    EAnimationLogicState transitionLogicState,
    AnimationComposeContext context,
    out TransitionSuppressReason suppressReason,
    out string reason)
    {
        suppressReason = TransitionSuppressReason.None;
        reason = null;

        if (intent == null || context == null)
            return false;

        if (ShouldSuppressMovementDirectionFlipTransition(
            intent,
            previousLogicState,
            targetLogicState,
            transitionLogicState,
            context,
            out reason))
        {
            suppressReason = TransitionSuppressReason.MovementDirectionFlip;
            return true;
        }

        return false;
    }

    // 前后移动不使用过渡动画 case 的具体规则
    private bool ShouldSuppressMovementDirectionFlipTransition(
    AnimationIntent intent,
    EAnimationLogicState previousLogicState,
    EAnimationLogicState targetLogicState,
    EAnimationLogicState transitionLogicState,
    AnimationComposeContext context,
    out string reason)
    {
        reason = null;

        if (intent.intentSourceStateMatine != AnimationIntentSourceStateMachine.Movement)
            return false;

        if (!IsMoveLogicState(previousLogicState))
            return false;

        if (!IsMoveLogicState(targetLogicState))
            return false;

        if (!IsOppositeMoveLogicDirection(previousLogicState, targetLogicState))
            return false;

        reason =
            $"移动前后方向切换不播放过渡: {previousLogicState} -> {targetLogicState}, transition={transitionLogicState}";

        return true;
    }






}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationClip.cs`

```cs
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class CharacterAnimationClip
{
    [ValueDropdown("GetAnimationNames")]
    [LabelText("Animation")]
    [LabelWidth(70)]
    public string animaName;

    [HorizontalGroup("Params", Width = 90)]
    [LabelWidth(40)]
    public int track = 0;

    [HorizontalGroup("Params", Width = 70)]
    [LabelWidth(35)]
    public bool loop = true;

    [HorizontalGroup("Params", Width = 140)]
    [LabelWidth(85)]
    public float mixDuration = 0f;

    [HorizontalGroup("Params", Width = 130)]
    [LabelWidth(85)]
    public bool isAdditive = false;

    [ShowInInspector, ReadOnly, LabelText("Skeleton")]
    [HideInInspector]
    public SkeletonDataAsset skeletonDataAsset;

    [HideInInspector] public CharacterRoot characterRoot;

    [System.NonSerialized]
    public AnimationIntentSourceStateMachine runtimeSourceStateMachine;

    [System.NonSerialized]
    public int runtimeReplayId;

    private IEnumerable<string> GetAnimationNames()
    {
        if (skeletonDataAsset == null)
            return Enumerable.Empty<string>();

        var data = skeletonDataAsset.GetSkeletonData(true);
        if (data == null)
            return Enumerable.Empty<string>();

        var names = new List<string>(data.Animations.Count);
        foreach (var anim in data.Animations)
            names.Add(anim.Name);
        names.Sort();
        return names;
    }

    public CharacterAnimationClip() { }

    public CharacterAnimationClip(string name, int track = 0)
    {
        this.animaName = name;
        this.track = track;
        this.loop = loop;
        this.mixDuration = mixDuration;
    }

    public CharacterAnimationClip Clone()
    {
        CharacterAnimationClip copy = new CharacterAnimationClip();

        copy.animaName = animaName;
        copy.track = track;
        copy.loop = loop;
        copy.mixDuration = mixDuration;
        copy.isAdditive = isAdditive;
        copy.skeletonDataAsset = skeletonDataAsset;
        copy.characterRoot = characterRoot;
        copy.runtimeSourceStateMachine = runtimeSourceStateMachine;
        copy.runtimeReplayId = runtimeReplayId;
        return copy;
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationComposer.cs`

```cs
using PixelCrushers.QuestMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CharacterAnimationComposer : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    private CharacterRoot characterRoot;
    private CharacterAnimationConfigManager configManager;

    private readonly Dictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents =
        new Dictionary<AnimationIntentSourceStateMachine, AnimationIntent>();

    private readonly Dictionary<AnimationIntentSourceStateMachine, EAnimationLogicState> lastRoutedLogicBySource =
    new Dictionary<AnimationIntentSourceStateMachine, EAnimationLogicState>();
    private AnimationTransitionRouter transitionRouter = new AnimationTransitionRouter();
    private CharacterAnimationPlan currentOutputPlan;
    public CharacterAnimationPlan CurrentOutputPlan => currentOutputPlan;
    private bool _isInitialized;
    [Header("Debug")]
    public bool debugCompose = true;
    public bool debugEveryRebuild = true;
    private List<ResolvedAnimationPlan> lastResolvedPlans;

    public enum TrackOperationType
    {
        None,
        Play,
        Clear
    }

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        configManager = root.characterAnimationConfigManager;

        _isInitialized = true;
    }

    private class TrackOperation
    {
        public TrackOperationType type;

        public int track;
        public int priority;

        public CharacterAnimationClip clip;

        public AnimationIntentSourceStateMachine sourceStateMachine;
        public int replayId;
    }

    public void ManagedUpdate(float deltaTime)
    {
        RemoveExpiredOneShotIntents();
    }

    private void TryApplyTrackOperation(Dictionary<int, TrackOperation> trackOperations,TrackOperation newOperation)
    {
        if (newOperation == null)
            return;

        if (newOperation.track < 0)
            return;

        int track = newOperation.track;

        if (!trackOperations.TryGetValue(track, out TrackOperation current))
        {
            trackOperations[track] = newOperation;
            return;
        }

        if (ShouldReplaceTrackOperation(current, newOperation))
        {
            trackOperations[track] = newOperation;
        }
    }
    private bool ShouldReplaceTrackOperation(
        TrackOperation current,
       TrackOperation candidate
    )
    {
        if (candidate == null)
            return false;

        if (current == null)
            return true;

        // 1. ���ȼ�����Ӯ
        if (candidate.priority > current.priority)
            return true;

        if (candidate.priority < current.priority)
            return false;

        // 2. ͬ���ȼ�ʱ��Play ������ Clear
        // ����ͬһ����ϣ�ͬ���ȼ� Clear �� Play �����
        if (candidate.type == TrackOperationType.Play &&
            current.type == TrackOperationType.Clear)
        {
            return true;
        }

        if (candidate.type == TrackOperationType.Clear &&
            current.type == TrackOperationType.Play)
        {
            return false;
        }

        // 3. ͬ���ȼ���ͬ����ʱ�������ĸ���ǰ��ġ�
        // ����ͬһ��Դ���� resolve �����Ľ�����Ը��Ǿɽ����
        return true;
    }

    public void SubmitIntent(AnimationIntent intent)
    {
        if (intent == null)
            return;

        if (characterRoot == null)
        {
            Debug.LogWarning("[AnimationComposer] characterRoot == null");
            return;
        }

        if (intent.root != characterRoot)
        {
            Debug.LogWarning(
                $"[AnimationComposer] root ��ƥ�䣬intent.root={intent.root?.name}, composer.root={characterRoot.name}"
            );
            return;
        }

        if ( intent.intentSourceStateMatine == AnimationIntentSourceStateMachine.None)
        {
            Debug.LogWarning("[AnimationComposer] AnimationIntent source Ϊ��");
            return;
        }
        //������intent != null
        //intent.root == characterRoot
        //intent.source �ǿգ�Ȼ��Ϳ�ʼClone��

        AnimationIntent copy = intent.Clone();
        /*
* �Է� oneShot �ĳ�����ͼ��ȥ�ء�
* ͬ��Դ��ͬ���塢ͬ���ȼ���ͬ replayId �ĳ�����ͼ������Ҫ�ظ� Rebuild��
*/
        if (!copy.oneShot &&
            activeIntents.TryGetValue(copy.intentSourceStateMatine, out AnimationIntent existing) &&
            IsSamePersistentIntent(existing, copy))
        {
            return;
        }

        if (copy.oneShot && copy.duration > 0f)
        {
            copy.expireTime = Time.time + copy.duration;
        }
        else
        {
            copy.expireTime = 0f;
        }

        activeIntents[copy.intentSourceStateMatine] = copy;
        //Clone �������ǣ�״̬���ύ���Ժ�Composer �����Լ��ĸ����������ⲿ���󱻸ġ�
        //��intent��oneShot���ԣ�������ǹ���


        RebuildAndDispatch();
    }

    private bool IsSamePersistentIntent(AnimationIntent a, AnimationIntent b)
    {
        if (a == null || b == null)
            return false;

        if (a.oneShot || b.oneShot)
            return false;

        return
            a.root == b.root &&
            a.intentSourceStateMatine == b.intentSourceStateMatine &&
            a.semantic == b.semantic &&
            a.layer == b.layer &&
            a.priority == b.priority &&
            a.exclusiveFullBody == b.exclusiveFullBody &&
            a.replayId == b.replayId;
    }

    public void RemoveIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return;

        if (activeIntents.Remove(source))
        {
            RebuildAndDispatch();
        }
    }

    public void ClearAllIntents()
    {
        if (activeIntents.Count <= 0)
            return;

        activeIntents.Clear();
        RebuildAndDispatch();
    }

    public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return null;


        activeIntents.TryGetValue(source, out AnimationIntent intent);
        return intent;
    }

    private void RemoveExpiredOneShotIntents()
    {
        if (activeIntents.Count <= 0)
            return;

        List<AnimationIntentSourceStateMachine> removeList = null;

        foreach (KeyValuePair<AnimationIntentSourceStateMachine, AnimationIntent> pair in activeIntents)
        {
            AnimationIntent intent = pair.Value;

            if (intent == null)
                continue;

            if (!intent.oneShot)
                continue;

            if (intent.expireTime <= 0f)
                continue;

            if (Time.time < intent.expireTime)
                continue;

            removeList ??= new List<AnimationIntentSourceStateMachine>();
            removeList.Add(pair.Key);
        }

        if (removeList == null)
            return;

        for (int i = 0; i < removeList.Count; i++)
        {
            activeIntents.Remove(removeList[i]);
        }

        RebuildAndDispatch();
    }

    private void RebuildAndDispatch()//����
    {
        CharacterAnimationPlan finalPlan = ComposeFinalPlan();


        if (finalPlan == null)
        {
        //    //Composer ��Ҫ��Ϊ finalPlan == null �Ͳ��ɷ�
        //    ���ٷ�һ���� plan��
            finalPlan = new CharacterAnimationPlan();
            finalPlan.characterRoot = characterRoot;
        }

        currentOutputPlan = finalPlan;

        EventCenter.Instance.EventTrigger<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            finalPlan
        );
    }

    private CharacterAnimationPlan ComposeFinalPlan()//�ѵ�ǰ���� active intents ���ºϳ�һ��
    {
        if (characterRoot == null || configManager == null)
            return null;
        AnimationComposeContext context = new AnimationComposeContext//�ȹ��������ģ���ϵ�ǰ�ֵ��д洢����������
                                                                     //��� context ��·�����á�
        {
            root = characterRoot,
            dataBus = characterRoot.characterDataBus,
            activeIntents = activeIntents
        };

        List<ResolvedAnimationPlan> resolvedPlans = new List<ResolvedAnimationPlan>();//newһ������plan

        foreach (AnimationIntent intent in activeIntents.Values)//����ͼ�����������ɹ���spine�ļ����ö����Ķ����߼�ID
        {
            //��ÿ�� intent����·�ɣ�����������Ķ����汾������Movement + Jog + HasIntent(AutoAttack)=> Jog_LowerOnly_Attack

            if (intent == null)
                continue;
            // ·�ɺ�Ľ��������ɲ��Ŷ���ID
            EAnimationLogicState routedLogicID =
             configManager.RouteIntent(intent, context);

            if (routedLogicID == EAnimationLogicState.None)
                continue;

            //�ص�ComposeFinalPlan()������ Plan����ʱ���õ��õ� ·�ɺ�ľ��嶯���߼�ID
            //������߼� ID ��ɾ���ɲ��ŵĶ������á�����״̬/����/basemap��ӳ��
            CharacterAnimationPlan rawPlan =
                configManager.ResolvePlan(routedLogicID);

            lastRoutedLogicBySource.TryGetValue(intent.intentSourceStateMatine,out EAnimationLogicState previousLogicState);

            rawPlan = transitionRouter.BuildTransitionedPlan(
    intent,
    previousLogicState,
    routedLogicID,
    rawPlan,
    context,
    configManager
            );

            if (rawPlan == null)
            {
                Debug.LogWarning(
                    $"[AnimationComposer] ResolvePlan ʧ��: source={intent.intentSourceStateMatine}, inputSemantic={intent.semantic}, routed={routedLogicID}"
                );
                continue;
            }

            // �ؼ�����Ҫֱ��ʹ�������ʲ���� Plan
            CharacterAnimationPlan plan = ClonePlan(rawPlan);

            // ReplayId ģʽ�£�����ͨ�� ClearTrack ��ǿ���ز���
            // �Ƿ��ز����� AnimationHandleSystem ���� clip.runtimeReplayId �жϡ�
            resolvedPlans.Add(new ResolvedAnimationPlan
            {
                resolvedIndex = resolvedPlans.Count,
                //������ص��ǣ�Plan �����������ã��� source / replayId / priority ������� Intent��
                sourceStateMachine = intent.intentSourceStateMatine,
                inputSemantic = intent.semantic,
                routedLogicID = routedLogicID,
                priority = intent.priority,
                replayId = intent.replayId,
                exclusiveFullBody = intent.exclusiveFullBody,
                plan = plan
            });
            lastRoutedLogicBySource[intent.intentSourceStateMatine] = routedLogicID;
        }
        if (resolvedPlans.Count <= 0)
            return null;

        lastResolvedPlans = resolvedPlans;

        CharacterAnimationPlan finalPlan = MergePlans(resolvedPlans);//�������� Plan

        if (debugCompose && debugEveryRebuild)
        {
            Debug.Log(BuildComposeDebugString(resolvedPlans, finalPlan), this);
        }

        return finalPlan;
    }
    private CharacterAnimationPlan ClonePlan(CharacterAnimationPlan source)
    {
        if (source == null)
            return null;

        CharacterAnimationPlan copy = new CharacterAnimationPlan();
        copy.characterRoot = characterRoot;

        if (source.clearTracks != null)
        {
            for (int i = 0; i < source.clearTracks.Count; i++)
            {
                copy.clearTracks.Add(source.clearTracks[i]);
            }
        }

        if (source.clips != null)
        {
            for (int i = 0; i < source.clips.Count; i++)
            {
                CharacterAnimationClip clip = source.clips[i];

                if (clip == null)
                    continue;

                CharacterAnimationClip clipCopy = clip.Clone();
                clipCopy.characterRoot = characterRoot;
                copy.clips.Add(clipCopy);
            }
        }

        if (source.queuedClips != null)
        {
            for (int i = 0; i < source.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queued = source.queuedClips[i];

                if (queued == null)
                    continue;

                CharacterAnimationQueuedClip queuedCopy = queued.Clone();

                if (queuedCopy.clip != null)
                    queuedCopy.clip.characterRoot = characterRoot;

                copy.queuedClips.Add(queuedCopy);
            }
        }

        return copy;
    }




    private string BuildComposeDebugString(List<ResolvedAnimationPlan> resolvedPlans,CharacterAnimationPlan finalPlan)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("========== [AnimationComposer Debug] ==========");

        sb.AppendLine($"Character: {(characterRoot != null ? characterRoot.name : "NULL")}");
        sb.AppendLine($"Time: {Time.time:F2}");

        sb.AppendLine();
        sb.AppendLine("---- Active Intents ----");

        if (activeIntents == null || activeIntents.Count <= 0)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            foreach (KeyValuePair<AnimationIntentSourceStateMachine, AnimationIntent> pair in activeIntents)
            {
                AnimationIntent intent = pair.Value;

                if (intent == null)
                {
                    sb.AppendLine($"  {pair.Key}: NULL");
                    continue;
                }

                sb.AppendLine(
                    $"  Source={intent.intentSourceStateMatine}, Semantic={intent.semantic}, " +
                    $"Priority={intent.priority}, OneShot={intent.oneShot}, " +

                        $"ReplayId={intent.replayId}"

                );
            }
        }

        sb.AppendLine();
        sb.AppendLine("---- Routed / Resolved Plans ----");

        if (resolvedPlans == null || resolvedPlans.Count <= 0)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            for (int i = 0; i < resolvedPlans.Count; i++)
            {
                ResolvedAnimationPlan resolved = resolvedPlans[i];

                if (resolved == null)
                {
                    sb.AppendLine($"  [{i}] NULL");
                    continue;
                }

                sb.AppendLine(
                    $"  [{i}] Source={resolved.sourceStateMachine}, " +
                    $"Input={resolved.inputSemantic}, " +
                    $"Routed={resolved.routedLogicID}, " +
                    $"Priority={resolved.priority}"
                );

                CharacterAnimationPlan plan = resolved.plan;

                if (plan == null)
                {
                    sb.AppendLine("      Plan=NULL");
                    continue;
                }

                if (plan.clearTracks != null && plan.clearTracks.Count > 0)
                {
                    sb.Append("      ClearTracks: ");

                    for (int j = 0; j < plan.clearTracks.Count; j++)
                    {
                        sb.Append(plan.clearTracks[j]);

                        if (j < plan.clearTracks.Count - 1)
                            sb.Append(", ");
                    }

                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("      ClearTracks: <empty>");
                }

                if (plan.clips != null && plan.clips.Count > 0)
                {
                    for (int j = 0; j < plan.clips.Count; j++)
                    {
                        CharacterAnimationClip clip = plan.clips[j];

                        if (clip == null)
                        {
                            sb.AppendLine($"      Clip[{j}]: NULL");
                            continue;
                        }

                        sb.AppendLine(
                            $"      Clip[{j}]: Track={clip.track}, " +
                            $"Anim={clip.animaName}, Loop={clip.loop}, " +
                            $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                        );
                    }
                }
                else
                {
                    sb.AppendLine("      Clips: <empty>");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("---- Final Plan ----");


        if (finalPlan.queuedClips != null && finalPlan.queuedClips.Count > 0)
        {
            for (int i = 0; i < finalPlan.queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip queued = finalPlan.queuedClips[i];

                if (queued == null || queued.clip == null)
                {
                    sb.AppendLine($"  FinalQueuedClip[{i}]: NULL");
                    continue;
                }

                CharacterAnimationClip clip = queued.clip;

                sb.AppendLine(
                    $"  FinalQueuedClip[{i}]: Track={clip.track}, " +
                    $"Anim={clip.animaName}, Loop={clip.loop}, " +
                    $"Delay={queued.delay}, " +
                    $"Source={clip.runtimeSourceStateMachine}, ReplayId={clip.runtimeReplayId}, " +
                    $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                );
            }
        }
        else
        {
            sb.AppendLine("  QueuedClips: <empty>");
        }






        if (finalPlan == null)
        {
            sb.AppendLine("  FinalPlan=NULL");
        }
        else
        {
            if (finalPlan.clearTracks != null && finalPlan.clearTracks.Count > 0)
            {
                sb.Append("  ClearTracks: ");

                for (int i = 0; i < finalPlan.clearTracks.Count; i++)
                {
                    sb.Append(finalPlan.clearTracks[i]);

                    if (i < finalPlan.clearTracks.Count - 1)
                        sb.Append(", ");
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("  ClearTracks: <empty>");
            }

            if (finalPlan.clips != null && finalPlan.clips.Count > 0)
            {
                for (int i = 0; i < finalPlan.clips.Count; i++)
                {
                    CharacterAnimationClip clip = finalPlan.clips[i];

                    if (clip == null)
                    {
                        sb.AppendLine($"  FinalClip[{i}]: NULL");
                        continue;
                    }

                    sb.AppendLine(
    $"  FinalClip[{i}]: Track={clip.track}, " +
    $"Anim={clip.animaName}, Loop={clip.loop}, " +
    $"Source={clip.runtimeSourceStateMachine}, ReplayId={clip.runtimeReplayId}, " +
    $"Mix={clip.mixDuration}, Additive={clip.isAdditive}"
                    );
                }
            }
            else
            {
                sb.AppendLine("  Clips: <empty>");
            }
        }

        sb.AppendLine("===============================================");

        return sb.ToString();
    }


    private CharacterAnimationPlan MergePlans(List<ResolvedAnimationPlan> resolvedPlans)
    {
        CharacterAnimationPlan finalPlan = new CharacterAnimationPlan();
        finalPlan.characterRoot = characterRoot;

        finalPlan.clips ??= new List<CharacterAnimationClip>();
        finalPlan.clearTracks ??= new List<int>();

        finalPlan.queuedClips ??= new List<CharacterAnimationQueuedClip>();


        if (resolvedPlans == null || resolvedPlans.Count <= 0)
            return finalPlan;


        //clearTracks ��׼ȷ������Ӧ���ǣ�ϣ��ĳ�� Track ���ձ�ɿա�������������һ�ֹ������,һ�����ն��������ÿ�������ʵֻ�����ֲ�����
        //1. ��һ�� Clip2. ������ Track.��������ģ����ÿ�� Track �ϣ�Play �� Clear ����Ϊ��ѡ�������뾺����
        //Clip ��һ�ֹ��������Play,ClearTrack Ҳ��һ�ֹ��������Clear.ÿ�� Track ����ֻ����һ������Ӯ��Ҫô Play��Ҫô Clear
        //�����ɿ��ƶ���ʱ��AutoAttack: Play Track 0 Attack priority 50;  Movement Idle: Play Track 0 Idle priority 10 ,  Clear Track 1 priority 10
        //���ս��Ӧ���ǣ�Track 0 Play Attack��Track 1 Clear
        // ���ı仯��
        // ÿ�� Track ���ղ���ֻ��ѡ winner clip��
        // ����ѡһ�� winner operation��
        //      Play  = ����ĳ������
        //      Clear = ���������
        Dictionary<int, TrackOperation> trackOperations =
            new Dictionary<int, TrackOperation>();

        for (int i = 0; i < resolvedPlans.Count; i++)
        {
            ResolvedAnimationPlan resolved = resolvedPlans[i];

            if (resolved == null || resolved.plan == null)
                continue;

            CharacterAnimationPlan plan = resolved.plan;

            // 1. clearTracks Ҳ��Ϊ��ѡ�����������ȼ��ٲ�
            if (plan.clearTracks != null)
            {
                for (int j = 0; j < plan.clearTracks.Count; j++)
                {
                    int track = plan.clearTracks[j];

                    if (track < 0)
                        continue;

                    TrackOperation clearOperation = new TrackOperation
                    {
                        type = TrackOperationType.Clear,
                        track = track,
                        priority = resolved.priority,

                        clip = null,

                        sourceStateMachine = resolved.sourceStateMachine,
                        replayId = resolved.replayId
                    };

                    TryApplyTrackOperation(trackOperations, clearOperation);
                }
            }

            // 2. clips ��Ϊ Play �����������ȼ��ٲ�
            if (plan.clips != null)
            {
                for (int j = 0; j < plan.clips.Count; j++)
                {
                    CharacterAnimationClip clip = plan.clips[j];

                    if (clip == null)
                        continue;

                    if (clip.track < 0)
                        continue;

                    TrackOperation playOperation = new TrackOperation
                    {
                        type = TrackOperationType.Play,
                        track = clip.track,
                        priority = resolved.priority,

                        clip = clip,

                        sourceStateMachine = resolved.sourceStateMachine,
                        replayId = resolved.replayId
                    };

                    TryApplyTrackOperation(trackOperations, playOperation);
                }
            }


            if (plan.queuedClips != null)
            {
                for (int j = 0; j < plan.queuedClips.Count; j++)
                {
                    CharacterAnimationQueuedClip queued = plan.queuedClips[j];

                    if (queued == null || queued.clip == null)
                        continue;

                    CharacterAnimationQueuedClip queuedCopy = queued.Clone();
                    queuedCopy.clip.characterRoot = characterRoot;
                    queuedCopy.clip.runtimeSourceStateMachine = resolved.sourceStateMachine;
                    queuedCopy.clip.runtimeReplayId = resolved.replayId;

                    finalPlan.queuedClips.Add(queuedCopy);
                }
            }

        }

        // ========== exclusiveFullBody suppression ==========
        // When a winning Play comes from an exclusiveFullBody source,
        // clear all other tracks so the exclusive animation owns the full body.
        // This lets GunDown stay in activeIntents (avoiding 1-2 frame gap
        // when the oneShot expires) while being visually suppressed during the attack.
        bool hasExclusiveFullBody = false;
        HashSet<int> exclusiveTracks = new HashSet<int>();

        for (int i = 0; i < resolvedPlans.Count; i++)
        {
            ResolvedAnimationPlan resolved = resolvedPlans[i];
            if (!resolved.exclusiveFullBody || resolved.plan == null)
                continue;

            if (resolved.plan.clips != null)
            {
                for (int j = 0; j < resolved.plan.clips.Count; j++)
                {
                    int track = resolved.plan.clips[j].track;
                    if (track < 0)
                        continue;

                    if (trackOperations.TryGetValue(track, out TrackOperation op)
                        && op.type == TrackOperationType.Play
                        && op.sourceStateMachine == resolved.sourceStateMachine)
                    {
                        hasExclusiveFullBody = true;
                        exclusiveTracks.Add(track);
                    }
                }
            }
        }

        if (hasExclusiveFullBody)
        {
            List<int> tracksToClear = new List<int>();
            foreach (KeyValuePair<int, TrackOperation> pair in trackOperations)
            {
                if (exclusiveTracks.Contains(pair.Key))
                    continue;

                if (pair.Value.type == TrackOperationType.Play)
                    tracksToClear.Add(pair.Key);
            }

            for (int i = 0; i < tracksToClear.Count; i++)
            {
                int track = tracksToClear[i];
                trackOperations[track] = new TrackOperation
                {
                    type = TrackOperationType.Clear,
                    track = track,
                    priority = 100,
                    clip = null,
                    sourceStateMachine = AnimationIntentSourceStateMachine.None,
                    replayId = 0
                };
            }
        }

        foreach (KeyValuePair<int, TrackOperation> pair in trackOperations)
        {
            TrackOperation operation = pair.Value;

            if (operation == null)
                continue;

            switch (operation.type)
            {
                case TrackOperationType.Clear:
                    {
                        if (!finalPlan.clearTracks.Contains(operation.track))
                        {
                            finalPlan.clearTracks.Add(operation.track);
                        }

                        break;
                    }

                case TrackOperationType.Play:
                    {
                        if (operation.clip == null)
                            break;

                        CharacterAnimationClip clipCopy = operation.clip.Clone();

                        clipCopy.characterRoot = characterRoot;
                        clipCopy.runtimeSourceStateMachine = operation.sourceStateMachine;
                        clipCopy.runtimeReplayId = operation.replayId;

                        finalPlan.clips.Add(clipCopy);

                        break;
                    }
            }
        }

        return finalPlan;
    }
    private class ResolvedAnimationPlan
    {
        public int resolvedIndex;
        public AnimationIntentSourceStateMachine sourceStateMachine;
        public EAnimSemantic inputSemantic;
        public EAnimationLogicState routedLogicID;
        public int priority;
        public int replayId;
        public bool exclusiveFullBody;
        public CharacterAnimationPlan plan;
    }

    public void ClearTracks(params int[] tracks)
    {
        CharacterAnimationPlan plan = new CharacterAnimationPlan();
        plan.characterRoot = characterRoot;

        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] >= 0 && !plan.clearTracks.Contains(tracks[i]))
                plan.clearTracks.Add(tracks[i]);
        }

        currentOutputPlan = plan;

        EventCenter.Instance.EventTrigger<CharacterAnimationPlan>(
            GameEvent.AnimationPlanRequest,
            plan
        );
    }

}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationConfigManager.cs`

```cs
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
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationPlan.cs`

```cs
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterAnimationPlan
{
    [FoldoutGroup("Clips", false)]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<CharacterAnimationClip> clips = new List<CharacterAnimationClip>();

    [FoldoutGroup("Queued Clips", false)]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<CharacterAnimationQueuedClip> queuedClips = new List<CharacterAnimationQueuedClip>();

    [FoldoutGroup("Clear Tracks", false)]
    [Tooltip("播放剪辑之前需要清空的 Spine Track，例如混合动画时清空 Track 1")]
    [ValueDropdown("@CharacterAnimationPlan.GetTrackOptions()", IsUniqueList = true)]
    public List<int> clearTracks = new List<int>();

    [HideInInspector] public CharacterRoot characterRoot;

    public static IEnumerable<ValueDropdownItem<int>> GetTrackOptions()
    {
        for (int i = 0; i <= 9; i++)
            yield return new ValueDropdownItem<int> { Text = i.ToString(), Value = i };
    }

    public CharacterAnimationPlan CloneFor(CharacterRoot root)
    {
        CharacterAnimationPlan copyPlan = new CharacterAnimationPlan();

        copyPlan.characterRoot = root;

        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                CharacterAnimationClip sourceClip = clips[i];

                if (sourceClip == null)
                    continue;

                CharacterAnimationClip clipCopy = sourceClip.Clone();
                clipCopy.characterRoot = root;

                copyPlan.clips.Add(clipCopy);
            }
        }

        if (queuedClips != null)
        {
            for (int i = 0; i < queuedClips.Count; i++)
            {
                CharacterAnimationQueuedClip sourceQueuedClip = queuedClips[i];

                if (sourceQueuedClip == null)
                    continue;

                CharacterAnimationQueuedClip queuedCopy = sourceQueuedClip.Clone();

                if (queuedCopy.clip != null)
                    queuedCopy.clip.characterRoot = root;

                copyPlan.queuedClips.Add(queuedCopy);
            }
        }

        if (clearTracks != null)
        {
            for (int i = 0; i < clearTracks.Count; i++)
            {
                copyPlan.clearTracks.Add(clearTracks[i]);
            }
        }

        return copyPlan;
    }

    public void SetSkeletonDataAsset(SkeletonDataAsset skeletonDataAsset)
    {
        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    clips[i].skeletonDataAsset = skeletonDataAsset;
                }
            }
        }

        if (queuedClips != null)
        {
            for (int i = 0; i < queuedClips.Count; i++)
            {
                if (queuedClips[i] != null && queuedClips[i].clip != null)
                {
                    queuedClips[i].clip.skeletonDataAsset = skeletonDataAsset;
                }
            }
        }
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterAnimationQueuedClip.cs`

```cs
using Sirenix.OdinInspector;
using System;

[System.Serializable]
public class CharacterAnimationQueuedClip
{
    [HorizontalGroup("Queued", Width = 220)]
    [LabelWidth(30)]
    public CharacterAnimationClip clip;

    [HorizontalGroup("Queued", Width = 70)]
    [LabelWidth(35)]
    public float delay = 0f;

    public CharacterAnimationQueuedClip Clone()
    {
        CharacterAnimationQueuedClip copy = new CharacterAnimationQueuedClip();

        copy.delay = delay;

        if (clip != null)
        {
            copy.clip = clip.Clone();
        }

        return copy;
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/CharacterSpineEventSystem.cs`

```cs
using Spine;
using Spine.Unity;
using System;
using UnityEngine;
public enum CharacterSpineSignalType
{
    EventReached,
    AnimationCompleted
}

public readonly struct CharacterSpineAnimationSignal
{
    public readonly CharacterSpineSignalType type;
    public readonly string animationName;
    public readonly string eventName;
    public readonly int trackIndex;
    public readonly float trackTime;

    public CharacterSpineAnimationSignal(
        CharacterSpineSignalType type,
        string animationName,
        string eventName,
        int trackIndex,
        float trackTime
    )
    {
        this.type = type;
        this.animationName = animationName;
        this.eventName = eventName;
        this.trackIndex = trackIndex;
        this.trackTime = trackTime;
    }
}
public class CharacterSpineEventSystem : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private SkeletonAnimation houndSkeletonAnimation;

    private bool isBound;
    private bool isInitialized;

    public event Action<CharacterSpineAnimationSignal> OnSpineSignal;

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;

        characterRoot = root;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] Initialize failed: characterRoot == null", this);
            return;
        }

        houndSkeletonAnimation = characterRoot.houndSkeletonAnimation;

        Bind();

        isInitialized = true;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        if (isBound)
            return;

        if (houndSkeletonAnimation == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] houndSkeletonAnimation == null", this);
            return;
        }

        if (houndSkeletonAnimation.AnimationState == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] AnimationState == null", this);
            return;
        }

        houndSkeletonAnimation.AnimationState.Event += HandleSpineEvent;
        houndSkeletonAnimation.AnimationState.Complete += HandleSpineAnimationComplete;
        Debug.Log($"[SpineEvent] skeleton={houndSkeletonAnimation.name}, id={houndSkeletonAnimation.GetInstanceID()}", this);
        isBound = true;
    }

    private void Unbind()
    {
        if (!isBound)
            return;

        if (houndSkeletonAnimation != null && houndSkeletonAnimation.AnimationState != null)
        {
            houndSkeletonAnimation.AnimationState.Event -= HandleSpineEvent;
            houndSkeletonAnimation.AnimationState.Complete -= HandleSpineAnimationComplete;
        }

        isBound = false;
    }

    private void HandleSpineEvent(TrackEntry trackEntry, Spine.Event spineEvent)
    {
        if (trackEntry == null || spineEvent == null || spineEvent.Data == null)
            return;

        string animationName = trackEntry.Animation != null
            ? trackEntry.Animation.Name
            : string.Empty;

        CharacterSpineAnimationSignal signal = new CharacterSpineAnimationSignal(
            CharacterSpineSignalType.EventReached,
            animationName,
            spineEvent.Data.Name,
            trackEntry.TrackIndex,
            trackEntry.TrackTime
        );

        OnSpineSignal?.Invoke(signal);
    }

    private void HandleSpineAnimationComplete(TrackEntry trackEntry)
    {
        if (trackEntry == null || trackEntry.Animation == null)
            return;

        CharacterSpineAnimationSignal signal = new CharacterSpineAnimationSignal(
            CharacterSpineSignalType.AnimationCompleted,
            trackEntry.Animation.Name,
            null,
            trackEntry.TrackIndex,
            trackEntry.TrackTime
        );

        OnSpineSignal?.Invoke(signal);
    }
}
```

### `Assets/Script/Character/Core/Systems/AnimationSystem/IAnimationProvider.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public interface IAnimationProvider
{
    //CharacterAnimationClip[] GetDesiredAnimations();

}
```

### `Assets/Script/Character/Core/Systems/CameraSystem/CharacterCameraTargetSystem.cs`

```cs
using UnityEngine;
using Cinemachine;

[DisallowMultipleComponent]
public class CharacterCameraTargetSystem : MonoBehaviour, ICharacterSubsystem
{
    [Header("相机目标点")]
    [SerializeField] private Transform cameraTarget;

    [Header("运行时自动寻找场景 Virtual Camera")]
    [SerializeField] private bool autoFindSceneVirtualCamera = true;

    [Tooltip("可选：如果场景里有多个 Virtual Camera，可以填名字，比如 Virtual Camera")]
    [SerializeField] private string virtualCameraName = "Virtual Camera";

    [SerializeField] private bool autoBindVirtualCameraFollow = true;

    [Header("基础偏移")]
    [SerializeField] private Vector3 baseLocalOffset = new Vector3(0f, 1f, 0f);

    [Header("移动前置")]
    [SerializeField] private bool useMoveLookAhead = true;
    [SerializeField] private float moveLookAheadX = 0.8f;
    [SerializeField] private float moveLookAheadY = 0.2f;

    [Header("瞄准偏移")]
    [SerializeField] private bool useAimOffset = true;
    [SerializeField] private float aimOffsetDistance = 1.5f;
    [SerializeField] private float maxAimOffsetDistance = 2f;

    [Header("平滑")]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("调试")]
    [SerializeField] private bool debugLog;

    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;

    private CinemachineVirtualCamera sceneVirtualCamera;

    private Vector3 localVelocity;
    private bool initialized;

    public void Initialize(CharacterRoot root)
    {
        characterRoot = root;
        dataBus = root.characterDataBus;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterCameraTargetSystem] CharacterRoot 为空", this);
            return;
        }

        if (dataBus == null)
        {
            Debug.LogError("[CharacterCameraTargetSystem] CharacterDataBus 为空", this);
            return;
        }

        EnsureCameraTarget();

        if (autoFindSceneVirtualCamera)
        {
            sceneVirtualCamera = FindSceneVirtualCamera();
        }

        if (autoBindVirtualCameraFollow)
        {
            BindVirtualCameraFollow();
        }

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || characterRoot == null || dataBus == null || cameraTarget == null)
            return;

        UpdateCameraTarget();

        // 防御：如果运行中虚拟相机晚于角色生成，或者 Follow 被清空，则再次尝试绑定
        if (autoBindVirtualCameraFollow)
        {
            if (sceneVirtualCamera == null)
                sceneVirtualCamera = FindSceneVirtualCamera();

            if (sceneVirtualCamera != null && sceneVirtualCamera.Follow != cameraTarget)
                BindVirtualCameraFollow();
        }
    }

    private void EnsureCameraTarget()
    {
        if (cameraTarget != null)
            return;

        Transform existTarget = transform.Find("CameraTarget");

        if (existTarget != null)
        {
            cameraTarget = existTarget;
            return;
        }

        GameObject targetObj = new GameObject("CameraTarget");
        targetObj.transform.SetParent(transform);
        targetObj.transform.localPosition = baseLocalOffset;
        targetObj.transform.localRotation = Quaternion.identity;
        targetObj.transform.localScale = Vector3.one;

        cameraTarget = targetObj.transform;
    }

    private CinemachineVirtualCamera FindSceneVirtualCamera()
    {
        CinemachineVirtualCamera[] vcams = FindObjectsOfType<CinemachineVirtualCamera>(true);

        if (vcams == null || vcams.Length == 0)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] 场景中没有找到 CinemachineVirtualCamera", this);

            return null;
        }

        // 1. 优先按名字找
        if (!string.IsNullOrEmpty(virtualCameraName))
        {
            foreach (var vcam in vcams)
            {
                if (vcam == null)
                    continue;

                if (!IsSceneObject(vcam.gameObject))
                    continue;

                if (vcam.name == virtualCameraName)
                {
                    if (debugLog)
                        Debug.Log($"[CharacterCameraTargetSystem] 按名字找到 Virtual Camera: {vcam.name}", vcam);

                    return vcam;
                }
            }
        }

        // 2. 找优先级最高的场景 Virtual Camera
        CinemachineVirtualCamera best = null;
        int bestPriority = int.MinValue;

        foreach (var vcam in vcams)
        {
            if (vcam == null)
                continue;

            if (!IsSceneObject(vcam.gameObject))
                continue;

            if (!vcam.gameObject.activeInHierarchy)
                continue;

            if (vcam.Priority > bestPriority)
            {
                best = vcam;
                bestPriority = vcam.Priority;
            }
        }

        if (best != null && debugLog)
            Debug.Log($"[CharacterCameraTargetSystem] 自动选择 Virtual Camera: {best.name}", best);

        return best;
    }

    private bool IsSceneObject(GameObject obj)
    {
        if (obj == null)
            return false;

        // Project 里的 Prefab 资产没有有效加载场景
        return obj.scene.IsValid() && obj.scene.isLoaded;
    }

    private void BindVirtualCameraFollow()
    {
        if (sceneVirtualCamera == null)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] sceneVirtualCamera 为空，无法绑定 Follow", this);

            return;
        }

        if (cameraTarget == null)
        {
            if (debugLog)
                Debug.LogWarning("[CharacterCameraTargetSystem] cameraTarget 为空，无法绑定 Follow", this);

            return;
        }

        sceneVirtualCamera.Follow = cameraTarget;
        sceneVirtualCamera.LookAt = null;

        if (debugLog)
        {
            Debug.Log(
                $"[CharacterCameraTargetSystem] 已绑定 {sceneVirtualCamera.name}.Follow = {cameraTarget.name}",
                sceneVirtualCamera
            );
        }
    }

    private void UpdateCameraTarget()
    {
        Vector3 desiredLocalPosition = baseLocalOffset;

        desiredLocalPosition += GetMoveLookAheadOffset();
        desiredLocalPosition += GetAimOffset();

        cameraTarget.localPosition = Vector3.SmoothDamp(
            cameraTarget.localPosition,
            desiredLocalPosition,
            ref localVelocity,
            smoothTime
        );

        cameraTarget.localRotation = Quaternion.identity;
    }

    private Vector3 GetMoveLookAheadOffset()
    {
        if (!useMoveLookAhead)
            return Vector3.zero;

        Vector2 moveInput = dataBus.moveInput;

        if (moveInput.sqrMagnitude < 0.01f)
            return Vector3.zero;

        return new Vector3(
            moveInput.x * moveLookAheadX,
            moveInput.y * moveLookAheadY,
            0f
        );
    }

    private Vector3 GetAimOffset()
    {
        if (!useAimOffset)
            return Vector3.zero;

        if (!dataBus.isAiming)
            return Vector3.zero;

        Vector3 aimDir = dataBus.aimDirection;
        aimDir.z = 0f;

        if (aimDir.sqrMagnitude < 0.001f)
            return Vector3.zero;

        aimDir.Normalize();

        Vector3 offset = aimDir * aimOffsetDistance;

        if (offset.magnitude > maxAimOffsetDistance)
            offset = offset.normalized * maxAimOffsetDistance;

        return offset;
    }

    public Transform GetCameraTarget()
    {
        return cameraTarget;
    }
}
```

### `Assets/Script/Character/Core/Systems/CameraSystem/ProCameraRuntimeBinder.cs`

```cs
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

public class ProCameraRuntimeBinder : MonoBehaviour
{
    [SerializeField] private ProCamera2D proCamera2D;

    private void Awake()
    {
        if (proCamera2D == null)
        {
            proCamera2D = FindObjectOfType<ProCamera2D>();
        }

        if (proCamera2D == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 场景中没有找到 ProCamera2D");
        }
    }

    public void BindTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 绑定失败：target 为 null");
            return;
        }

        if (proCamera2D == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 绑定失败：proCamera2D 为 null");
            return;
        }

        proCamera2D.RemoveAllCameraTargets();
        proCamera2D.AddCameraTarget(target);

        //Debug.Log($"[ProCameraRuntimeBinder] 已绑定相机目标：{target.name}");
    }
}
```

### `Assets/Script/Character/Core/Systems/InputHandler.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class InputHandler : MonoBehaviour,ICharacterSubsystem, ICharacterTickable,ICharacterLateTickable
{
    private bool _isInitialized;


    #region 输入系统字段声明
    [Header("移动动作")]
    public InputAction movementAction;//默认为Jog
    [Header("跳跃动作")]
    public InputAction jumpAction;//默认为Dush
    [Header("强制慢走动作")]
    public InputAction forceSlowWalkAction;//强制慢走，只在键鼠下按住时生效
    [Header("蹲下动作")]
    public InputAction crouchAction;//按住蹲下
    [Header("装弹/卸弹动作")]
    public InputAction equipOrReloadAction;//短按装弹/卸弹，长按切换
    [Header("冲刺/回避动作")]
    public InputAction dushOrAvoidAction;//默认为Dush
    [Header("回避动作")]
    public InputAction avoidAction;//回避
    [Header("瞄准设备_鼠标")]
    public InputAction mouseAction;//鼠标，手柄右摇杆控制
    [Header("瞄准设备_手柄")]
    public InputAction gamePad_MouseAction;//鼠标，手柄右摇杆控制
    [Header("猎人武器攻击动作")]
    public InputAction HunterWeaponAttack_Action;//攻击按钮，手柄直接扳机

    [Header("猎犬武器攻击动作")]
    public InputAction HoundWeaponAttack_Action;//攻击按钮，手柄直接扳机

    [Header("瞄准动作")]
    public InputAction AimAction;//鼠标右键，手柄LT键

    [Header("武器槽位切换")]
    [Tooltip("手柄选择武器槽位 1")]
    public InputAction weaponSlot1Action;

    [Tooltip("手柄选择武器槽位 2")]
    public InputAction weaponSlot2Action;

    [Tooltip("手柄选择武器槽位 3")]
    public InputAction weaponSlot3Action;

    [Tooltip("手机端循环切换下一个武器")]
    public InputAction weaponCycleNextAction;

    [Header("拾取动作")]
    [Tooltip("按E拾取附近物品")]
    public InputAction pickupAction;

    [Header("背包开关")]
    [Tooltip("按Tab打开/关闭背包")]
    public InputAction packageToggleAction;


    #endregion
    //================== 输入处理配置 =================
    [Header("是否使用新的瞄准系统")]
    public bool useNewCursorSystem = true;

    // ==== Gameplay 输入总开关 ====
    // 当背包 UI 打开时禁用，关闭时恢复。packageToggleAction 永不参与（保证能再次按 Tab 关闭）。
    private bool _isGameplayInputEnabled = true;
    private readonly List<InputAction> _gameplayActions = new List<InputAction>();


    private Vector2 currentMoveInput { get; set; }

    private Vector2 virtualMousePosition;
    private Vector2 lastMousePos { get; set; }
    private Vector3 MousePosDelta { get; set; }
    public Vector2 currentMousePos { get; set; }
    public Vector2 gamepadInput { get; set; }
    // 两种输入设备同步位置来源
    public Vector2 GamepadScreenPosition;    // 手柄在屏幕位置
    public Vector2 ActiveScreenPosition;     // 当前有效屏幕位置

    private bool dashOrAvoidPerformed;

    private bool equipOrReloadLongPerformed;
    private CharacterRoot characterRoot { get; set; }
    private CharacterDataBus dataBus;
    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        characterRoot = root;
        dataBus = root.characterDataBus;
        this.dataBus = characterRoot.characterDataBus;
        _isInitialized = true;
    }
    void Start()
    {
        //===========输入设备初始化================
        // 初始化虚拟鼠标位置为屏幕中心
        virtualMousePosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        // 可选择是否同步真实鼠标（很多游戏不同步）
        //Mouse.current.WarpCursorPosition(virtualMousePosition);
        //===========鼠标/手柄设备初始化================
        #region 动作绑定初始化
        movementAction.Enable();//移动只需要enable即可，因为Input_Movement需要在update中每一帧调用
        mouseAction.Enable();//只需要enable即可
        gamePad_MouseAction.Enable();//只需要enable即可
        //==============强制慢走动作================
        forceSlowWalkAction.Enable();
        forceSlowWalkAction.started += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        forceSlowWalkAction.performed += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        forceSlowWalkAction.canceled += (context) =>
        {
            Input_ForceSlowWalk(context);
        };
        //=========蹲下===========
        crouchAction.Enable();
        crouchAction.started += (context) =>
        {
            Input_Crouch(context);
        };
        crouchAction.performed += (context) =>
        {
            Input_Crouch(context);
        };
        crouchAction.canceled += (context) =>
        {
            Input_Crouch(context);
        };
        //=========冲刺/回避键===========
        dushOrAvoidAction.Enable();
        dushOrAvoidAction.started += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        dushOrAvoidAction.performed += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        dushOrAvoidAction.canceled += (context) =>
        {
            Input_DashOrAvoid(context);
        };
        //=========瞄准键===========
        AimAction.Enable();
        AimAction.started += (context) =>
        {
            Input_Aim(context);
        };
        AimAction.performed += (context) =>
        {
            Input_Aim(context);
        };
        AimAction.canceled += (context) =>
        {
            Input_Aim(context);
        };
        //=========装弹/卸弹键===========
        equipOrReloadAction.Enable();
        equipOrReloadAction.started += (context) =>
        {
            Input_EquipOrReload(context);
        };
        equipOrReloadAction.performed += (context) =>
        {
            Input_EquipOrReload(context);
        };
        equipOrReloadAction.canceled += (context) =>
        {
            Input_EquipOrReload(context);
        };
        //=========跳跃键===========
        jumpAction.Enable();
        jumpAction.started += (context) =>
        {
            Input_Jump(context);
        };
        // jumpAction.performed 已移除，避免 started+performed 双触发
        jumpAction.canceled += (context) =>
        {
            Input_Jump(context);
        };
        //=========攻击键===========
        HunterWeaponAttack_Action.Enable();
        HunterWeaponAttack_Action.started += HunterWeapon_Attack;
        HunterWeaponAttack_Action.performed += HunterWeapon_Attack;
        HunterWeaponAttack_Action.canceled += HunterWeapon_Attack;

        HoundWeaponAttack_Action.Enable();
        HoundWeaponAttack_Action.started += HoundWeapon_Attack;
        HoundWeaponAttack_Action.performed += HoundWeapon_Attack;
        HoundWeaponAttack_Action.canceled += HoundWeapon_Attack;
        //===========武器切换键===========
        weaponSlot1Action.Enable();
        weaponSlot1Action.started += Input_WeaponSlot1;
        weaponSlot2Action.Enable();
        weaponSlot2Action.started += Input_WeaponSlot2;
        weaponSlot3Action.Enable();
        weaponSlot3Action.started += Input_WeaponSlot3;
        weaponCycleNextAction.Enable();
        weaponCycleNextAction.started += Input_WeaponCycleNext;

        //=========拾取键===========
        if (pickupAction == null)
        {
            pickupAction = new InputAction("Pickup", binding: "<Keyboard>/e");
        }
        pickupAction.Enable();
        pickupAction.started += Input_Pickup;

        //=========背包开关===========
        if (packageToggleAction == null)
        {
            packageToggleAction = new InputAction("PackageToggle", binding: "<Keyboard>/tab");
        }
        packageToggleAction.Enable();
        packageToggleAction.started += Input_PackageToggle;

        #endregion

        // 收集所有 Gameplay 输入动作（packageToggleAction 除外）
        RegisterGameplayAction(movementAction);
        RegisterGameplayAction(jumpAction);
        RegisterGameplayAction(forceSlowWalkAction);
        RegisterGameplayAction(crouchAction);
        RegisterGameplayAction(equipOrReloadAction);
        RegisterGameplayAction(dushOrAvoidAction);
        RegisterGameplayAction(avoidAction);
        RegisterGameplayAction(mouseAction);
        RegisterGameplayAction(gamePad_MouseAction);
        RegisterGameplayAction(HunterWeaponAttack_Action);
        RegisterGameplayAction(HoundWeaponAttack_Action);
        RegisterGameplayAction(AimAction);
        RegisterGameplayAction(weaponSlot1Action);
        RegisterGameplayAction(weaponSlot2Action);
        RegisterGameplayAction(weaponSlot3Action);
        RegisterGameplayAction(weaponCycleNextAction);
        RegisterGameplayAction(pickupAction);
    }

    private void RegisterGameplayAction(InputAction action)
    {
        if (action != null && !_gameplayActions.Contains(action))
            _gameplayActions.Add(action);
    }

    /// <summary>
    /// Master switch for Gameplay inputs (called by CharacterUIInputRouter when the backpack opens/closes).
    /// Enables/disables movement, jump, crouch, dash, aim, attack, reload, weapon switch, pickup and aim input.
    /// Never disables packageToggleAction so the player can always press Tab to close the UI.
    /// When disabling, also clears held/sticky states so nothing lingers when the UI opens.
    /// </summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        if (_isGameplayInputEnabled == enabled)
        {
            if (enabled && dataBus != null && dataBus.moveInput != Vector2.zero)
                dataBus.moveInput = Vector2.zero;
            return;
        }

        _isGameplayInputEnabled = enabled;

        foreach (var action in _gameplayActions)
        {
            if (action == null) continue;
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }

        ClearGameplayStickyStates();
        if (dataBus == null) return;
        dataBus.moveInput = Vector2.zero;
    }

    public bool IsGameplayInputEnabled => _isGameplayInputEnabled;

    private void ClearGameplayStickyStates()
    {
        currentMoveInput = Vector2.zero;
        if (dataBus == null) return;

        dataBus.aimHeld = false;
        dataBus.dashPressed = false;
        dataBus.jumpHeld = false;
        dataBus.jumpPressed = false;
        dataBus.jumpPressedThisFrame = false;
        dataBus.isForceWalkSlow = false;
        dataBus.isCrouch = false;
        dataBus.moveInput = Vector2.zero;
        dataBus.mouseMoving = false;

        if (characterRoot != null && characterRoot.characterAttackController != null)
        {
            characterRoot.characterAttackController.SetHunterAttackHeld(false);
            characterRoot.characterAttackController.SetHoundAttackHeld(false);
        }
    }


    private void OnDestroy()
    {
        if (HunterWeaponAttack_Action != null)
        {
            HunterWeaponAttack_Action.started -= HunterWeapon_Attack;
            HunterWeaponAttack_Action.performed -= HunterWeapon_Attack;
            HunterWeaponAttack_Action.canceled -= HunterWeapon_Attack;
        }

        if (HoundWeaponAttack_Action != null)
        {
            HoundWeaponAttack_Action.started -= HoundWeapon_Attack;
            HoundWeaponAttack_Action.performed -= HoundWeapon_Attack;
            HoundWeaponAttack_Action.canceled -= HoundWeapon_Attack;
        }

        #region 武器切换解绑
        if (weaponSlot1Action != null)
        {
            weaponSlot1Action.started -=
                Input_WeaponSlot1;

            weaponSlot1Action.Disable();
        }

        if (weaponSlot2Action != null)
        {
            weaponSlot2Action.started -=
                Input_WeaponSlot2;

            weaponSlot2Action.Disable();
        }

        if (weaponSlot3Action != null)
        {
            weaponSlot3Action.started -=
                Input_WeaponSlot3;

            weaponSlot3Action.Disable();
        }

        if (weaponCycleNextAction != null)
        {
            weaponCycleNextAction.started -=
                Input_WeaponCycleNext;

            weaponCycleNextAction.Disable();
        }
        #endregion

        #region 拾取/背包解绑
        if (pickupAction != null)
        {
            pickupAction.started -= Input_Pickup;
            pickupAction.Disable();
        }

        if (packageToggleAction != null)
        {
            packageToggleAction.started -= Input_PackageToggle;
            packageToggleAction.Disable();
        }
        #endregion
    }
    public void ManagedUpdate(float deltaTime)
    {
        if (!_isGameplayInputEnabled)
        {
            // 背包 UI 打开时禁止读取移动，确保角色不动
            if (dataBus != null)
                dataBus.moveInput = Vector2.zero;
            return;
        }

        Input_Movement();
        if (!useNewCursorSystem)
        {
            Input_MouseControl();
        }
    }

    public void ManagedLateUpdate(float deltaTime)//在LateUpdate中清理这些只需要存活一帧的标记，确保它们不会被下一帧误用
    {
        if (dataBus == null)
            return;

        dataBus.aimPressedThisFrame = false;
        dataBus.aimReleasedThisFrame = false;
        dataBus.houndAttackPressedThisFrame = false;
        dataBus.hunterAttackPressedThisFrame = false;
        dataBus.reloadPressedThisFrame = false;
        dataBus.weaponToggleLongPressedThisFrame = false;
        dataBus.jumpPressedThisFrame = false;
        dataBus.jumpReleasedThisFrame = false;
    }


    #region 移动系统
    public void Input_Movement()
    {
        Vector2 rawInput = movementAction.ReadValue<Vector2>();  // 获取原始的二维方向输入
        if (rawInput.magnitude > 0.1f)                 // 只有当输入超过阈值时，才认为是有效输入
        {
            currentMoveInput = rawInput.normalized;    // 关键：对输入进行标准化，确保8个方向长度一致
        }
        else
        {
            currentMoveInput = Vector2.zero;           // 没有输入时，清零移动方向
        }
        dataBus.moveInput = currentMoveInput;
        Logic_CheckMovingBackward();
    }
    private void Logic_CheckMovingBackward()//根据输入与当前朝向判断是否后退
    {
        if (dataBus.moveInput == Vector2.zero)//输入为零时不执行
        {
            dataBus.isMovingBackward = false;
            return;
        }
        // 获取角色朝向方向（标准化）
        Vector2 facingDirection2D = new Vector2(dataBus.facingDirection.x, dataBus.facingDirection.y).normalized;
        // 获取移动方向（标准化）
        // 如果点积小于0，表示方向相反
        float dotProduct = Vector2.Dot(facingDirection2D, dataBus.moveInput);
        // 点积值小于 -0.3 认为是有效后退
        dataBus.isMovingBackward = dotProduct < -0.3f;
    }
    public void Input_ForceSlowWalk(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press）按下后强制慢走，松开或冲刺时取消
    {
        if (dataBus.dashPressed)
        {
            dataBus.isForceWalkSlow = false;
            return;
        }

        bool isTriger = dataBus.forceSlowWalkInputTypeIsTriger;
        if (isTriger)
        {
            if (context.started)
            {
                bool lastTimeForceSlowWalkState = dataBus.isForceWalkSlow;
                bool newState = !lastTimeForceSlowWalkState;
                dataBus.isForceWalkSlow = newState;
            }
        }
        else
        {
            if (context.started)
            {
                dataBus.isForceWalkSlow = true;
            }

            if (context.canceled)
            {
                dataBus.isForceWalkSlow = false;
            }
        }
    }
    #endregion  
    public void Input_Crouch(InputAction.CallbackContext context)
    {
        if (dataBus.crouchInputTypeIsTriger)
        {
            if (context.started)  // 触发判断按压版本
            {
                if (!dataBus.isCrouch)
                {
                    dataBus.isCrouch = true;
                }
                else
                {
                    dataBus.isCrouch = false;
                }
            }
        }
        else
        {
            if (context.started)
            {
                dataBus.isCrouch = true;
            }
            else if (context.canceled)
            {
                dataBus.isCrouch = false;
            }
        }
    }
    #region 冲刺/回避系统（包含冲刺、回避判断，使用输入缓冲）
    public void Input_DashOrAvoid(InputAction.CallbackContext context)
    {

        //if (IsLocked) return;
        if (context.started)
        {
            dashOrAvoidPerformed = false;
        }
        if (context.performed)//需要按扭扭动因为hold模式
        {
            dataBus.dashPressed = true;//使用状态机方法分发更适合后续要处理的冲刺状态系统（继承冲刺时间、回避持续时间等），需要手动构建状态
            dashOrAvoidPerformed = true; ;
            dataBus.isForceWalkSlow = false;
        }
        if (context.canceled)
        {
            if (!dashOrAvoidPerformed)
            {
                EventCenter.Instance.EventTrigger(GameEvent.ANOIR_IsAvoid);
            }

            dataBus.dashPressed = false;
            dashOrAvoidPerformed = false;
        }
    }
    #endregion
    #region 鼠标系统

    private Vector2 Logic_MousePosition()
    {
        Vector2 hardwareMousePos = mouseAction.ReadValue<Vector2>();
        gamepadInput = gamePad_MouseAction.ReadValue<Vector2>();
        bool isGamepadActive = gamepadInput.magnitude > 0.1f;

        if (!isGamepadActive)
        {
            MousePosDelta = lastMousePos - hardwareMousePos;
        }
        bool isMouseActive = !isGamepadActive && MousePosDelta.magnitude > 0.1f;

        if (isGamepadActive)
        {
            dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Gamepad;

            // 更新手柄光标位置
            GamepadScreenPosition += gamepadInput * dataBus.gamepadMouseControlSensitivity * Time.deltaTime;
            GamepadScreenPosition.x = Mathf.Clamp(GamepadScreenPosition.x, 0, Screen.width);
            GamepadScreenPosition.y = Mathf.Clamp(GamepadScreenPosition.y, 0, Screen.height);

            ActiveScreenPosition = GamepadScreenPosition;
            lastMousePos = hardwareMousePos;

            return ActiveScreenPosition;
        }
        else if (isMouseActive)
        {
            dataBus.currentInputDevice = CharacterDataBus.InputDevice_AimType.Mouse;

            ActiveScreenPosition = hardwareMousePos;
            GamepadScreenPosition = hardwareMousePos;
            lastMousePos = hardwareMousePos;

            return ActiveScreenPosition;
        }
        else
        {
            if (dataBus.currentInputDevice == CharacterDataBus.InputDevice_AimType.Gamepad)
            {
                ActiveScreenPosition = GamepadScreenPosition;
            }
            else
            {
                ActiveScreenPosition = hardwareMousePos;
                GamepadScreenPosition = hardwareMousePos;
                lastMousePos = hardwareMousePos;
            }

            return ActiveScreenPosition;
        }
    }
    Vector3 Logic_GetMouseWorldPositionOnPlane(Vector3 planePosition)
    {
        Vector2 getMouseInputposi = Logic_MousePosition();
        Ray ray = Camera.main.ScreenPointToRay(getMouseInputposi);
        Plane plane = new Plane(Vector3.forward, planePosition); // 使用角色的 Z 平面
        float distance;
        if (plane.Raycast(ray, out distance))//可能，会返回一个distance，表示射线与平面的交点距离
        {
            return ray.GetPoint(distance);//如果有交点，返回交点坐标
        }
        return Vector3.zero; // 如果射线未命中平面（几乎不会发生）
    }
    private void Input_MouseControl()
    {
        Vector3 MouseWorldPosition = Logic_GetMouseWorldPositionOnPlane(dataBus.characterPosition);
        MouseWorldPosition.z = dataBus.characterPosition.z; // 确保 Z 一致
        dataBus.mousePositionOnScreen = MouseWorldPosition;
        if (MousePosDelta.magnitude > 0.1f || gamepadInput.magnitude > 0.1f)
        {
            dataBus.mouseMoving = true;
        }
        else
        {
            dataBus.mouseMoving = false;
        }
    }
    #endregion
    #region 瞄准系统
    public void Input_Aim(InputAction.CallbackContext context)//手动选择触发模式（Trigger/Press)
    {

        if (context.started)
        {
            dataBus.aimHeld = true;
            dataBus.aimPressedThisFrame = true;
        }
        else if (context.canceled)
        {
            dataBus.aimHeld = false;
            dataBus.aimReleasedThisFrame = true;
        }



    }
    public void SyncVirtualMouseToPosition(Vector2 screenPosition)
    {
        GamepadScreenPosition = screenPosition;
        ActiveScreenPosition = screenPosition;
        virtualMousePosition = screenPosition;  // 更新虚拟鼠标位置
    }
    #endregion
    #region 装弹/卸弹系统（包含短按、长按判断，使用输入缓冲）
    //短按装弹/卸弹，长按切换
    public void Input_EquipOrReload(InputAction.CallbackContext context)
    {
        if (dataBus == null)
            return;

        if (context.started)
        {
            // 第一次按下开始，重置长按标记
            equipOrReloadLongPerformed = false;
        }

        if (context.performed)
        {
            if (context.interaction is HoldInteraction)
            {
                // 一次按住期间，只触发一次长按
                if (!equipOrReloadLongPerformed)
                {
                    equipOrReloadLongPerformed = true;
                    dataBus.weaponToggleLongPressedThisFrame = true;

                    Debug.Log("[Input] Weapon Toggle Long Press Triggered Once");
                }
            }
        }

        if (context.canceled)
        {
            // 如果释放时没有触发长按，说明是短按
            if (!equipOrReloadLongPerformed)
            {
                dataBus.reloadPressedThisFrame = true;
            }

            // 一次按键结束，重置长按标记关闭
            equipOrReloadLongPerformed = false;
        }
    }

    #endregion

    public void Input_Jump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.jumpPressed = true;
            dataBus.jumpHeld = true;
            dataBus.jumpPressedThisFrame = true;
            // 不再以 isGrounded 拦截输入，由 JumpStateMachine 判断 Coyote Time / Buffer
            CharacterStateRequest request = new CharacterStateRequest(CharacterStateType.Jump_PreStart, true);
            characterRoot.stateArbitrator.SubmitRequest(request);
        }
        else if (context.canceled)
        {
            dataBus.jumpHeld = false;
            dataBus.jumpReleasedThisFrame = true;
            dataBus.jumpPressed = false;
        }
    }




    #region 攻击系统
    public void HunterWeapon_Attack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.hunterAttackPressedThisFrame = true;
            characterRoot.characterAttackController.SetHunterAttackHeld(true);
        }
        else if (context.canceled)
        {
            characterRoot.characterAttackController.SetHunterAttackHeld(false);
        }
    }

    public void HoundWeapon_Attack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            dataBus.houndAttackPressedThisFrame = true;
            characterRoot.characterAttackController.SetHoundAttackHeld(true);
        }
        else if (context.canceled)
        {
            characterRoot.characterAttackController.SetHoundAttackHeld(false);
        }
    }


    #endregion


    #region 武器切换系统
    private void Input_WeaponSlot1(
    InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot1);
    }

    private void Input_WeaponSlot2(InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot2);
    }

    private void Input_WeaponSlot3(InputAction.CallbackContext context)
    {
        RequestWeaponSlot(WeaponSlotId.Slot3);
    }

    private void Input_WeaponCycleNext(InputAction.CallbackContext context)
    {
        if (dataBus == null)
            return;
        if (!dataBus.isAlive)
            return;
        dataBus.RequestWeaponSlotCycleNext();
        Debug.Log( "[Input] Request weapon cycle next.",this);
    }

    private void Input_Pickup(InputAction.CallbackContext context)
    {
        if (characterRoot == null) return;
        var pickupCtrl = characterRoot.characterPickupController;
        if (pickupCtrl != null)
            pickupCtrl.OnPickupPressed();
    }

    private void Input_PackageToggle(InputAction.CallbackContext context)
    {
        if (!context.started) return; // 只在按下瞬间触发一次，避免 started/performed 双触发
        if (characterRoot == null) return;
        var uiRouter = characterRoot.characterUIInputRouter;
        if (uiRouter != null)
            uiRouter.TogglePackageUI();
    }

    private void RequestWeaponSlot(WeaponSlotId slotId)
    {
        if (dataBus == null)
            return;
        if (!dataBus.isAlive)
            return;
        dataBus.RequestWeaponSlotSwitch(slotId);
        Debug.Log( $"[Input] Request weapon slot: {slotId}",this);
    }
    #endregion





}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/InventorySlotData.cs`

```cs
using System;

/// <summary>
/// A single slot in any IItemContainer. Holds at most one ItemInstance.
/// </summary>
[Serializable]
public class InventorySlotData
{
    public int slotIndex;
    public ItemInstance item;

    public bool IsEmpty => item == null || item.definition == null;

    public InventorySlotData(int index)
    {
        slotIndex = index;
        item = null;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemCategory.cs`

```cs
/// <summary>
/// Item category for inventory sorting and weapon-slot filtering.
/// </summary>
public enum ItemCategory
{
    Weapon,
    Consumable,
    Material,
    Quest,
    Misc
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/Data/ItemDefinitionSO.cs`

```cs
using UnityEngine;

/// <summary>
/// Static definition of "what an item is". Subclassed per category.
/// SO can be referenced by unlimited ItemInstances, but the SO itself is not a specific item.
/// </summary>
public abstract class ItemDefinitionSO : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private ItemCategory category;
    [SerializeField] private Sprite inventoryIcon;
    [SerializeField] private bool stackable = false;
    [SerializeField] private int maxStack = 1;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public ItemCategory Category => category;
    public Sprite InventoryIcon => inventoryIcon;
    public bool Stackable => stackable;
    public int MaxStack => Mathf.Max(1, maxStack);
}
```
