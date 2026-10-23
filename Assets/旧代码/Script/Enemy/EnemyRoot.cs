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