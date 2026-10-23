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