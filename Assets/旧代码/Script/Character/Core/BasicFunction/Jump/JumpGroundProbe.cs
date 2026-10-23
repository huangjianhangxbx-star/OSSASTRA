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
