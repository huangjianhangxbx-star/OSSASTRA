using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战命中检测控制器。
/// 订阅 CharacterSpineEventSystem 的 Spine 事件，
/// 在 HitOpen/HitClose 窗口期间使用 OverlapCircle 检测敌人，
/// 命中后通过 CombatEffectExecutor 执行伤害效果。
/// </summary>
[RequireComponent(typeof(CharacterSpineEventSystem))]
[RequireComponent(typeof(CharacterCombatGeometryProvider))]
public class MeleeHitController : MonoBehaviour
{
    [Header("命中检测配置")]
    [SerializeField] private float hitCheckRadius = 0.5f;
    [SerializeField] private string kickAnchorId = "KickFoot";

    [Header("命中效果")]
    [SerializeField] private MeleeHitProfileSO kickHitProfile;

    private CharacterSpineEventSystem _spineEventSystem;
    private CharacterCombatGeometryProvider _geometryProvider;
    private CharacterRoot _characterRoot;

    private bool _isHitWindowOpen;
    private readonly List<Collider2D> _hitTargets = new();
    private readonly List<Collider2D> _resultsBuffer = new();

    private void Awake()
    {
        _spineEventSystem = GetComponent<CharacterSpineEventSystem>();
        _geometryProvider = GetComponent<CharacterCombatGeometryProvider>();
        _characterRoot = GetComponent<CharacterRoot>();
    }

    private void OnEnable()
    {
        if (_spineEventSystem != null)
            _spineEventSystem.OnSpineSignal += HandleSpineSignal;
    }

    private void OnDisable()
    {
        if (_spineEventSystem != null)
            _spineEventSystem.OnSpineSignal -= HandleSpineSignal;
        _isHitWindowOpen = false;
    }

    private void HandleSpineSignal(CharacterSpineAnimationSignal signal)
    {
        if (signal.type != CharacterSpineSignalType.EventReached)
            return;

        if (signal.eventName == "HitOpen")
        {
            _isHitWindowOpen = true;
            _hitTargets.Clear();
            Debug.Log($"[MeleeHit] HitOpen | anim={signal.animationName}");
        }
        else if (signal.eventName == "HitClose")
        {
            _isHitWindowOpen = false;
            Debug.Log($"[MeleeHit] HitClose | hits={_hitTargets.Count}");
        }
    }

    private void FixedUpdate()
    {
        if (!_isHitWindowOpen)
            return;

        if (kickHitProfile == null || _characterRoot == null)
            return;

        if (!_geometryProvider.TryGetCharacterAnchor(kickAnchorId, out var anchor))
            return;

        Vector2 center = anchor.WorldPosition;

        int count = Physics2D.OverlapCircle(
            center,
            hitCheckRadius,
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = kickHitProfile.targetMask,
                useTriggers = false
            },
            _resultsBuffer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _resultsBuffer[i];
            if (col == null || _hitTargets.Contains(col))
                continue;

            if (!CombatTargetResolver.ResolveTarget(col, out IDamageable damageable))
                continue;

            if (!damageable.CanTakeDamage)
                continue;

            _hitTargets.Add(col);

            Vector2 hitPoint = col.ClosestPoint(center);

            // 攻击方向 = 从角色根位置指向敌人根位置
            // 不依赖碰撞体表面点，避免近距离时锚点在碰撞体内部导致方向错误
            Vector2 enemyPos = col.transform.position;
            Vector2 charPos = _characterRoot.transform.position;
            Vector2 attackDirection = (enemyPos - charPos).normalized;
            if (attackDirection.sqrMagnitude < 0.001f)
            {
                attackDirection = (Vector2)_characterRoot.transform.right;
            }

            Vector2 hitNormal = (center - hitPoint).normalized;
            if (hitNormal.sqrMagnitude < 0.001f)
                hitNormal = -attackDirection;

            var context = new CombatEffectContext(
                _characterRoot,
                null,
                null,
                0,
                0,
                col,
                col.gameObject,
                damageable,
                hitPoint,
                hitNormal,
                attackDirection
            );

            CombatEffectExecutor.ApplyEffects(kickHitProfile, context);

            Debug.Log($"[MeleeHit] Hit {col.gameObject.name} at {hitPoint}", col.gameObject);
        }
    }
}
