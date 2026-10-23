using UnityEngine;

[ExecuteAlways]
public class AutoAttackGizmosDrawer : MonoBehaviour
{
    //[Header("References")]
    [SerializeField] private CharacterRoot characterRoot;
    [SerializeField] private CharacterStatsController statsController;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("Foot Point")]
    [Tooltip("如果指定了这个点，则优先用它作为索敌圈和连线起点")]
    [SerializeField] private Transform footPointOverride;

    [Tooltip("没有 FootPointOverride 时，是否使用 BoxCollider2D 底部作为脚底点")]
    [SerializeField] private bool useColliderBottomAsFootPoint = false;

    [SerializeField] private Vector3 footPointOffset = Vector3.zero;

    [Header("Draw Options")]
    [SerializeField] private bool drawSearchRange = true;
    [SerializeField] private bool drawAttackRange = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private bool drawFootPoint = true;

    [Tooltip("勾上后只有选中角色时才画")]
    [SerializeField] private bool drawOnlyWhenSelected = false;

    [Header("Colors")]
    [SerializeField] private Color searchRangeColor = new Color(1f, 0.9f, 0.1f, 0.9f);
    [SerializeField] private Color attackRangeColor = new Color(1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color targetLineColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color targetPointColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color footPointColor = Color.white;
    [SerializeField] private Color warningColor = Color.magenta;

    [Header("Debug")]
    [SerializeField] private bool logRangeWhenPlaying = false;

    private float lastLogTime;

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();

        if (statsController == null)
            statsController = GetComponent<CharacterStatsController>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<BoxCollider2D>();
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected)
            return;

        DrawGizmosInternal();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected)
            return;

        DrawGizmosInternal();
    }

    private void DrawGizmosInternal()
    {
        AutoFindReferences();

        if (characterRoot == null)
            return;

        Vector3 origin = GetFootPoint();

        if (drawFootPoint)
        {
            Gizmos.color = footPointColor;
            Gizmos.DrawWireSphere(origin, 0.12f);
        }

        float searchRange = 0f;
        float attackRange = 0f;

        if (statsController != null)
        {
            searchRange = statsController.GetStat(CharacterStats.SearchRange);
            //attackRange = statsController.GetStat(CharacterStats.AttackRange);
        }

        if (Application.isPlaying && logRangeWhenPlaying)
        {
            if (Time.realtimeSinceStartup - lastLogTime > 1f)
            {
                lastLogTime = Time.realtimeSinceStartup;
                Debug.Log(
                    $"[AutoAttackGizmosDrawer] SearchRange={searchRange}, AttackRange={attackRange}, Origin={origin}",
                    this
                );
            }
        }

        if (drawSearchRange)
        {
            if (searchRange > 0f)
            {
                Gizmos.color = searchRangeColor;
                Gizmos.DrawWireSphere(origin, searchRange);
            }
            else
            {
                Gizmos.color = warningColor;
                Gizmos.DrawWireSphere(origin, 0.35f);
            }
        }

        if (drawAttackRange)
        {
            if (attackRange > 0f)
            {
                Gizmos.color = attackRangeColor;
                Gizmos.DrawWireSphere(origin, attackRange);
            }
        }

        if (drawTargetLine)
        {
            DrawTargetLine(origin);
        }
    }

    private Vector3 GetFootPoint()
    {
        Vector3 point;

        if (footPointOverride != null)
        {
            point = footPointOverride.position;
        }
        else if (useColliderBottomAsFootPoint && bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            point = new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
        }
        else
        {
            point = transform.position;
        }

        point += footPointOffset;
        return point;
    }

    private void DrawTargetLine(Vector3 origin)
    {
        CharacterDataBus dataBus = characterRoot.characterDataBus;

        if (dataBus == null)
            return;

        if (!dataBus.hasAttackTarget)
            return;

        if (dataBus.lockedAttackTarget == null)
            return;

        if (!dataBus.lockedAttackTarget.IsValid())
            return;

        Vector3 targetPoint = dataBus.lockedAttackTarget.GetWorldPosition();

        Gizmos.color = targetLineColor;
        Gizmos.DrawLine(origin, targetPoint);

        Gizmos.color = targetPointColor;
        Gizmos.DrawWireSphere(targetPoint, 2f);
    }
}