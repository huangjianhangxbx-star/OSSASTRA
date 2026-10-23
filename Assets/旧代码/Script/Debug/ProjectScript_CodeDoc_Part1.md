# ProjectScript Code Documentation - Part 1

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Audio/AudioArbitrator.cs` | 207 | - | class AudioArbitrator | 0 |
| `Assets/Script/Audio/AudioEventSO.cs` | 49 | - | class AudioEventSO | 0 |
| `Assets/Script/Audio/AudioRequest.cs` | 13 | - | struct AudioRequest | 0 |
| `Assets/Script/Audio/BaseHuntAudioState.cs` | 27 | - | class BaseHuntAudioState | 0 |
| `Assets/Script/Audio/GameAudioRoot.cs` | 91 | - | class GameAudioRoot | 0 |
| `Assets/Script/Audio/HuntAudioCombatState.cs` | 108 | - | class HuntAudioCombatState | 0 |
| `Assets/Script/Audio/HuntAudioStateMachine.cs` | 95 | - | class HuntAudioStateMachine | 0 |
| `Assets/Script/Audio/IAudioStateMachine.cs` | 13 | - | interface IAudioStateMachine | 0 |
| `Assets/Script/Audio/WeaponAudio/WeaponAudioProfileSO.cs` | 29 | - | class WeaponAudioProfileSO | 0 |
| `Assets/Script/Character/Core/BasicFunction/Aim/AimIKTargetSafetyClamp.cs` | 135 | - | struct AimIKSafetyConfig, struct AimIKClampResult, class AimIKTargetSafetyClamp | 0 |
| `Assets/Script/Character/Core/BasicFunction/Attack/CharacterAttackController.cs` | 143 | - | class CharacterAttackController, enum AttackInputChannel | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Fall.cs` | 78 | - | class Jump_Fall | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Land.cs` | 61 | - | class Jump_Land | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Peak.cs` | 18 | - | class Jump_Peak | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_PreStart.cs` | 18 | - | class Jump_PreStart | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Rising.cs` | 94 | - | class Jump_Rising | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Start.cs` | 38 | - | class Jump_Start | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/JumpConfigSO.cs` | 102 | - | class JumpConfigSO | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/JumpGroundProbe.cs` | 91 | - | class JumpGroundProbe | 0 |
| `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateBase.cs` | 89 | - | class JumpStateBase | 0 |

## Public API

### `Assets/Script/Audio/AudioArbitrator.cs`
- `public class AudioArbitrator : MonoBehaviour, IGameSubsystem
{`
- `public void Initialize(GameFlowRoot root)
    {`
- `public bool SubmitRequest(AudioRequest request)
    {`
### `Assets/Script/Audio/AudioEventSO.cs`
- `public class AudioEventSO : ScriptableObject
{`
- `public AudioMixerGroup OutputGroup => outputGroup;`
- `public float SpatialBlend => spatialBlend;`
- `public float MinDistance => minDistance;`
- `public float MaxDistance => maxDistance;`
- `public bool TryGetClip(out AudioClip clip)
    {`
- `public float GetVolume()
    {`
- `public float GetPitch()
    {`
### `Assets/Script/Audio/AudioRequest.cs`
- `public struct AudioRequest
{`
- `public AudioStateType audioStateType;`
- `public object Data;`
- `public bool IsUrgent;`
- `public AudioRequest(AudioStateType audioStateType, object data = null, bool isUrgent = false)
    {`
### `Assets/Script/Audio/BaseHuntAudioState.cs`
- `public abstract class BaseHuntAudioState
{`
- `protected HuntAudioStateMachine stateMachine;`
- `protected GameFlowRoot gameFlowRoot;`
- `protected GameAudioRoot audioRoot;`
- `protected GameEventHub eventHub;`
- `public abstract string StateName {`
- `public virtual void Initialize(
        HuntAudioStateMachine stateMachine,
        GameFlowRoot gameFlowRoot,
        GameAudioRoot audioRoot,
        GameEventHub eventHub)
    {`
- `public abstract void Enter();`
- `public virtual void Exit() {`
- `public virtual void Update(float deltaTime) {`
- `public abstract bool CanTransitionTo(BaseHuntAudioState newState);`
### `Assets/Script/Audio/GameAudioRoot.cs`
- `public class GameAudioRoot : MonoBehaviour, IGameSubsystem
{`
- `public void Initialize(GameFlowRoot root)
    {`
- `public void PlaySfx(AudioEventSO audioEvent, Vector3 position)
    {`
### `Assets/Script/Audio/HuntAudioCombatState.cs`
- `public class HuntAudioCombatState : BaseHuntAudioState
{`
- `public override string StateName => "HuntAudioCombatState";`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override bool CanTransitionTo(BaseHuntAudioState newState)
    {`
### `Assets/Script/Audio/HuntAudioStateMachine.cs`
- `public class HuntAudioStateMachine : IAudioStateMachine
{`
- `public string AudioStateMachineName => "HuntAudioStateMachine";`
- `public string CurrentStateName => currentState?.StateName ?? "None";`
- `public int Priority => 20;`
- `public bool IsActive => isRunning;`
- `public void Initialize(GameFlowRoot root, GameAudioRoot audioRoot, GameEventHub eventHub)
    {`
- `public bool RequestState(object stateData)
    {`
- `public bool TransitionTo<T>() where T : BaseHuntAudioState
    {`
- `public void Enter()
    {`
- `public void Exit()
    {`
- `public void Update(float deltaTime)
    {`
### `Assets/Script/Audio/IAudioStateMachine.cs`
- `public interface IAudioStateMachine
{`
### `Assets/Script/Audio/WeaponAudio/WeaponAudioProfileSO.cs`
- `public class WeaponAudioProfileSO : ScriptableObject
{`
- `public AudioEventSO fire;`
- `public AudioEventSO fireMechanical;`
- `public AudioEventSO fireTail;`
- `public AudioEventSO empty;`
- `public AudioEventSO reloadStart;`
- `public AudioEventSO reloadInsert;`
- `public AudioEventSO reloadCharge;`
- `public AudioEventSO reloadEnd;`
- `public AudioEventSO defaultImpact;`
- `public AudioEventSO fleshImpact;`
- `public AudioEventSO metalImpact;`
- `public AudioEventSO concreteImpact;`
- `public AudioEventSO woodImpact;`
- `public AudioEventSO glassImpact;`
### `Assets/Script/Character/Core/BasicFunction/Aim/AimIKTargetSafetyClamp.cs`
- `public struct AimIKSafetyConfig
{`
- `public bool enableSafetyClamp;`
- `public float maxAimAngle;`
- `public float minimumForwardDistance;`
- `public bool preserveTargetDistance;`
- `public bool enableClampDebug;`
- `public static AimIKSafetyConfig Default => new AimIKSafetyConfig
    {`
- `public struct AimIKClampResult
{`
- `public Vector2 rawWorldTarget;`
- `public Vector2 clampedWorldTarget;`
- `public bool wasClamped;`
- `public float rawAngle;`
- `public float clampedAngle;`
- `public static class AimIKTargetSafetyClamp
{`
- `public static AimIKClampResult Clamp(
        Vector2 rawWorldTarget,
        Vector2 aimOriginWorld,
        int stableFacingSign,
        AimIKSafetyConfig config)
    {`
### `Assets/Script/Character/Core/BasicFunction/Attack/CharacterAttackController.cs`
- `public class CharacterAttackController : MonoBehaviour, ICharacterSubsystem
{`
- `public enum AttackInputChannel
    {`
- `public bool HunterAttackHeld => hunterAttackHeld;`
- `public bool HoundAttackHeld => houndAttackHeld;`
- `public bool AnyAttackHeld => hunterAttackHeld || houndAttackHeld;`
- `public AttackInputChannel LastInputChannel {`
- `public void Initialize(CharacterRoot root)
    {`
- `public void SetHunterAttackHeld(bool held)
    {`
- `public void SetHoundAttackHeld(bool held)
    {`
- `public void SetAttackHeld(AttackInputChannel channel, bool held)
    {`
- `public void ReleaseAll()
    {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Fall.cs`
- `public class Jump_Fall : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_Fall;`
- `public override string StateName => "Jump_Falling";`
- `public override void Enter() {`
- `public override void Exit() {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime)
    {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Land.cs`
- `public class Jump_Land : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_Land;`
- `public override string StateName => "Jump_Landing";`
- `public override void Enter()
    {`
- `public override void Exit() {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime) {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Peak.cs`
- `public class Jump_Peak : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_Peak;`
- `public override string StateName => "Jump_Peak (Deprecated)";`
- `public override void Enter() {`
- `public override void Exit() {`
- `public override void Update(float deltaTime) {`
- `public override void FixedUpdate(float fixedDeltaTime) {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_PreStart.cs`
- `public class Jump_PreStart : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_PreStart;`
- `public override string StateName => "Jump_PreStart (Deprecated)";`
- `public override void Enter() {`
- `public override void Exit() {`
- `public override void Update(float deltaTime) {`
- `public override void FixedUpdate(float fixedDeltaTime) {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Rising.cs`
- `public class Jump_Rising : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_Rising;`
- `public override string StateName => "Jump_Rising";`
- `public override void Enter()
    {`
- `public override void Exit() {`
- `public override void Update(float deltaTime)
    {`
- `public override void FixedUpdate(float fixedDeltaTime)
    {`
### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Start.cs`
- `public class Jump_Start : JumpStateBase
{`
- `public override EState StateKey => EState.Jump_Start;`
- `public override string StateName => "Jump_Start";`
- `public override void Enter()
    {`
- `public override void Exit() {`
- `public override void Update(float deltaTime) {`
- `public override void FixedUpdate(float fixedDeltaTime) {`
### `Assets/Script/Character/Core/BasicFunction/Jump/JumpConfigSO.cs`
- `public class JumpConfigSO : ScriptableObject
{`
- `public float jumpSpeed = 9f;`
- `public float baseGravityAcceleration = 30f;`
- `public float riseGravityMultiplier = 1.0f;`
- `public float fallGravityMultiplier = 1.8f;`
- `public float apexGravityMultiplier = 0.6f;`
- `public float apexSpeedThreshold = 2f;`
- `public float jumpCutMultiplier = 0.5f;`
- `public float maxFallSpeed = 20f;`
- `public float coyoteTime = 0.1f;`
- `public float jumpBufferTime = 0.1f;`
- `public float airAcceleration = 20f;`
- `public float airDeceleration = 10f;`
- `public float maxAirMoveSpeed = 5f;`
- `public float groundProbeDistance = 0.15f;`
- `public float ceilingProbeDistance = 0.1f;`
- `public float collisionSkinWidth = 0.02f;`
- `public float landingDuration = 0.03f;`
- `public float heavyLandingSpeedThreshold = 15f;`
- `public bool enableDebugLogs = true;`
### `Assets/Script/Character/Core/BasicFunction/Jump/JumpGroundProbe.cs`
- `public class JumpGroundProbe
{`
- `public bool IsGrounded => _isGrounded;`
- `public bool IsHittingCeiling => _isHittingCeiling;`
- `public Vector2 GroundNormal => _groundNormal;`
- `public float GroundDistance => _groundDistance;`
- `public void Initialize(Collider2D collider, LayerMask groundMask, JumpConfigSO config)
    {`
- `public void Probe()
    {`
- `public void DrawGizmos()
    {`
### `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateBase.cs`
- `public abstract class JumpStateBase
{`
- `public abstract EState StateKey {`
- `public abstract string StateName {`
- `protected JumpStateMachine stateMachine;`
- `protected CharacterDataBus dataBus;`
- `protected Rigidbody2D rigidbody;`
- `protected Vector2 gravityDown = Vector2.down;`
- `protected Vector2 gravityUp = Vector2.up;`
- `protected Vector2 lateralAxis = Vector2.right;`
- `public virtual void Initialize(JumpStateMachine machine, CharacterDataBus bus, Rigidbody2D rb)
    {`
- `public void SetGravityDirection(Vector2 down)
    {`
- `protected float UpwardSpeed => Vector2.Dot(rigidbody.velocity, gravityUp);`
- `protected float LateralSpeed => Vector2.Dot(rigidbody.velocity, lateralAxis);`
- `protected Vector2 ComposeVelocity(float upwardSpeed, float lateralSpeed)
    {`
- `protected float ComputeGravity(float currentUpwardSpeed, float multiplier, float maxFallSpeed, float fixedDeltaTime)
    {`
- `protected float ComputeAirControl(float input, float currentLateral, float fixedDeltaTime)
    {`
- `public virtual void Enter() {`
- `public virtual void Exit() {`
- `public virtual void Update(float deltaTime) {`
- `public virtual void FixedUpdate(float fixedDeltaTime) {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Audio/AudioArbitrator.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

public class AudioArbitrator : MonoBehaviour, IGameSubsystem
{
    private Dictionary<AudioStateType, IAudioStateMachine> stateMachinesDictionary;
    private HashSet<IAudioStateMachine> uniqueStateMachines;
    private Queue<AudioRequest> requestQueue;

    private IAudioStateMachine activeStateMachine;

    private HuntAudioStateMachine huntAudioSM;

    private GameFlowRoot gameFlowRoot;
    private GameEventHub eventHub;
    private GameAudioRoot audioRoot;

    private bool isInitialized;

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        gameFlowRoot = root;
        eventHub = root.EventHub;
        audioRoot = root.audioRoot;

        stateMachinesDictionary = new Dictionary<AudioStateType, IAudioStateMachine>();
        uniqueStateMachines = new HashSet<IAudioStateMachine>();
        requestQueue = new Queue<AudioRequest>();

        InitializeStateMachines();

        SetInitialState();

        eventHub.Hunt.Subscribe<HuntFlowStateChangedEvent>(OnHuntFlowStateChanged);

        isInitialized = true;
    }
    private void SetInitialState()
    {
        activeStateMachine = huntAudioSM;
        activeStateMachine.Enter();

        Debug.Log($"[AudioArbitrator] 初始音频状态机: {activeStateMachine.AudioStateMachineName}");
    }
    private void Update()
    {
        if (!isInitialized)
            return;

        float deltaTime = Time.deltaTime;

        ProcessRequestQueue();

        IAudioStateMachine newActiveMachine = DetermineActiveStateMachine();

        if (newActiveMachine != null && newActiveMachine != activeStateMachine)
        {
            if (activeStateMachine == null ||
                newActiveMachine.Priority > activeStateMachine.Priority ||
                !activeStateMachine.IsActive)
            {
                PerformStateTransition(activeStateMachine, newActiveMachine);
            }
        }

        if (activeStateMachine != null && activeStateMachine.IsActive)
        {
            activeStateMachine.Update(deltaTime);
        }
    }

    private void OnDestroy()
    {
        if (eventHub != null)
        {
            eventHub.Hunt.Unsubscribe<HuntFlowStateChangedEvent>(OnHuntFlowStateChanged);
        }
    }






    private void InitializeStateMachines()
    {
        huntAudioSM = new HuntAudioStateMachine();

        RegisterStateMachine(AudioStateType.HuntPrepare, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntLoadMap, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntCountdown, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntCombat, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntLevelUp, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntResult, huntAudioSM);
    }

    private void RegisterStateMachine(AudioStateType type, IAudioStateMachine machine)
    {
        if (machine == null)
        {
            Debug.LogError($"[AudioArbitrator] 尝试注册空音频状态机: {type}");
            return;
        }

        if (!uniqueStateMachines.Contains(machine))
        {
            machine.Initialize(gameFlowRoot, audioRoot, eventHub);
            uniqueStateMachines.Add(machine);
        }

        stateMachinesDictionary[type] = machine;
    }

    private void OnHuntFlowStateChanged(HuntFlowStateChangedEvent e)
    {
        AudioStateType audioStateType = ConvertHuntFlowToAudioState(e.to);

        if (audioStateType == AudioStateType.None)
            return;

        SubmitRequest(new AudioRequest(audioStateType, audioStateType, true));
    }

    private AudioStateType ConvertHuntFlowToAudioState(GameFlowStateType flowStateType)
    {
        switch (flowStateType)
        {
            case GameFlowStateType.HuntPrepare:
                return AudioStateType.HuntPrepare;

            case GameFlowStateType.HuntLoadMap:
                return AudioStateType.HuntLoadMap;

            case GameFlowStateType.HuntCountdown:
                return AudioStateType.HuntCountdown;

            case GameFlowStateType.HuntCombat:
                return AudioStateType.HuntCombat;

            case GameFlowStateType.HuntLevelUp:
                return AudioStateType.HuntLevelUp;

            case GameFlowStateType.HuntResult:
                return AudioStateType.HuntResult;

            default:
                return AudioStateType.None;
        }
    }

    public bool SubmitRequest(AudioRequest request)
    {
        if (request.IsUrgent)
            return ProcessRequestImmediately(request);

        requestQueue.Enqueue(request);
        return true;
    }

    private void ProcessRequestQueue()
    {
        while (requestQueue.Count > 0)
        {
            AudioRequest request = requestQueue.Dequeue();
            ProcessRequestImmediately(request);
        }
    }

    private bool ProcessRequestImmediately(AudioRequest request)
    {
        if (!stateMachinesDictionary.TryGetValue(request.audioStateType, out IAudioStateMachine targetMachine))
        {
            Debug.LogWarning($"[AudioArbitrator] 没有找到处理 {request.audioStateType} 的音频状态机");
            return false;
        }

        return targetMachine.RequestState(request.Data);
    }

    private IAudioStateMachine DetermineActiveStateMachine()
    {
        IAudioStateMachine highestPriorityMachine = null;
        int maxPriority = int.MinValue;

        foreach (IAudioStateMachine machine in uniqueStateMachines)
        {
            if (machine.IsActive && machine.Priority > maxPriority)
            {
                maxPriority = machine.Priority;
                highestPriorityMachine = machine;
            }
        }

        return highestPriorityMachine;
    }

    private void PerformStateTransition(IAudioStateMachine from, IAudioStateMachine to)
    {
        from?.Exit();

        activeStateMachine = to;
        activeStateMachine.Enter();
    }
}
```

### `Assets/Script/Audio/AudioEventSO.cs`

```cs
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "AE_NewAudioEvent",menuName = "ScriptableObject/音频/音频Event"
)]
public class AudioEventSO : ScriptableObject
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float volumeRandomRange = 0.05f;

    [Header("Pitch")]
    [SerializeField] private float pitch = 1f;
    [SerializeField] private float pitchRandomRange = 0.04f;

    [Header("Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;

    public AudioMixerGroup OutputGroup => outputGroup;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;

    public bool TryGetClip(out AudioClip clip)
    {
        clip = null;

        if (clips == null || clips.Length == 0)
            return false;

        clip = clips[Random.Range(0, clips.Length)];
        return clip != null;
    }

    public float GetVolume()
    {
        return Mathf.Clamp01(volume + Random.Range(-volumeRandomRange, volumeRandomRange));
    }

    public float GetPitch()
    {
        return Mathf.Max(0.01f, pitch + Random.Range(-pitchRandomRange, pitchRandomRange));
    }
}
```

### `Assets/Script/Audio/AudioRequest.cs`

```cs
public struct AudioRequest
{
    public AudioStateType audioStateType;
    public object Data;
    public bool IsUrgent;

    public AudioRequest(AudioStateType audioStateType, object data = null, bool isUrgent = false)
    {
        this.audioStateType = audioStateType;
        Data = data;
        IsUrgent = isUrgent;
    }
}
```

### `Assets/Script/Audio/BaseHuntAudioState.cs`

```cs
public abstract class BaseHuntAudioState
{
    protected HuntAudioStateMachine stateMachine;
    protected GameFlowRoot gameFlowRoot;
    protected GameAudioRoot audioRoot;
    protected GameEventHub eventHub;

    public abstract string StateName { get; }

    public virtual void Initialize(
        HuntAudioStateMachine stateMachine,
        GameFlowRoot gameFlowRoot,
        GameAudioRoot audioRoot,
        GameEventHub eventHub)
    {
        this.stateMachine = stateMachine;
        this.gameFlowRoot = gameFlowRoot;
        this.audioRoot = audioRoot;
        this.eventHub = eventHub;
    }

    public abstract void Enter();
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }

    public abstract bool CanTransitionTo(BaseHuntAudioState newState);
}
```

### `Assets/Script/Audio/GameAudioRoot.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
//1.初始化 AudioMixer
//2. 管理 AudioSource Pool
//3. 播放 2D SFX
//4. 播放 3D/位置 SFX
//5. 播放/切换/淡入淡出 BGM
//6. 控制 Master/Music/SFX/UI/Weapon 音量
//7. 暂停/恢复音频
//8. 根据 GameFlow 切换大的音频场景

public class GameAudioRoot : MonoBehaviour, IGameSubsystem
{
    [SerializeField] private int initialPoolSize = 32;

    private readonly Queue<AudioSource> pool = new Queue<AudioSource>();
    private bool isInitialized;

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            pool.Enqueue(CreateSource());
        }

        isInitialized = true;
        Debug.Log("[GameAudioRoot] 初始化完成", this);
    }

    public void PlaySfx(AudioEventSO audioEvent, Vector3 position)
    {
        if (audioEvent == null)
            return;

        if (!audioEvent.TryGetClip(out AudioClip clip))
            return;

        AudioSource source = GetSource();

        source.transform.position = position;
        source.clip = clip;
        source.outputAudioMixerGroup = audioEvent.OutputGroup;
        source.volume = audioEvent.GetVolume();
        source.pitch = audioEvent.GetPitch();
        source.spatialBlend = audioEvent.SpatialBlend;
        source.minDistance = audioEvent.MinDistance;
        source.maxDistance = audioEvent.MaxDistance;
        source.loop = false;

        source.Play();

        float duration = clip.length / Mathf.Abs(source.pitch);
        StartCoroutine(ReturnAfterFinished(source, duration));
    }

    private AudioSource GetSource()
    {
        if (pool.Count > 0)
            return pool.Dequeue();

        return CreateSource();
    }

    private AudioSource CreateSource()
    {
        GameObject obj = new GameObject("PooledAudioSource");
        obj.transform.SetParent(transform);

        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;

        return source;
    }

    private IEnumerator ReturnAfterFinished(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.transform.SetParent(transform);

        pool.Enqueue(source);
    }
}
```

### `Assets/Script/Audio/HuntAudioCombatState.cs`

```cs
using UnityEngine;

public class HuntAudioCombatState : BaseHuntAudioState
{
    public override string StateName => "HuntAudioCombatState";

    public override void Enter()
    {
        Debug.Log("[HuntAudioCombat] Enter");

        eventHub.Hunt.Subscribe<WeaponFiredEvent>(OnWeaponFired);
        eventHub.Hunt.Subscribe<BulletImpactEvent>(OnBulletImpact);
        eventHub.Hunt.Subscribe<WeaponReloadEvent>(OnWeaponReload);

        // audioRoot.PlayMusic(... combat music ...)
    }

    public override void Exit()
    {
        eventHub.Hunt.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
        eventHub.Hunt.Unsubscribe<BulletImpactEvent>(OnBulletImpact);
        eventHub.Hunt.Unsubscribe<WeaponReloadEvent>(OnWeaponReload);
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        if (e.audioProfile == null)
            return;

        audioRoot.PlaySfx(e.audioProfile.fire, e.position);

        if (e.audioProfile.fireMechanical != null)
            audioRoot.PlaySfx(e.audioProfile.fireMechanical, e.position);
    }

    private void OnBulletImpact(BulletImpactEvent e)
    {
        if (e.audioProfile == null)
            return;

        AudioEventSO cue = SelectImpactCue(e);

        if (cue != null)
            audioRoot.PlaySfx(cue, e.position);
    }

    private void OnWeaponReload(WeaponReloadEvent e)
    {
        if (e.audioProfile == null)
            return;

        AudioEventSO cue = null;

        switch (e.phase)
        {
            case WeaponReloadPhase.Start:
                cue = e.audioProfile.reloadStart;
                break;

            case WeaponReloadPhase.InsertMagazine:
                cue = e.audioProfile.reloadInsert;
                break;

            case WeaponReloadPhase.Charge:
                cue = e.audioProfile.reloadCharge;
                break;

            case WeaponReloadPhase.End:
                cue = e.audioProfile.reloadEnd;
                break;
        }

        if (cue != null)
            audioRoot.PlaySfx(cue, e.position);
    }

    private AudioEventSO SelectImpactCue(BulletImpactEvent e)
    {
        if (e.hitEnemy && e.audioProfile.fleshImpact != null)
            return e.audioProfile.fleshImpact;

        switch (e.surfaceType)
        {
            case SurfaceType.Flesh:
                return e.audioProfile.fleshImpact;

            case SurfaceType.Metal:
                return e.audioProfile.metalImpact;

            case SurfaceType.Concrete:
                return e.audioProfile.concreteImpact;

            case SurfaceType.Wood:
                return e.audioProfile.woodImpact;

            case SurfaceType.Glass:
                return e.audioProfile.glassImpact;

            default:
                return e.audioProfile.defaultImpact;
        }
    }

    public override bool CanTransitionTo(BaseHuntAudioState newState)
    {
        return true;
    }
}
```

### `Assets/Script/Audio/HuntAudioStateMachine.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;


public class HuntAudioStateMachine : IAudioStateMachine
{
    public string AudioStateMachineName => "HuntAudioStateMachine";
    public string CurrentStateName => currentState?.StateName ?? "None";

    public int Priority => 20;
    public bool IsActive => isRunning;

    private bool isRunning;

    private Dictionary<Type, BaseHuntAudioState> states;
    private BaseHuntAudioState currentState;

    private GameFlowRoot gameFlowRoot;
    private GameAudioRoot audioRoot;
    private GameEventHub eventHub;

    public void Initialize(GameFlowRoot root, GameAudioRoot audioRoot, GameEventHub eventHub)
    {
        this.gameFlowRoot = root;
        this.audioRoot = audioRoot;
        this.eventHub = eventHub;

        InitializeStates();
    }

    private void InitializeStates()
    {
        states = new Dictionary<Type, BaseHuntAudioState>
        {

            [typeof(HuntAudioCombatState)] = new HuntAudioCombatState(),
        };

        foreach (BaseHuntAudioState state in states.Values)
        {
            state.Initialize(this, gameFlowRoot, audioRoot, eventHub);
        }
    }

    public bool RequestState(object stateData)
    {
        if (stateData is AudioStateType type)
        {
            switch (type)
            {
                case AudioStateType.HuntCombat:
                    return TransitionTo<HuntAudioCombatState>();
            }
        }

        return false;
    }

    public bool TransitionTo<T>() where T : BaseHuntAudioState
    {
        if (!states.TryGetValue(typeof(T), out BaseHuntAudioState newState))
            return false;

        if (currentState != null && !currentState.CanTransitionTo(newState))
            return false;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();

        return true;
    }

    public void Enter()
    {
        isRunning = true;

        if (currentState == null)
            TransitionTo<HuntAudioCombatState>();
        else
            currentState.Enter();
    }

    public void Exit()
    {
        currentState?.Exit();
        isRunning = false;
    }

    public void Update(float deltaTime)
    {
        currentState?.Update(deltaTime);
    }
}
```

### `Assets/Script/Audio/IAudioStateMachine.cs`

```cs
public interface IAudioStateMachine
{
    string AudioStateMachineName { get; }
    string CurrentStateName { get; }
    int Priority { get; }
    bool IsActive { get; }

    void Initialize(GameFlowRoot root, GameAudioRoot audioRoot, GameEventHub eventHub);
    bool RequestState(object stateData);
    void Enter();
    void Exit();
    void Update(float deltaTime);
}
```

### `Assets/Script/Audio/WeaponAudio/WeaponAudioProfileSO.cs`

```cs
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAudioProfile",menuName = "ScriptableObject/武器/武器音频"
)]
public class WeaponAudioProfileSO : ScriptableObject
{
    [Header("Fire")]
    public AudioEventSO fire;
    public AudioEventSO fireMechanical;
    public AudioEventSO fireTail;

    [Header("Ammo")]
    public AudioEventSO empty;

    [Header("Reload")]
    public AudioEventSO reloadStart;
    public AudioEventSO reloadInsert;
    public AudioEventSO reloadCharge;
    public AudioEventSO reloadEnd;


    [Header("Impact")]
    public AudioEventSO defaultImpact;
    public AudioEventSO fleshImpact;
    public AudioEventSO metalImpact;
    public AudioEventSO concreteImpact;
    public AudioEventSO woodImpact;
    public AudioEventSO glassImpact;
}
```

### `Assets/Script/Character/Core/BasicFunction/Aim/AimIKTargetSafetyClamp.cs`

```cs
using UnityEngine;

/// <summary>
/// IK 安全钳制配置。
/// </summary>
[System.Serializable]
public struct AimIKSafetyConfig
{
    [Tooltip("是否启用安全钳制")]
    public bool enableSafetyClamp;

    [Tooltip("最大瞄准角度（度），不允许达到或超过90")]
    [Range(45f, 89f)]
    public float maxAimAngle;

    [Tooltip("最小目标距离")]
    public float minimumForwardDistance;

    [Tooltip("钳制后是否保留原始目标距离")]
    public bool preserveTargetDistance;

    [Tooltip("是否绘制调试Gizmo")]
    public bool enableClampDebug;

    public static AimIKSafetyConfig Default => new AimIKSafetyConfig
    {
        enableSafetyClamp = true,
        maxAimAngle = 82f,
        minimumForwardDistance = 0.05f,
        preserveTargetDistance = true,
        enableClampDebug = false
    };
}

/// <summary>
/// IK 钳制结果。
/// </summary>
public struct AimIKClampResult
{
    public Vector2 rawWorldTarget;
    public Vector2 clampedWorldTarget;
    public bool wasClamped;
    public float rawAngle;
    public float clampedAngle;
}

/// <summary>
/// 纯 C# 辅助类，在 AimIKController 最终写入 Spine IK Target 前进行前方扇区限制。
/// 防止 IK Target 进入角色视觉朝向的背面。
/// 不独立 Tick，不写 DataBus，不控制角色朝向。
/// </summary>
public static class AimIKTargetSafetyClamp
{
    /// <summary>
    /// 将原始世界目标钳制到角色前方扇区内。
    /// 使用方案B：在世界空间中计算，基于角色稳定朝向判定前方半平面。
    /// </summary>
    public static AimIKClampResult Clamp(
        Vector2 rawWorldTarget,
        Vector2 aimOriginWorld,
        int stableFacingSign,
        AimIKSafetyConfig config)
    {
        AimIKClampResult result = new AimIKClampResult
        {
            rawWorldTarget = rawWorldTarget,
            clampedWorldTarget = rawWorldTarget,
            wasClamped = false,
            rawAngle = 0f,
            clampedAngle = 0f
        };

        if (!config.enableSafetyClamp)
            return result;

        // 角色前方方向（世界空间）
        // stableFacingSign > 0 = 右, 前方 = Vector2.right
        // stableFacingSign < 0 = 左, 前方 = Vector2.left
        Vector2 forward = stableFacingSign > 0 ? Vector2.right : Vector2.left;

        Vector2 offset = rawWorldTarget - aimOriginWorld;
        float distance = offset.magnitude;

        if (distance < 0.001f)
        {
            // 目标就在原点，放到最小前方距离
            result.clampedWorldTarget = aimOriginWorld + forward * config.minimumForwardDistance;
            result.wasClamped = true;
            return result;
        }

        // 计算原始角度（相对于前方）
        float forwardDot = Vector2.Dot(offset.normalized, forward);
        float rawAngle = Mathf.Acos(Mathf.Clamp(forwardDot, -1f, 1f)) * Mathf.Rad2Deg;
        result.rawAngle = rawAngle;

        if (rawAngle <= config.maxAimAngle)
        {
            // 在允许范围内，不钳制
            result.clampedAngle = rawAngle;
            return result;
        }

        // 需要钳制：将目标投影到扇区边界
        // 计算偏移在前方轴和侧向轴上的分量
        float forwardComponent = Vector2.Dot(offset, forward);
        Vector2 sideAxis = new Vector2(-forward.y, forward.x); // 前方的垂直方向
        float sideComponent = Vector2.Dot(offset, sideAxis);

        // 如果前方分量为负（目标在背后），强制为最小前方距离
        if (forwardComponent < config.minimumForwardDistance)
            forwardComponent = config.minimumForwardDistance;

        // 钳制角度
        float clampedAngleRad = config.maxAimAngle * Mathf.Deg2Rad;

        // 使用角度钳制重建偏移
        float preserveDistance = config.preserveTargetDistance
            ? Mathf.Max(distance, config.minimumForwardDistance)
            : Mathf.Max(forwardComponent / Mathf.Cos(clampedAngleRad), config.minimumForwardDistance);

        // 方向：保持侧向符号，限制角度
        float sideSign = sideComponent >= 0f ? 1f : -1f;

        Vector2 clampedOffset = forward * Mathf.Cos(clampedAngleRad) * preserveDistance
                              + sideAxis * Mathf.Sin(clampedAngleRad) * preserveDistance * sideSign;

        result.clampedWorldTarget = aimOriginWorld + clampedOffset;
        result.clampedAngle = config.maxAimAngle;
        result.wasClamped = true;

        return result;
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Attack/CharacterAttackController.cs`

```cs
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAttackController : MonoBehaviour, ICharacterSubsystem
{
    public enum AttackInputChannel
    {
        None,
        Hunter,
        Hound
    }

    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;

    private CharacterDataBus dataBus;
    private bool _isInitialized;

    private bool hunterAttackHeld;
    private bool houndAttackHeld;

    private bool lastHunterAttackHeld;
    private bool lastHoundAttackHeld;

    public bool HunterAttackHeld => hunterAttackHeld;
    public bool HoundAttackHeld => houndAttackHeld;
    public bool AnyAttackHeld => hunterAttackHeld || houndAttackHeld;

    public AttackInputChannel LastInputChannel { get; private set; } = AttackInputChannel.None;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;
        }

        if (dataBus == null)
        {
            Debug.LogError("[CharacterAttackInputController] dataBus == null", this);
            return;
        }

        SyncToDataBus();

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();
    }

    public void SetHunterAttackHeld(bool held)
    {
        hunterAttackHeld = held;

        if (held)
            LastInputChannel = AttackInputChannel.Hunter;
        else
            RefreshLastInputChannelAfterRelease();

        SyncToDataBus();
    }

    public void SetHoundAttackHeld(bool held)
    {
        houndAttackHeld = held;

        if (held)
            LastInputChannel = AttackInputChannel.Hound;
        else
            RefreshLastInputChannelAfterRelease();

        SyncToDataBus();
    }

    public void SetAttackHeld(AttackInputChannel channel, bool held)
    {
        switch (channel)
        {
            case AttackInputChannel.Hunter:
                SetHunterAttackHeld(held);
                break;

            case AttackInputChannel.Hound:
                SetHoundAttackHeld(held);
                break;

            case AttackInputChannel.None:
                if (!held)
                    ReleaseAll();
                break;
        }
    }

    public void ReleaseAll()
    {
        hunterAttackHeld = false;
        houndAttackHeld = false;
        LastInputChannel = AttackInputChannel.None;

        SyncToDataBus();
    }

    private void RefreshLastInputChannelAfterRelease()
    {
        if (houndAttackHeld)
        {
            LastInputChannel = AttackInputChannel.Hound;
            return;
        }

        if (hunterAttackHeld)
        {
            LastInputChannel = AttackInputChannel.Hunter;
            return;
        }

        LastInputChannel = AttackInputChannel.None;
    }

    private void SyncToDataBus()
    {
        if (dataBus == null)
            return;

        dataBus.hunterAttackHeld = hunterAttackHeld;
        dataBus.houndAttackHeld = houndAttackHeld;
    }

    private void OnDisable()
    {
        if (_isInitialized)
            ReleaseAll();
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Fall.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃下落阶段。
/// 每物理帧：先检测地面 → 吸附到命中位置 → 清 Y 速度 → 进入 Land。
/// 否则才计算重力、空中控制并写速度。
/// </summary>
public class Jump_Fall : JumpStateBase
{
    public override EState StateKey => EState.Jump_Fall;
    public override string StateName => "Jump_Falling";

    public override void Enter() { }
    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        if (dataBus.jumpPressedThisFrame)
        {
            dataBus.lastJumpPressedTime = Time.time;
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
            return;

        // 1. 先检测落地条件
        var probe = stateMachine.GroundProbe;

        bool canLand =
            probe != null &&
            probe.IsGrounded &&
            stateMachine.HasLeftGround &&
            UpwardSpeed <= 0.1f;

        if (canLand)
        {
            float landingSpeed = Mathf.Max(0f, -UpwardSpeed);

            if (config.enableDebugLogs)
            {
                Debug.Log(
                    $"[Jump] Falling -> Landing " +
                    $"landingSpeed={landingSpeed:F1}, " +
                    $"groundDistance={probe.GroundDistance:F3}"
                );
            }

            // 吸附到实际地面命中位置
            stateMachine.SnapToGround();

            // 清除 Y 速度
            Vector2 velocity = rigidbody.velocity;
            velocity.y = 0f;
            rigidbody.velocity = velocity;

            stateMachine.TransitionTo(EState.Jump_Land);
            return;
        }

        // 2. 读取当前分量
        float upSpeed = UpwardSpeed;
        float lateralSpeed = LateralSpeed;

        // 3. 计算重力
        upSpeed = ComputeGravity(upSpeed, config.fallGravityMultiplier, config.maxFallSpeed, fixedDeltaTime);

        // 4. 计算空中横向控制
        lateralSpeed = ComputeAirControl(dataBus.moveInput.x, lateralSpeed, fixedDeltaTime);

        // 5. 一次写入最终 velocity
        rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Land.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃落地阶段。
/// - 清除朝下的穿透速度
/// - 轻落地短暂表现后完成跳跃
/// - 若有有效 Jump Buffer 可直接重新起跳
/// </summary>
public class Jump_Land : JumpStateBase
{
    private float _landTimer;

    public override EState StateKey => EState.Jump_Land;
    public override string StateName => "Jump_Landing";

    public override void Enter()
    {
        var config = stateMachine.Config;

        // 清除朝下的穿透速度，保留横向
        if (rigidbody != null)
        {
            Vector2 v = rigidbody.velocity;
            float lateral = Vector2.Dot(v, lateralAxis);
            rigidbody.velocity = lateralAxis * lateral;
        }

        _landTimer = config != null ? config.landingDuration : 0.03f;

        // 重置跳跃消费（落地后可以再次跳跃）
        stateMachine.ResetJumpConsumption();

        // 如果有有效 Jump Buffer，直接重新起跳
        if (stateMachine.HasBufferedJump && dataBus.canJump)
        {
            if (config != null && config.enableDebugLogs)
                Debug.Log("[Jump] Landing -> Startup reason=BufferedLanding");

            stateMachine.ConsumeJump();
            stateMachine.TransitionTo(EState.Jump_Start);
            return;
        }

        if (config != null && config.enableDebugLogs)
            Debug.Log("[Jump] Landing -> Complete");
    }

    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        _landTimer -= deltaTime;
        if (_landTimer <= 0f)
        {
            stateMachine.CompleteJump();
        }
    }

    public override void FixedUpdate(float fixedDeltaTime) { }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Peak.cs`

```cs
using UnityEngine;

/// <summary>
/// [已停用] 原 Jump_Peak，不再作为独立物理状态。
/// 顶点判断改为 Rising 中的 apexGravityMultiplier 逻辑。
/// 保留文件避免 .meta 引用丢失。
/// </summary>
public class Jump_Peak : JumpStateBase
{
    public override EState StateKey => EState.Jump_Peak;
    public override string StateName => "Jump_Peak (Deprecated)";

    public override void Enter() { }
    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_PreStart.cs`

```cs
using UnityEngine;

/// <summary>
/// [已停用] 原 Jump_PreStart，有效起跳逻辑已迁移到 Jump_Start。
/// 保留文件避免 .meta 引用丢失。
/// 不注册、不转换、不参与运行时状态字典。
/// </summary>
public class Jump_PreStart : JumpStateBase
{
    public override EState StateKey => EState.Jump_PreStart;
    public override string StateName => "Jump_PreStart (Deprecated)";

    public override void Enter() { }
    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Rising.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃上升阶段。
/// 每物理帧：读取分量 → 计算重力 → 计算空中控制 → 一次写 velocity → 检测切换。
/// </summary>
public class Jump_Rising : JumpStateBase
{
    private bool _jumpCutApplied;

    public override EState StateKey => EState.Jump_Rising;
    public override string StateName => "Jump_Rising";

    public override void Enter()
    {
        _jumpCutApplied = false;
    }

    public override void Exit() { }

    public override void Update(float deltaTime)
    {
        if (_jumpCutApplied)
            return;

        if (dataBus.jumpReleasedThisFrame || !dataBus.jumpHeld)
        {
            _jumpCutApplied = true;

            if (stateMachine.Config.enableDebugLogs)
                Debug.Log("[Jump] JumpCut applied");
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
            return;

        // 1. 读取当前分量
        float upSpeed = UpwardSpeed;
        float lateralSpeed = LateralSpeed;

        // 2. Jump Cut：削减上升速度（仅一次，在物理帧中生效）
        if (_jumpCutApplied && upSpeed > 0f)
        {
            upSpeed *= config.jumpCutMultiplier;
            _jumpCutApplied = false; // 已应用

            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=JumpCut");
        }

        // 3. 检查天花板
        var probe = stateMachine.GroundProbe;
        if (probe != null && probe.IsHittingCeiling)
        {
            upSpeed = 0f;

            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=Ceiling");

            // 先写速度再切换
            rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
            stateMachine.TransitionTo(EState.Jump_Fall);
            return;
        }

        // 4. 上升速度 <= 0 → 进入 Falling
        if (upSpeed <= 0f)
        {
            if (config.enableDebugLogs)
                Debug.Log("[Jump] Rising -> Falling reason=Apex");

            stateMachine.TransitionTo(EState.Jump_Fall);
            return;
        }

        // 5. 计算重力（选择倍率）
        float gravityMultiplier = upSpeed <= config.apexSpeedThreshold
            ? config.apexGravityMultiplier
            : config.riseGravityMultiplier;

        upSpeed = ComputeGravity(upSpeed, gravityMultiplier, config.maxFallSpeed, fixedDeltaTime);

        // 6. 计算空中横向控制
        lateralSpeed = ComputeAirControl(dataBus.moveInput.x, lateralSpeed, fixedDeltaTime);

        // 7. 一次写入最终 velocity
        rigidbody.velocity = ComposeVelocity(upSpeed, lateralSpeed);
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/Jump_Start.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃起始阶段：验证跳跃资格，设置起跳速度，保留横向速度，立即进入 Rising。
/// </summary>
public class Jump_Start : JumpStateBase
{
    public override EState StateKey => EState.Jump_Start;
    public override string StateName => "Jump_Start";

    public override void Enter()
    {
        var config = stateMachine.Config;
        if (config == null || rigidbody == null)
        {
            Debug.LogWarning("[Jump] Jump_Start: config 或 rigidbody 缺失，取消跳跃");
            stateMachine.CancelJump("MissingConfigOrRigidbody");
            return;
        }

        if (config.enableDebugLogs)
            Debug.Log("[Jump] Startup -> Rising reason=Grounded/Coyote/BufferedLanding");

        // 保留横向速度，设置沿 GravityUp 的起跳速度
        Vector2 v = rigidbody.velocity;
        float lateralSpeed = Vector2.Dot(v, lateralAxis);

        rigidbody.velocity = gravityUp * config.jumpSpeed + lateralAxis * lateralSpeed;

        // 立即进入 Rising
        stateMachine.TransitionTo(EState.Jump_Rising);
    }

    public override void Exit() { }
    public override void Update(float deltaTime) { }
    public override void FixedUpdate(float fixedDeltaTime) { }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/JumpConfigSO.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃参数集中配置。通过 ScriptableObject 在 Inspector 中调整。
/// </summary>
[CreateAssetMenu(
    fileName = "JumpConfig",
    menuName = "ScriptableObject/战斗配置/跳跃配置")]
public class JumpConfigSO : ScriptableObject
{
    [Header("起跳")]
    [Tooltip("沿 GravityUp 的起跳初速度（米/秒）")]
    [Min(0.1f)]
    public float jumpSpeed = 9f;

    [Header("重力")]
    [Tooltip("基础重力加速度（米/秒²）")]
    [Min(0.1f)]
    public float baseGravityAcceleration = 30f;

    [Tooltip("上升时重力倍率")]
    [Min(0.01f)]
    public float riseGravityMultiplier = 1.0f;

    [Tooltip("下落时重力倍率")]
    [Min(0.01f)]
    public float fallGravityMultiplier = 1.8f;

    [Tooltip("接近顶点时重力倍率（越小悬停感越强）")]
    [Min(0.01f)]
    public float apexGravityMultiplier = 0.6f;

    [Tooltip("判定接近顶点的速度阈值（米/秒）")]
    [Min(0.01f)]
    public float apexSpeedThreshold = 2f;

    [Tooltip("松开跳跃键后上升速度的保留比例")]
    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;

    [Tooltip("最大下落速度（米/秒）")]
    [Min(0.1f)]
    public float maxFallSpeed = 20f;

    [Header("时间窗口")]
    [Tooltip("土狼时间（秒）：离开平台后仍可起跳的窗口")]
    [Min(0f)]
    public float coyoteTime = 0.1f;

    [Tooltip("输入缓存时间（秒）：提前按键后落地自动起跳的窗口")]
    [Min(0f)]
    public float jumpBufferTime = 0.1f;

    [Header("空中控制")]
    [Tooltip("空中加速度（米/秒²）")]
    [Min(0f)]
    public float airAcceleration = 20f;

    [Tooltip("空中减速度（米/秒²），无输入时")]
    [Min(0f)]
    public float airDeceleration = 10f;

    [Tooltip("空中最大横向速度（米/秒）")]
    [Min(0.1f)]
    public float maxAirMoveSpeed = 5f;

    [Header("探测")]
    [Tooltip("地面探测距离（含 skinWidth）")]
    [Min(0.01f)]
    public float groundProbeDistance = 0.15f;

    [Tooltip("天花板探测距离")]
    [Min(0.01f)]
    public float ceilingProbeDistance = 0.1f;

    [Tooltip("碰撞皮肤宽度")]
    [Min(0.001f)]
    public float collisionSkinWidth = 0.02f;

    [Header("落地")]
    [Tooltip("轻落地持续时间（秒）")]
    [Min(0.01f)]
    public float landingDuration = 0.03f;

    [Tooltip("重落地速度阈值（预留）")]
    [Min(0f)]
    public float heavyLandingSpeedThreshold = 15f;

    [Header("调试")]
    public bool enableDebugLogs = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        jumpSpeed = Mathf.Max(0.1f, jumpSpeed);
        baseGravityAcceleration = Mathf.Max(0.1f, baseGravityAcceleration);
        maxFallSpeed = Mathf.Max(0.1f, maxFallSpeed);
        landingDuration = Mathf.Max(0.01f, landingDuration);
    }
#endif
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/JumpGroundProbe.cs`

```cs
using UnityEngine;

/// <summary>
/// 地面与天花板探测。使用角色 Collider2D 自身的 Cast 向下检测地面，
/// 向上检测天花板。提供唯一权威的 isGrounded 写入。
/// 这是一个纯 C# 类（非 MonoBehaviour），由 JumpStateMachine 拥有。
/// </summary>
public class JumpGroundProbe
{
    private Collider2D _collider;
    private LayerMask _groundLayerMask;
    private JumpConfigSO _config;

    private bool _isGrounded;
    private bool _isHittingCeiling;
    private Vector2 _groundNormal = Vector2.up;
    private float _groundDistance = float.PositiveInfinity;

    public bool IsGrounded => _isGrounded;
    public bool IsHittingCeiling => _isHittingCeiling;
    public Vector2 GroundNormal => _groundNormal;
    public float GroundDistance => _groundDistance;

    public void Initialize(Collider2D collider, LayerMask groundMask, JumpConfigSO config)
    {
        _collider = collider;
        _groundLayerMask = groundMask;
        _config = config;
    }

    public void Probe()
    {
        _isGrounded = false;
        _isHittingCeiling = false;
        _groundNormal = Vector2.up;
        _groundDistance = float.PositiveInfinity;

        if (_collider == null) return;

        float castDistance = _config != null ? _config.groundProbeDistance : 0.15f;
        float ceilingDistance = _config != null ? _config.ceilingProbeDistance : 0.1f;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _groundLayerMask,
            useTriggers = false
        };

        // 地面检测
        var groundHits = new RaycastHit2D[5];
        int groundHitCount = _collider.Cast(Vector2.down, filter, groundHits, castDistance);

        if (groundHitCount > 0)
        {
            _isGrounded = true;
            _groundNormal = groundHits[0].normal;
            _groundDistance = groundHits[0].distance;
        }

        if (_config != null && _config.enableDebugLogs)
        {
            //Debug.Log($"[GroundProbe] IsGrounded={_isGrounded} dist={(_isGrounded ? _groundDistance.ToString("F3") : "Inf")}");
        }

        // 天花板检测
        var ceilingHits = new RaycastHit2D[5];
        int ceilingHitCount = _collider.Cast(Vector2.up, filter, ceilingHits, ceilingDistance);

        _isHittingCeiling = ceilingHitCount > 0 && ceilingHits[0].normal.y < -0.5f;
    }

    public void DrawGizmos()
    {
        if (_collider == null) return;

        float castDist = _config != null ? _config.groundProbeDistance : 0.15f;
        float ceilDist = _config != null ? _config.ceilingProbeDistance : 0.1f;

        Bounds b = _collider.bounds;

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 groundCenter = new Vector3(b.center.x, b.min.y - castDist * 0.5f, 0f);
        Gizmos.DrawWireCube(groundCenter, new Vector3(b.size.x, castDist, 0f));

        Gizmos.color = _isHittingCeiling ? Color.yellow : Color.gray;
        Vector3 ceilCenter = new Vector3(b.center.x, b.max.y + ceilDist * 0.5f, 0f);
        Gizmos.DrawWireCube(ceilCenter, new Vector3(b.size.x, ceilDist, 0f));
    }
}
```

### `Assets/Script/Character/Core/BasicFunction/Jump/JumpStateBase.cs`

```cs
using UnityEngine;

/// <summary>
/// 跳跃子状态基类。提供重力方向、配置和速度工具方法。
/// 所有速度操作都基于重力方向投影，不直接写 velocity.y。
/// </summary>
public abstract class JumpStateBase
{
    public abstract EState StateKey { get; }
    public abstract string StateName { get; }

    protected JumpStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Rigidbody2D rigidbody;

    protected Vector2 gravityDown = Vector2.down;
    protected Vector2 gravityUp = Vector2.up;
    protected Vector2 lateralAxis = Vector2.right;

    public virtual void Initialize(JumpStateMachine machine, CharacterDataBus bus, Rigidbody2D rb)
    {
        this.stateMachine = machine;
        this.dataBus = bus;
        this.rigidbody = rb;
    }

    public void SetGravityDirection(Vector2 down)
    {
        gravityDown = down.normalized;
        gravityUp = -gravityDown;
        lateralAxis = new Vector2(-gravityDown.y, gravityDown.x).normalized;
    }

    protected float UpwardSpeed => Vector2.Dot(rigidbody.velocity, gravityUp);
    protected float LateralSpeed => Vector2.Dot(rigidbody.velocity, lateralAxis);

    /// <summary>合成速度，每物理帧只写一次</summary>
    protected Vector2 ComposeVelocity(float upwardSpeed, float lateralSpeed)
    {
        return gravityUp * upwardSpeed + lateralAxis * lateralSpeed;
    }

    /// <summary>
    /// 计算重力作用后的上升速度。不直接写 velocity。
    /// </summary>
    protected float ComputeGravity(float currentUpwardSpeed, float multiplier, float maxFallSpeed, float fixedDeltaTime)
    {
        float gravity = stateMachine.Config.baseGravityAcceleration * multiplier;
        float newUpwardSpeed = currentUpwardSpeed - gravity * fixedDeltaTime;

        // 限制最大下落速度
        float maxDownward = -maxFallSpeed;
        if (newUpwardSpeed < maxDownward)
            newUpwardSpeed = maxDownward;

        return newUpwardSpeed;
    }

    /// <summary>
    /// 计算空中横向控制后的横向速度。不直接写 velocity。
    /// </summary>
    protected float ComputeAirControl(float input, float currentLateral, float fixedDeltaTime)
    {
        var config = stateMachine.Config;

        if (Mathf.Abs(input) > 0.01f)
        {
            return Mathf.MoveTowards(
                currentLateral,
                input * config.maxAirMoveSpeed,
                config.airAcceleration * fixedDeltaTime
            );
        }
        else
        {
            return Mathf.MoveTowards(
                currentLateral,
                0f,
                config.airDeceleration * fixedDeltaTime
            );
        }
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }
    public virtual void FixedUpdate(float fixedDeltaTime) { }
}
```
