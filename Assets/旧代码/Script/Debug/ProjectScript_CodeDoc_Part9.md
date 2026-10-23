# ProjectScript Code Documentation - Part 9

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/TurnAroundRequest.cs` | 19 | - | class TurnAroundRequest | 0 |
| `Assets/Script/Enemy/EnemyDamageReceiver.cs` | 82 | - | class EnemyDamageReceiver | 0 |
| `Assets/Script/Enemy/EnemyDataBus.cs` | 36 | - | class EnemyDataBus | 0 |
| `Assets/Script/Enemy/EnemyHealthBar.cs` | 128 | - | class EnemyHealthBar | 0 |
| `Assets/Script/Enemy/EnemyReactionController.cs` | 141 | - | class EnemyReactionController | 0 |
| `Assets/Script/Enemy/EnemyRoot.cs` | 175 | - | class EnemyRoot | 0 |
| `Assets/Script/Enemy/EnemySO.cs` | 51 | - | class EnemySO | 0 |
| `Assets/Script/Enemy/IDamageable.cs` | 20 | - | struct DamageInfo, interface IDamageable | 0 |
| `Assets/Script/Enemy/IEnemySubsystem.cs` | 8 | - | interface IEnemySubsystem | 0 |
| `Assets/Script/Enemy/ITargetable.cs` | 14 | - | interface ITargetable | 0 |
| `Assets/Script/Enemy/LockTargetPoint.cs` | 49 | - | class LockTargetPoint | 0 |
| `Assets/Script/Enum/Enum.cs` | 55 | - | enum GameState, enum CombatImpactType, enum PlayerDirection, enum AimDirection, enum HoldGunState | 0 |
| `Assets/Script/Enum/Enum_ANOIR.cs` | 78 | - | enum ANOIR_BasicActionState_LongTerm, enum ANOIR_BasicActionState_ShortTerm, enum ANOIR_CrouchState, enum ANOIR_AimState, enum ANOIR_CloseAttackState, enum ANOIR_WeaponState | 0 |
| `Assets/Script/GameEvent/CallBack.cs` | 9 | - | - | 0 |
| `Assets/Script/GameEvent/EventBus.cs` | 79 | - | class EventBus | 0 |
| `Assets/Script/GameEvent/EventCenter.cs` | 232 | - | interface IEventInfo, class EventInfo, class EventInfo, class EventCenter | 0 |
| `Assets/Script/GameEvent/GameEvent.cs` | 156 | - | enum GameEvent | 0 |
| `Assets/Script/GameEvent/GameEventHub.cs` | 48 | - | class GameEventHub, struct GameFlowStateChangedEvent, struct HuntFlowStateChangedEvent | 0 |
| `Assets/Script/GameEvent/HuntCombatFlowEvents.cs` | 35 | - | struct BulletImpactEvent, struct EnemyDiedEvent, struct PlayerDamagedEvent, enum SurfaceType | 0 |
| `Assets/Script/GameEvent/HuntWeaponEvents.cs` | 34 | - | struct WeaponFiredEvent, struct WeaponReloadEvent, struct WeaponEmptyEvent, enum WeaponReloadPhase | 0 |

## Public API

### `Assets/Script/Character/Core/TurnAroundRequest.cs`
- `public class TurnAroundRequest : StateRequest
{`
- `public Vector3 TargetDirection {`
- `public TurnAroundRequest(Vector3 direction)
    {`
### `Assets/Script/Enemy/EnemyDamageReceiver.cs`
- `public class EnemyDamageReceiver : MonoBehaviour, IEnemySubsystem, IDamageable
{`
- `public bool CanTakeDamage
    {`
- `public void Initialize(EnemyRoot root)
    {`
- `public void TakeDamage(DamageInfo damageInfo)
    {`
### `Assets/Script/Enemy/EnemyDataBus.cs`
- `public class EnemyDataBus
{`
- `public string enemyName;`
- `public BattleCamp battleCamp = BattleCamp.EnemySide;`
- `public bool isAlive = true;`
- `public bool canBeTargeted = true;`
- `public float currentHp = 100f;`
- `public float maxHp = 100f;`
- `public EnemyRank enemyRank = EnemyRank.Minion;`
- `public void LoadFromSO(EnemySO so)
    {`
### `Assets/Script/Enemy/EnemyHealthBar.cs`
- `public class EnemyHealthBar : MonoBehaviour, IEnemySubsystem
{`
- `public void Initialize(EnemyRoot root)
    {`
### `Assets/Script/Enemy/EnemyReactionController.cs`
- `public class EnemyReactionController : MonoBehaviour, IEnemySubsystem
{`
- `public void Initialize(EnemyRoot root)
    {`
- `public void React(DamageInfo damageInfo)
    {`
### `Assets/Script/Enemy/EnemyRoot.cs`
- `public class EnemyRoot : MonoBehaviour, ITargetable
{`
- `public EnemyDataBus enemyDataBus {`
- `public BattleCamp BattleCamp
    {`
- `public bool IsAlive
    {`
- `public bool CanBeTargeted
    {`
- `public Transform TargetPoint
    {`
- `public Transform Transform => transform;`
- `public void BootstrapOnly()
    {`
### `Assets/Script/Enemy/EnemySO.cs`
- `public class EnemySO : ScriptableObject
{`
- `public Sprite enemyIcon;`
- `public SkeletonDataAsset enemyModel;`
- `public AnimationPlanMappingSO basicAnimationPlanMapping;`
- `public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;`
- `public string enemyName;`
- `public EnemyRank enemyRank;`
- `public BattleCamp battleCamp;`
- `public Facing assetDefaultFacing = Facing.Right;`
- `public float maxHp = 100f;`
- `public EnemyRoot enemyPrefab;`
### `Assets/Script/Enemy/IDamageable.cs`
- `public struct DamageInfo
{`
- `public GameObject attacker;`
- `public float damage;`
- `public Vector2 hitPoint;`
- `public Vector2 hitNormal;`
- `public Vector2 shotDirection;`
- `public CombatImpactType impactType;`
- `public interface IDamageable
{`
### `Assets/Script/Enemy/IEnemySubsystem.cs`
- `public interface IEnemySubsystem
{`
### `Assets/Script/Enemy/ITargetable.cs`
- `public interface ITargetable
{`
### `Assets/Script/Enemy/LockTargetPoint.cs`
- `public class LockTargetPoint : MonoBehaviour
{`
- `public bool canBeLocked = true;`
- `public int priority = 0;`
- `public bool isMainPoint = true;`
- `public EnemyRoot enemyRoot;`
- `public void BindEnemyRoot(EnemyRoot root)
    {`
- `public Vector3 GetWorldPosition()
    {`
- `public bool IsValid()
    {`
### `Assets/Script/Enum/Enum.cs`
- `public enum GameState
{`
- `public enum CombatImpactType
{`
- `public enum HoldGunState
{`
### `Assets/Script/Enum/Enum_ANOIR.cs`
- `public enum ANOIR_BasicActionState_LongTerm //ANOIR主体的实时主要状态
{`
- `public enum ANOIR_BasicActionState_ShortTerm //ANOIR主体的实时主要状态
{`
- `public enum ANOIR_AimState
{`
- `public enum ANOIR_CloseAttackState
{`
- `public enum ANOIR_WeaponState
{`
### `Assets/Script/GameEvent/CallBack.cs`
- `public delegate void CallBack();`
- `public delegate void CallBack<T>(T arg);`
- `public delegate void CallBack<T, X>(T arg1, X arg2);`
- `public delegate void CallBack<T, X, Y>(T arg1, X arg2, Y arg3);`
- `public delegate void CallBack<T, X, Y, Z>(T arg1, X arg2, Y arg3, Z arg4);`
### `Assets/Script/GameEvent/EventBus.cs`
- `public sealed class EventBus
{`
- `public void Subscribe<TEvent>(Action<TEvent> handler)
    {`
- `public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {`
- `public void Publish<TEvent>(TEvent eventData)
    {`
- `public void Clear()
    {`
- `public int GetListenerCount<TEvent>()
    {`
### `Assets/Script/GameEvent/EventCenter.cs`
- `public interface IEventInfo
{`
- `public class EventInfo<T> : IEventInfo
{`
- `public UnityAction<T> actions;`
- `public EventInfo( UnityAction<T> action)
    {`
- `public class EventInfo : IEventInfo
{`
- `public UnityAction actions;`
- `public EventInfo(UnityAction action)
    {`
- `public class EventCenter : BaseManager<EventCenter>
{`
- `public void AddEventListener<T>(GameEvent eventType, UnityAction<T> action)
    {`
- `public void AddEventListener(GameEvent eventType, UnityAction action)
    {`
- `public void RemoveEventListener<T>(GameEvent eventType, UnityAction<T> action)
    {`
- `public void RemoveEventListener(GameEvent eventType, UnityAction action)
    {`
- `public void EventTrigger<T>(GameEvent eventType, T info)
    {`
- `public void EventTrigger(GameEvent eventType)
    {`
- `public void AddEventListener<T>(string name, UnityAction<T> action)
    {`
- `public void AddEventListener(string name, UnityAction action)
    {`
- `public void RemoveEventListener<T>(string name, UnityAction<T> action)
    {`
- `public void RemoveEventListener(string name, UnityAction action)
    {`
- `public void EventTrigger<T>(string name, T info)
    {`
- `public void EventTrigger(string name)
    {`
- `public void Clear()
    {`
### `Assets/Script/GameEvent/GameEvent.cs`
- `public enum GameEvent
{`
### `Assets/Script/GameEvent/GameEventHub.cs`
- `public sealed class GameEventHub
{`
- `public EventBus GameFlow {`
- `public EventBus Hunt {`
- `public EventBus UI {`
- `public EventBus Audio {`
- `public void ClearHunt()
    {`
- `public void ClearUI()
    {`
- `public void ClearAudio()
    {`
- `public void ClearAll()
    {`
- `public struct GameFlowStateChangedEvent
{`
- `public GameFlowStateType from;`
- `public GameFlowStateType to;`
- `public struct HuntFlowStateChangedEvent
{`
- `public GameFlowStateType from;`
- `public GameFlowStateType to;`
### `Assets/Script/GameEvent/HuntCombatFlowEvents.cs`
- `public struct BulletImpactEvent
{`
- `public Vector3 position;`
- `public Vector2 normal;`
- `public GameObject hitObject;`
- `public SurfaceType surfaceType;`
- `public bool hitEnemy;`
- `public WeaponAudioProfileSO audioProfile;`
- `public struct EnemyDiedEvent
{`
- `public GameObject enemy;`
- `public Vector3 position;`
- `public struct PlayerDamagedEvent
{`
- `public GameObject player;`
- `public Vector3 position;`
- `public float damage;`
- `public enum SurfaceType
{`
### `Assets/Script/GameEvent/HuntWeaponEvents.cs`
- `public struct WeaponFiredEvent
{`
- `public GameObject owner;`
- `public Vector3 position;`
- `public Vector2 direction;`
- `public WeaponAudioProfileSO audioProfile;`
- `public bool isPlayer;`
- `public struct WeaponReloadEvent
{`
- `public GameObject owner;`
- `public Vector3 position;`
- `public WeaponAudioProfileSO audioProfile;`
- `public WeaponReloadPhase phase;`
- `public struct WeaponEmptyEvent
{`
- `public GameObject owner;`
- `public Vector3 position;`
- `public WeaponAudioProfileSO audioProfile;`
- `public bool isPlayer;`
- `public enum WeaponReloadPhase
{`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/TurnAroundRequest.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具体的状态请求实现
/// </summary>
public class TurnAroundRequest : StateRequest
{
    public Vector3 TargetDirection { get; private set; }

    public TurnAroundRequest(Vector3 direction)
    {
        characterStateType = CharacterStateType.TurnAround;
        TargetDirection = direction;
        Data = direction; // 同时存到Data中
    }
}
```

### `Assets/Script/Enemy/EnemyDamageReceiver.cs`

```cs
using UnityEngine;

public class EnemyDamageReceiver : MonoBehaviour, IEnemySubsystem, IDamageable
{
    private EnemyRoot enemyRoot;
    private EnemyReactionController reactionController;

    public bool CanTakeDamage
    {
        get
        {
            return enemyRoot != null
                && enemyRoot.enemyDataBus != null
                && enemyRoot.enemyDataBus.isAlive
                && enemyRoot.enemyDataBus.currentHp > 0f;
        }
    }

    public void Initialize(EnemyRoot root)
    {
        enemyRoot = root;
    }

    private void Awake()
    {
        if (enemyRoot == null)
            enemyRoot = GetComponentInParent<EnemyRoot>();
    }

    private void Start()
    {
        if (reactionController == null)
            reactionController = GetComponentInParent<EnemyReactionController>();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!CanTakeDamage)
            return;

        EnemyDataBus dataBus = enemyRoot.enemyDataBus;

        dataBus.currentHp -= Mathf.Max(0f, damageInfo.damage);

        Debug.Log(
            $"[EnemyDamageReceiver] {enemyRoot.name} 受到伤害={damageInfo.damage} " +
            $"类型={damageInfo.impactType} " +
            $"剩余HP={dataBus.currentHp:F1}/{dataBus.maxHp:F1}",
            this
        );

        // 转发给反应控制器（由敌人自己决定如何反应）
        if (reactionController == null)
            reactionController = GetComponentInParent<EnemyReactionController>();

        if (reactionController != null)
        {
            reactionController.React(damageInfo);
        }

        if (dataBus.currentHp <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (enemyRoot == null || enemyRoot.enemyDataBus == null)
            return;

        EnemyDataBus dataBus = enemyRoot.enemyDataBus;

        Debug.Log($"[EnemyDamageReceiver] {enemyRoot.name} HP归零，自动补满", this);

        // 测试模式：HP归0后自动补满，不真正死亡
        dataBus.currentHp = dataBus.maxHp;
        dataBus.isAlive = true;
        dataBus.canBeTargeted = true;
    }
}
```

### `Assets/Script/Enemy/EnemyDataBus.cs`

```cs
using Com.LuisPedroFonseca.ProCamera2D.TopDownShooter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDataBus
{
    public string enemyName;

    public BattleCamp battleCamp = BattleCamp.EnemySide;

    public bool isAlive = true;

    public bool canBeTargeted = true;

    public float currentHp = 100f;

    public float maxHp = 100f;

    public EnemyRank enemyRank = EnemyRank.Minion;

    public void LoadFromSO(EnemySO so)
    {
        if (so == null)
            return;

        enemyName = so.enemyName;
        battleCamp = so.battleCamp;
        maxHp = so.maxHp;
        currentHp = maxHp;
        enemyRank = so.enemyRank;

        isAlive = true;
        canBeTargeted = true;
    }
}
```

### `Assets/Script/Enemy/EnemyHealthBar.cs`

```cs
using UnityEngine;

/// <summary>
/// 敌人头顶血条。使用 SpriteRenderer 实现，
/// 从 EnemyDataBus 读取 currentHp/maxHp 并更新填充比例。
/// </summary>
public class EnemyHealthBar : MonoBehaviour, IEnemySubsystem
{
    [Header("外观")]
    [SerializeField] private Vector3 barOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float barWidth = 1.2f;
    [SerializeField] private float barHeight = 0.15f;
    [SerializeField] private Color bgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color fillColor = new Color(0f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color fillColorMid = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] private Color fillColorLow = new Color(0.9f, 0.1f, 0.1f, 1f);
    [SerializeField] private string sortingLayerName = "Character1";
    [SerializeField] private int sortingOrder = 100;

    private EnemyRoot _root;
    private GameObject _barRoot;
    private SpriteRenderer _bgRenderer;
    private SpriteRenderer _fillRenderer;
    private Sprite _whiteSprite;
    private float _lastHp = float.NaN;

    public void Initialize(EnemyRoot root)
    {
        _root = root;
    }

    private void Awake()
    {
        if (_root == null)
            _root = GetComponentInParent<EnemyRoot>();
    }

    private void Start()
    {
        CreateBar();
    }

    private void LateUpdate()
    {
        if (_root?.enemyDataBus == null)
            return;

        float hp = _root.enemyDataBus.currentHp;
        if (Mathf.Approximately(hp, _lastHp))
            return;

        _lastHp = hp;
        RefreshBar();
    }

    private void CreateBar()
    {
        if (_barRoot != null)
            return;

        _whiteSprite = CreateWhiteSprite();

        _barRoot = new GameObject("HealthBar");
        _barRoot.transform.SetParent(transform, false);
        _barRoot.transform.localPosition = barOffset;
        _barRoot.transform.localScale = Vector3.one;

        // Background
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(_barRoot.transform, false);
        _bgRenderer = bgGO.AddComponent<SpriteRenderer>();
        _bgRenderer.sprite = _whiteSprite;
        _bgRenderer.color = bgColor;
        _bgRenderer.sortingLayerName = sortingLayerName;
        _bgRenderer.sortingOrder = sortingOrder;
        bgGO.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // Fill (anchored to left edge)
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        _fillRenderer = fillGO.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite = _whiteSprite;
        _fillRenderer.color = fillColor;
        _fillRenderer.sortingLayerName = sortingLayerName;
        _fillRenderer.sortingOrder = sortingOrder + 1;
        // Fill covers full BG, then we scale X and reposition to left-align
        fillGO.transform.localScale = new Vector3(1f, 1f, 1f);
        fillGO.transform.localPosition = new Vector3(-0.5f, 0f, 0f); // left-align pivot
    }

    private void RefreshBar()
    {
        if (_barRoot == null)
            CreateBar();
        if (_barRoot == null || _root?.enemyDataBus == null)
            return;

        EnemyDataBus db = _root.enemyDataBus;
        float ratio = db.maxHp > 0f ? Mathf.Clamp01(db.currentHp / db.maxHp) : 0f;

        // Scale fill horizontally, keep left-aligned
        _fillRenderer.transform.localScale = new Vector3(ratio, 1f, 1f);
        // With center pivot, adjust position so left edge stays at BG's left edge (-0.5)
        _fillRenderer.transform.localPosition = new Vector3(-0.5f + ratio * 0.5f, 0f, 0f);

        Color c;
        if (ratio > 0.5f)
            c = Color.Lerp(fillColorMid, fillColor, (ratio - 0.5f) * 2f);
        else
            c = Color.Lerp(fillColorLow, fillColorMid, ratio * 2f);
        _fillRenderer.color = c;

        bool show = db.isAlive && ratio > 0f;
        _barRoot.SetActive(show);
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[4 * 4];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
```

### `Assets/Script/Enemy/EnemyReactionController.cs`

```cs
using System.Collections;
using UnityEngine;

/// <summary>
/// 敌人受击反应控制器。根据 CombatImpactType 决定敌人的受击反应：
/// - Gunshot: 输出日志，当前版本不产生位移
/// - Kick: 产生后退位移（Dynamic 用 AddForce，Kinematic 用协程 MovePosition）
/// - 其他类型: 输出日志
///
/// 设计原则：受击方决定自己的反应，不由攻击方硬编码。
/// </summary>
public class EnemyReactionController : MonoBehaviour, IEnemySubsystem
{
    [Header("受击位移")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private float kickKnockbackDistance = 0.25f;
    [SerializeField] private float kickKnockbackDuration = 0.08f;
    [SerializeField] private float meleeWeaponKnockbackDistance = 0.2f;
    [SerializeField] private float meleeWeaponKnockbackDuration = 0.08f;

    [Header("调试")]
    [SerializeField] private bool logReactions = true;

    private EnemyRoot enemyRoot;
    private bool _isKnockbacking;

    public void Initialize(EnemyRoot root)
    {
        enemyRoot = root;
        if (body == null)
            body = GetComponentInParent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponentInParent<Rigidbody2D>();
    }

    /// <summary>
    /// 由 EnemyDamageReceiver 调用。根据攻击类型选择受击反应。
    /// </summary>
    public void React(DamageInfo damageInfo)
    {
        switch (damageInfo.impactType)
        {
            case CombatImpactType.Gunshot:
                ReactToGunshot(damageInfo);
                break;

            case CombatImpactType.Kick:
                ReactToKick(damageInfo);
                break;

            case CombatImpactType.MeleeWeapon:
                ReactToMeleeWeapon(damageInfo);
                break;

            default:
                if (logReactions)
                    Debug.Log($"[EnemyReaction] {gameObject.name} 收到未配置攻击类型: {damageInfo.impactType}", this);
                break;
        }
    }

    private void ReactToGunshot(DamageInfo damageInfo)
    {
        if (logReactions)
            Debug.Log($"[EnemyReaction] {gameObject.name} 受到枪击，当前版本仅输出日志", this);
    }

    private void ReactToKick(DamageInfo damageInfo)
    {
        Vector2 dir = GetKnockbackDirection(damageInfo);
        if (logReactions)
            Debug.Log($"[EnemyReaction] {gameObject.name} 受到踢击，后退方向={dir}", this);
        ApplyKnockback(dir, kickKnockbackDistance, kickKnockbackDuration);
    }

    private void ReactToMeleeWeapon(DamageInfo damageInfo)
    {
        Vector2 dir = GetKnockbackDirection(damageInfo);
        if (logReactions)
            Debug.Log($"[EnemyReaction] {gameObject.name} 受到近战武器攻击，后退方向={dir}", this);
        ApplyKnockback(dir, meleeWeaponKnockbackDistance, meleeWeaponKnockbackDuration);
    }

    private Vector2 GetKnockbackDirection(DamageInfo damageInfo)
    {
        Vector2 dir = damageInfo.shotDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.right;
        return dir.normalized;
    }

    private void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (body == null)
        {
            Debug.LogWarning($"[EnemyReaction] {gameObject.name} 没有 Rigidbody2D，无法产生位移", this);
            return;
        }

        if (_isKnockbacking)
            return; // 防止叠加

        if (body.bodyType == RigidbodyType2D.Dynamic)
        {
            // Dynamic: 用 AddForce
            float force = distance / Mathf.Max(duration, 0.01f) * body.mass;
            body.AddForce(direction * force, ForceMode2D.Impulse);
        }
        else
        {
            // Kinematic/Static: 用协程 MovePosition
            StartCoroutine(KnockbackCoroutine(direction, distance, duration));
        }
    }

    private IEnumerator KnockbackCoroutine(Vector2 direction, float distance, float duration)
    {
        _isKnockbacking = true;
        Vector2 startPos = body.position;
        Vector2 endPos = startPos + direction * distance;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease-out
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, eased);
            body.MovePosition(currentPos);
            yield return null;
        }

        _isKnockbacking = false;
    }
}
```

### `Assets/Script/Enemy/EnemyRoot.cs`

```cs
using Com.LuisPedroFonseca.ProCamera2D.TopDownShooter;
using Spine.Unity;
using UnityEngine;

public class EnemyRoot : MonoBehaviour, ITargetable
{
    private bool _isInitialized;

    public EnemyDataBus enemyDataBus { get; private set; }

    [Header("Target Points")]
    [SerializeField] private Transform targetPoint;

    [Header("Enemy Config")]
    [SerializeField] private EnemySO enemySO;

    [Header("Enemy Spine")]
    [SerializeField] private SkeletonAnimation weaponSkeletonAnimation;

    [Header("Layer Setup")]
    [SerializeField] private bool setupLayerOnBootstrap = true;
    [SerializeField, ReadOnly] private string enemyLayerName = "Render_Character";
    [SerializeField] private bool applyLayerToChildren = true;

    [SerializeField] private Collider2D collider2D;
    [SerializeField] private LockTargetPoint lockTargetPoint;





    public BattleCamp BattleCamp
    {
        get
        {
            if (enemyDataBus == null)
                return BattleCamp.EnemySide;

            return enemyDataBus.battleCamp;
        }
    }

    public bool IsAlive
    {
        get
        {
            if (enemyDataBus == null)
                return false;

            return enemyDataBus.isAlive;
        }
    }

    public bool CanBeTargeted
    {
        get
        {
            if (enemyDataBus == null)
                return false;

            return enemyDataBus.canBeTargeted;
        }
    }

    public Transform TargetPoint
    {
        get
        {
            return targetPoint != null ? targetPoint : transform;
        }
    }

    public Transform Transform => transform;

    private void Awake()
    {
        BootstrapOnly();
    }

    public void BootstrapOnly()
    {
        if (_isInitialized)
            return;


        SetupLayer();

        enemyDataBus = new EnemyDataBus();

        if (enemySO != null)
        {
            enemyDataBus.LoadFromSO(enemySO);
        }

        AutoBindTargetPoints();
        ValidateEnemySetup();
        InitializeEnemySubsystems();

        _isInitialized = true;
    }

    private void InitializeEnemySubsystems()
    {
        IEnemySubsystem[] subsystems = GetComponentsInChildren<IEnemySubsystem>(true);

        for (int i = 0; i < subsystems.Length; i++)
        {
            subsystems[i].Initialize(this);
        }
    }

    private void SetupLayer()
    {
        if (!setupLayerOnBootstrap)
            return;

        int layer = LayerMask.NameToLayer(enemyLayerName);

        if (layer < 0)
        {
            Debug.LogError($"[EnemyRoot] 找不到 Layer: {enemyLayerName}，请先在 Project Settings/Tags and Layers 中创建。", this);
            return;
        }

        if (applyLayerToChildren)
        {
            SetLayerRecursive(transform, layer);
        }
        else
        {
            gameObject.layer = layer;
        }
    }

    private void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursive(root.GetChild(i), layer);
        }
    }

    private void AutoBindTargetPoints()
    {
        LockTargetPoint[] points = GetComponentsInChildren<LockTargetPoint>(true);

        for (int i = 0; i < points.Length; i++)
        {
            points[i].BindEnemyRoot(this);
        }
    }

    private void ValidateEnemySetup()
    {


        if (collider2D == null)
        {
            collider2D = GetComponentInChildren<Collider2D>();
            Debug.LogWarning(
    "[EnemyRoot] 当前敌人没有 Collider2D，锁敌搜索和枪械 Raycast 都无法命中它。",
    this
);
        }

        lockTargetPoint = GetComponentInChildren<LockTargetPoint>(true);

        if (lockTargetPoint == null)
        {
            Debug.LogWarning("[EnemyRoot] 当前敌人没有 LockTargetPoint，角色即使搜索到 Collider 也无法锁定具体点。", this);
        }
    }
}
```

### `Assets/Script/Enemy/EnemySO.cs`

```cs
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "ScriptableObject/敌人/敌人创建")]
public class EnemySO : ScriptableObject
{

    [PreviewField(80)] // 重点：这是让 Odin 在任何地方都能看到图的前提
    public Sprite enemyIcon;

    //[LabelText("骨骼数据")]
    public SkeletonDataAsset enemyModel;//Spine模型数据

    //===========动画数据接入===================

    public AnimationPlanMappingSO basicAnimationPlanMapping;
    public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;



    //=============角色属性

    public string enemyName;

    [Header("逻辑属性")]//提供角色的专属属性由dataBus读取

    public EnemyRank enemyRank;//敌人等级，影响属性和技能，需要在SO具体指定

    public BattleCamp battleCamp;//所属阵营，需要在SO具体指定

    public Facing assetDefaultFacing = Facing.Right; // 美术画出来的默认朝向(防止美术模板方向搞反）


    public float maxHp = 100f;//生命值，初始值由SO具体指定，战斗中根据受击情况更新


    public EnemyRoot enemyPrefab;









}
```

### `Assets/Script/Enemy/IDamageable.cs`

```cs

using UnityEngine;

public struct DamageInfo
{
    public GameObject attacker;
    public float damage;
    public Vector2 hitPoint;
    public Vector2 hitNormal;
    public Vector2 shotDirection;
    public CombatImpactType impactType;
}


public interface IDamageable
{
    bool CanTakeDamage { get; }

    void TakeDamage(DamageInfo damageInfo);
}
```

### `Assets/Script/Enemy/IEnemySubsystem.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEnemySubsystem
{
    void Initialize(EnemyRoot root);
}
```

### `Assets/Script/Enemy/ITargetable.cs`

```cs
using UnityEngine;

public interface ITargetable
{
    BattleCamp BattleCamp { get; }

    bool IsAlive { get; }

    bool CanBeTargeted { get; }

    Transform TargetPoint { get; }

    Transform Transform { get; }
}
```

### `Assets/Script/Enemy/LockTargetPoint.cs`

```cs
using UnityEngine;

public class LockTargetPoint : MonoBehaviour
{
    [Header("锁定点配置")]
    public bool canBeLocked = true;

    [Tooltip("优先级越高越容易被锁定")]
    public int priority = 0;

    [Tooltip("如果一个敌人有多个锁定点，这个作为默认主锁定点")]
    public bool isMainPoint = true;

    [Header("归属角色")]
    public EnemyRoot enemyRoot;

    private void Awake()
    {
        if (enemyRoot == null)
            enemyRoot = GetComponentInParent<EnemyRoot>();
    }

    public void BindEnemyRoot(EnemyRoot root)
    {
        enemyRoot = root;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public bool IsValid()
    {
        if (!canBeLocked)
            return false;

        if (enemyRoot == null)
            return false;

        if (!enemyRoot.IsAlive)
            return false;

        if (!enemyRoot.CanBeTargeted)
            return false;

        return true;
    }
}
```

### `Assets/Script/Enum/Enum.cs`

```cs
public enum GameState
{
    gameStarted,
    playingLevel,
    engagingEnemies,
    bossStage,
    engagingBoss,
    levelCompleted,
    gameWon,
    gameLost,
    gamePaused,
    dungeonOverviewMap,
    restartGame
}

/// <summary>
/// 攻击效果携带的攻击类型。由攻击方（DamageEffectSO / GunSystemController）写入，
/// 由受击方（EnemyReactionController）读取并决定反应。
/// </summary>
public enum CombatImpactType
{
    Unknown = 0,
    Gunshot = 10,
    Kick = 20,
    MeleeWeapon = 30,
    Explosion = 40,
    Skill = 50
}

//public enum PlayerDirection
//{
//    PlayerLeft,
//    PlayerRight
//}



//public enum AimDirection
//{

//    Aim_Middle,
//    Aim_Up,
//    Aim_Down


//}

public enum HoldGunState
{
    HoldMachineGun,
    HoldPistal



}
```

### `Assets/Script/Enum/Enum_ANOIR.cs`

```cs
//本脚本用于定义ANOIR的状态






public enum ANOIR_BasicActionState_LongTerm //ANOIR主体的实时主要状态
{
    //每个状态匹配一个动画，如果动画大类有区别，一定要设立一个新状态种类
    ANOIR_Walk,
    ANOIR_Idle,
    ANOIR_Run,
    ANOIR_Crouch,
    ANOIR_CrouchWalk,

    ANOIR_Jump,


}


public enum ANOIR_BasicActionState_ShortTerm //ANOIR主体的实时主要状态
{
    //每个状态匹配一个动画，如果动画大类有区别，一定要设立一个新状态种类
    NoneState,
    ANOIR_Avoid,
    ANOIR_Slide,
    ANOIR_BrakeStop,  // 新增刹停状态




    ANOIR_JumpStart,
    ANOIR_JumpInAir,
    ANOIR_JumpEnd,

}








//public enum ANOIR_CrouchState
//{
//    ANOIR_IsCrouch,
//    ANOIR_NoCrouch


//}




public enum ANOIR_AimState
{
    ANOIR_Aim,
    ANOIR_NoAim
}


public enum ANOIR_CloseAttackState
{
    ANOIR_CloseAttack,
    ANOIR_NoCloseAttack

}



public enum ANOIR_WeaponState
{
    ANOIR_HoldMachineGun,
    ANOIR_HoldPistal
}
```

### `Assets/Script/GameEvent/CallBack.cs`

```cs
//delegate = 委托
//这里你可以理解为拨号的类型转接
public delegate void CallBack();
//T是变量类型（比如int）arg是变量名称
public delegate void CallBack<T>(T arg);
public delegate void CallBack<T, X>(T arg1, X arg2);
public delegate void CallBack<T, X, Y>(T arg1, X arg2, Y arg3);
public delegate void CallBack<T, X, Y, Z>(T arg1, X arg2, Y arg3, Z arg4);
```

### `Assets/Script/GameEvent/EventBus.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EventBus
{
    private readonly Dictionary<Type, Delegate> listeners = new Dictionary<Type, Delegate>();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);

        if (listeners.TryGetValue(eventType, out Delegate existing))
        {
            listeners[eventType] = Delegate.Combine(existing, handler);
        }
        else
        {
            listeners.Add(eventType, handler);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            return;

        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return;

        Delegate current = Delegate.Remove(existing, handler);

        if (current == null)
        {
            listeners.Remove(eventType);
        }
        else
        {
            listeners[eventType] = current;
        }
    }

    public void Publish<TEvent>(TEvent eventData)
    {
        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return;

        if (existing is Action<TEvent> callback)
        {
            callback.Invoke(eventData);
        }
        else
        {
            Debug.LogError($"[EventBus] 事件类型监听器不匹配: {eventType.Name}");
        }
    }

    public void Clear()
    {
        listeners.Clear();
    }

    public int GetListenerCount<TEvent>()
    {
        Type eventType = typeof(TEvent);

        if (!listeners.TryGetValue(eventType, out Delegate existing))
            return 0;

        return existing.GetInvocationList().Length;
    }
}
```

### `Assets/Script/GameEvent/EventCenter.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public interface IEventInfo
{

}

public class EventInfo<T> : IEventInfo
{
    public UnityAction<T> actions;

    public EventInfo( UnityAction<T> action)
    {
        actions += action;
    }
}

public class EventInfo : IEventInfo
{
    public UnityAction actions;

    public EventInfo(UnityAction action)
    {
        actions += action;
    }
}


/// <summary>
/// 事件中心 单例模式对象
/// 1.Dictionary
/// 2.委托
/// 3.观察者设计模式
/// 4.泛型
/// </summary>
public class EventCenter : BaseManager<EventCenter>
{
    //key —— 事件的名字（比如：怪物死亡，玩家死亡，通关 等等）
    //value —— 对应的是 监听这个事件 对应的委托函数们
    private Dictionary<string, IEventInfo> eventDic = new Dictionary<string, IEventInfo>();//事件中心的核心成员变量，事件名string，委托变量IEventInfo
    private Dictionary<GameEvent, IEventInfo> enumEventDic =  new Dictionary<GameEvent, IEventInfo>();//自创的枚举版本


    #region 枚举事件系统（新增功能）
    /// <summary>
    /// 添加枚举事件监听（带参数）
    /// </summary>
    public void AddEventListener<T>(GameEvent eventType, UnityAction<T> action)
    {
        if (enumEventDic.ContainsKey(eventType))
        {
            (enumEventDic[eventType] as EventInfo<T>).actions += action;
        }
        else
        {
            enumEventDic.Add(eventType, new EventInfo<T>(action));
        }
    }

    /// <summary>
    /// 添加枚举事件监听（无参数）
    /// </summary>
    public void AddEventListener(GameEvent eventType, UnityAction action)
    {
        if (enumEventDic.ContainsKey(eventType))
        {
            (enumEventDic[eventType] as EventInfo).actions += action;
        }
        else
        {
            enumEventDic.Add(eventType, new EventInfo(action));
        }
    }

    /// <summary>
    /// 移除枚举事件监听（带参数）
    /// </summary>
    public void RemoveEventListener<T>(GameEvent eventType, UnityAction<T> action)
    {
        if (enumEventDic.ContainsKey(eventType))
            (enumEventDic[eventType] as EventInfo<T>).actions -= action;
    }

    /// <summary>
    /// 移除枚举事件监听（无参数）
    /// </summary>
    public void RemoveEventListener(GameEvent eventType, UnityAction action)
    {
        if (enumEventDic.ContainsKey(eventType))
            (enumEventDic[eventType] as EventInfo).actions -= action;
    }

    /// <summary>
    /// 触发枚举事件（带参数）- 类似广播
    /// </summary>
    public void EventTrigger<T>(GameEvent eventType, T info)
    {
        if (enumEventDic.ContainsKey(eventType))
        {
            var eventInfo = enumEventDic[eventType] as EventInfo<T>;
            if (eventInfo?.actions != null)
                eventInfo.actions.Invoke(info);
        }
    }

    /// <summary>
    /// 触发枚举事件（无参数）- 类似广播
    /// </summary>
    public void EventTrigger(GameEvent eventType)
    {
        if (enumEventDic.ContainsKey(eventType))
        {
            var eventInfo = enumEventDic[eventType] as EventInfo;
            if (eventInfo?.actions != null)
                eventInfo.actions.Invoke();
        }
    }
    #endregion



    /// <summary>
    /// 添加事件监听
    /// </summary>
    /// <param name="name">事件的名字</param>
    /// <param name="action">准备用来处理事件 的委托函数</param>
    public void AddEventListener<T>(string name, UnityAction<T> action)
    {
        //有没有对应的事件监听
        //有的情况
        if( eventDic.ContainsKey(name) )
        {
            (eventDic[name] as EventInfo<T>).actions += action;
        }
        //没有的情况
        else
        {
            eventDic.Add(name, new EventInfo<T>( action ));
        }
    }

    /// <summary>
    /// 监听不需要参数传递的事件
    /// </summary>
    /// <param name="name"></param>
    /// <param name="action"></param>
    public void AddEventListener(string name, UnityAction action)
    {
        //有没有对应的事件监听
        //有的情况
        if (eventDic.ContainsKey(name))
        {
            (eventDic[name] as EventInfo).actions += action;
        }
        //没有的情况
        else
        {
            eventDic.Add(name, new EventInfo(action));
        }
    }


    /// <summary>
    /// 移除对应的事件监听
    /// </summary>
    /// <param name="name">事件的名字</param>
    /// <param name="action">对应之前添加的委托函数</param>
    public void RemoveEventListener<T>(string name, UnityAction<T> action)
    {
        if (eventDic.ContainsKey(name))
            (eventDic[name] as EventInfo<T>).actions -= action;
    }

    /// <summary>
    /// 移除不需要参数的事件
    /// </summary>
    /// <param name="name"></param>
    /// <param name="action"></param>
    public void RemoveEventListener(string name, UnityAction action)
    {
        if (eventDic.ContainsKey(name))
            (eventDic[name] as EventInfo).actions -= action;
    }

    /// <summary>
    /// 事件触发
    /// </summary>
    /// <param name="name">哪一个名字的事件触发了</param>
    public void EventTrigger<T>(string name, T info)
    {
        //有没有对应的事件监听
        //有的情况
        if (eventDic.ContainsKey(name))
        {
            //eventDic[name]();
            if((eventDic[name] as EventInfo<T>).actions != null)
                (eventDic[name] as EventInfo<T>).actions.Invoke(info);
            //eventDic[name].Invoke(info);
        }
    }

    /// <summary>
    /// 事件触发（不需要参数的）
    /// </summary>
    /// <param name="name"></param>
    public void EventTrigger(string name)
    {
        //有没有对应的事件监听
        //有的情况
        if (eventDic.ContainsKey(name))
        {
            //eventDic[name]();
            if ((eventDic[name] as EventInfo).actions != null)
                (eventDic[name] as EventInfo).actions.Invoke();
            //eventDic[name].Invoke(info);
        }
    }

    /// <summary>
    /// 清空事件中心
    /// 主要用在 场景切换时
    /// </summary>
    public void Clear()
    {
        eventDic.Clear();
    }
}
```

### `Assets/Script/GameEvent/GameEvent.cs`

```cs
public enum GameEvent
{
    //======================动画系统=============
    AnimationRequest,
    AnimationPlanRequest,

    #region AINO的动作
    ANOIR_MoveOrNot,

    ANOIR_IsWalk,
    ANOIR_IsJog,
    ANOIR_IsDash,
    ANOIR_IsIdle,
    ANOIR_IsCrouch,
    ANOIR_IsCrouchWalk,
    ANOIR_IsAvoid,
    ANOIR_IsAim,
    ANOIR_IsReload,//用于换弹动画
    ANOIR_IsEquip,//用于装备武器动画
    ANOIR_IsDisboard,//用于卸载武器动画

    ANOIR_IsSlide,

    ANOIR_IsBrakeStop,



    VELGITA_ShootTriggered,
    VELGITA_ShootEnd,






    ANOIR_IsJump,//概括所有自主滞空情况，包括主动跳跃，直接下落等

    ANOIR_IsJumpStart,
    ANOIR_IsInAir,
    ANOIR_IsJumpEnd,


    VELGITA_IsFireOrLightAttack_StartSingle,
    VELGITA_IsFireOrLightAttack_StartContinue,
    VELGITA_IsFireOrLightAttack_End,






    ANOIR_IsCloseAttack,
    ANOIR_IsAttack,




    #region 输入阻断
    ANOIR_CanNotAct,
    ANOIR_CanAct,

    #endregion

    #region 受伤状态
    ANOIR_Injured_Start,
    ANOIR_Injured_End,

    #endregion





    #endregion

    #region 鼠标输入状态
    Input_LeftButtonDown,
    Input_LeftButtonContinue,
    Input_LeftButtonUp,
    Input_RightButtonDown,
    Input_RightButtonContinue,
    Input_RightButtonUp,
    Input_ScrollUp,
    Input_ScrollDown,
    #endregion


    #region 键盘输入状态(使用高级输入系统，只需要up/dwon两个状态
    Input_CrouchKeyDown,//下蹲
    Input_CrouchKeyUp,
    Input_MovementKeyDown,//移动按键较为特殊，只需要判断是否摁下（WSAD）
    Input_MovementKeyUp,
    Input_PickUpKeyDown,//拾取
    Input_PickUpKeyUp,
    Input_DashKeyDown,//冲刺
    Input_DashKeyUp,
    Input_AvoidKeyDown,//冲刺
    Input_AvoidKeyUp,
    Input_WeaponStock1KeyDown,
    Input_WeaponStock2KeyDown,
    Input_ReloadKeyDown,
    Input_ReloadKeyUp,

    #endregion

    #region 武器系统/道具系统
    //广播：PickUpItem
    //接收：WeaponManager/
    ANOIR_GetItem,
    ANOIR_GetWeapon,
    ANOIR_HasEquipWeapon,
    ANOIR_HasDisboardWeapon,

    #endregion



    #region 背包系统
    ANOIR_BagSwitch,


    #endregion



    #region 战斗系统
    //============UI============
    ShowSkillUI,    // 逻辑层通知 UI：请刷新这 4 个技能图标

    RefreshTimeLineUI, // 逻辑层通知 UI：请刷新技能图标（比如变灰、亮起等）

    TargetSelect,//进入TargetSelectState,选择技能释放目标

    SkillSelectChange,//当玩家选择了一个技能，流程进入了TargetSelectState，却又点击了另一个技能UI图标，发布这个通知
    SkillClicked,  // UI 层通知逻辑层：玩家刚才点击了这个技能图标
    SkillSelectFinish,// 玩家选完了，发布通知
    ShowCharacterPromptUI,// 逻辑层通知 UI：请显示这个角色的提示UI（比如头像、名字等）









    BattleSystem_Start, 
    
    BattleSystem_End,


    #endregion



}
```

### `Assets/Script/GameEvent/GameEventHub.cs`

```cs
//GameEventHub 是一个容器
//EventBus 是真正的事件总线
//GameFlow / Hunt / UI / Audio 是不同事件领域


public sealed class GameEventHub
{
    public EventBus GameFlow { get; } = new EventBus();
    public EventBus Hunt { get; } = new EventBus();
    public EventBus UI { get; } = new EventBus();
    public EventBus Audio { get; } = new EventBus();

    public void ClearHunt()
    {
        Hunt.Clear();
    }

    public void ClearUI()
    {
        UI.Clear();
    }

    public void ClearAudio()
    {
        Audio.Clear();
    }

    public void ClearAll()
    {
        GameFlow.Clear();
        Hunt.Clear();
        UI.Clear();
        Audio.Clear();
    }
}

public struct GameFlowStateChangedEvent
{
    public GameFlowStateType from;
    public GameFlowStateType to;
}

public struct HuntFlowStateChangedEvent
{
    public GameFlowStateType from;
    public GameFlowStateType to;
}
```

### `Assets/Script/GameEvent/HuntCombatFlowEvents.cs`

```cs
using UnityEngine;

public struct BulletImpactEvent
{
    public Vector3 position;
    public Vector2 normal;
    public GameObject hitObject;
    public SurfaceType surfaceType;
    public bool hitEnemy;
    public WeaponAudioProfileSO audioProfile;
}

public struct EnemyDiedEvent
{
    public GameObject enemy;
    public Vector3 position;
}

public struct PlayerDamagedEvent
{
    public GameObject player;
    public Vector3 position;
    public float damage;
}

public enum SurfaceType
{
    Default,
    Flesh,
    Metal,
    Concrete,
    Wood,
    Glass,
    Water
}
```

### `Assets/Script/GameEvent/HuntWeaponEvents.cs`

```cs
using UnityEngine;

public struct WeaponFiredEvent
{
    public GameObject owner;
    public Vector3 position;
    public Vector2 direction;
    public WeaponAudioProfileSO audioProfile;
    public bool isPlayer;
}

public struct WeaponReloadEvent
{
    public GameObject owner;
    public Vector3 position;
    public WeaponAudioProfileSO audioProfile;
    public WeaponReloadPhase phase;
}

public struct WeaponEmptyEvent
{
    public GameObject owner;
    public Vector3 position;
    public WeaponAudioProfileSO audioProfile;
    public bool isPlayer;
}

public enum WeaponReloadPhase
{
    Start,
    InsertMagazine,
    Charge,
    End
}
```
