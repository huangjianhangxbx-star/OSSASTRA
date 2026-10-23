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
