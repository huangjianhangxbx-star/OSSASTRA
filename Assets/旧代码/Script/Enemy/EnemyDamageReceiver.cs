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
