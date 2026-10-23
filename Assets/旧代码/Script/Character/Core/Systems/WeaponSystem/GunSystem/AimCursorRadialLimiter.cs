using UnityEngine;

[DefaultExecutionOrder(10000)]
public class AimCursorRadialLimiter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("受控制的光标 Transform，也就是角色身边的圆圈 Cursor")]
    [SerializeField] private Transform cursorTransform;

    [Tooltip("禁止圆圆心。通常就是角色根节点/腰部的根节点")]
    [SerializeField] private Transform centerTransform;

    [Header("Forbidden Circle")]
    [Tooltip("禁止圆的绝对圆不可进入半径")]
    [SerializeField] private float forbiddenRadius = 2.5f;

    [Tooltip("离开禁止状态的光标半径，必须大于 Forbidden Radius 一点")]
    [SerializeField] private float releaseExtraRadius = 0.15f;

    [Header("Arc Slide")]
    [Tooltip("沿圆周滑动的角速度，越大越紧跟，越小越平滑")]
    [SerializeField] private float maxAngularSpeed = 720f;

    [Tooltip("光标几乎压住圆心时，沿用上一个方向，防止光标瞬间翻转")]
    [SerializeField] private float centerDeadZone = 0.05f;

    [Tooltip("光标刚进入禁止圆心。无法判断走上弧还是下弧时，true 默认走上半弧，false 默认走下半弧")]
    [SerializeField] private bool preferUpperArc = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private bool isInsideForbiddenZone;

    private Vector2 lastAllowedDirection = Vector2.right;
    private Vector2 lastRawCursorWorld;
    private bool hasLastRawCursorWorld;

    // +1 逆时针，-1 顺时针
    private int lockedArcSign = 1;

    private void LateUpdate()
    {
        ApplyLimit();
    }

    private void ApplyLimit()
    {
        if (cursorTransform == null)
            return;

        if (centerTransform == null)
            return;

        Vector3 rawCursorWorld3 = cursorTransform.position;
        Vector2 rawCursorWorld = rawCursorWorld3;

        Vector2 centerWorld = centerTransform.position;
        Vector2 delta = rawCursorWorld - centerWorld;

        float distance = delta.magnitude;
        float releaseRadius = forbiddenRadius + Mathf.Max(0f, releaseExtraRadius);

        // 在合法区域外，直接放行。
        if (!isInsideForbiddenZone && distance >= forbiddenRadius)
        {
            if (distance > 0.0001f)
                lastAllowedDirection = delta / distance;

            RememberRawCursor(rawCursorWorld);
            return;
        }

        // 已经进入禁止圆状态，只有超出 releaseRadius 才释放。
        if (isInsideForbiddenZone && distance >= releaseRadius)
        {
            isInsideForbiddenZone = false;

            if (distance > 0.0001f)
                lastAllowedDirection = delta / distance;

            RememberRawCursor(rawCursorWorld);
            return;
        }

        // 进入禁止圆的一瞬间，锁定方向。
        if (!isInsideForbiddenZone)
        {
            isInsideForbiddenZone = true;
            lockedArcSign = DecideArcSign(rawCursorWorld, centerWorld);
        }

        Vector2 rawDirection;

        if (distance > centerDeadZone)
        {
            rawDirection = delta / distance;
        }
        else
        {
            rawDirection = lastAllowedDirection;
        }

        if (rawDirection.sqrMagnitude < 0.0001f)
            rawDirection = Vector2.right;

        rawDirection.Normalize();

        Vector2 newDirection = MoveDirectionOnCircle(lastAllowedDirection, rawDirection);

        if (newDirection.sqrMagnitude < 0.0001f)
            newDirection = lastAllowedDirection;

        newDirection.Normalize();

        lastAllowedDirection = newDirection;

        Vector2 limitedCursorWorld = centerWorld + lastAllowedDirection * forbiddenRadius;

        cursorTransform.position = new Vector3(
            limitedCursorWorld.x,
            limitedCursorWorld.y,
            rawCursorWorld3.z
        );

        RememberRawCursor(rawCursorWorld);
    }

    private Vector2 MoveDirectionOnCircle(Vector2 currentDirection, Vector2 targetDirection)
    {
        if (currentDirection.sqrMagnitude < 0.0001f)
            currentDirection = Vector2.right;

        if (targetDirection.sqrMagnitude < 0.0001f)
            targetDirection = currentDirection;

        currentDirection.Normalize();
        targetDirection.Normalize();

        float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;

        float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);

        float sign;

        // 当接近角色正后方移过去时，target 可能接近 180 度。
        // 此时强制锁定逆时针或顺时针，沿圆边缘走，防止光标瞬间翻转。
        if (Mathf.Abs(deltaAngle) > 170f)
        {
            sign = lockedArcSign;
        }
        else if (Mathf.Abs(deltaAngle) < 0.01f)
        {
            sign = lockedArcSign;
        }
        else
        {
            sign = Mathf.Sign(deltaAngle);
        }

        float maxStep = maxAngularSpeed * Time.deltaTime;
        float step = Mathf.Min(Mathf.Abs(deltaAngle), maxStep) * sign;

        float newAngle = currentAngle + step;
        float rad = newAngle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private int DecideArcSign(Vector2 rawCursorWorld, Vector2 centerWorld)
    {
        Vector2 currentDir = lastAllowedDirection;

        if (currentDir.sqrMagnitude < 0.0001f)
            currentDir = Vector2.right;

        currentDir.Normalize();

        Vector2 rawDir = rawCursorWorld - centerWorld;

        if (rawDir.sqrMagnitude > centerDeadZone * centerDeadZone)
        {
            rawDir.Normalize();

            float cross = Cross(currentDir, rawDir);

            if (Mathf.Abs(cross) > 0.001f)
                return cross > 0f ? 1 : -1;
        }

        // 如果 rawDir 判断不出来，就看光标移动方向在当前圆周切线上的分量。
        if (hasLastRawCursorWorld)
        {
            Vector2 rawMove = rawCursorWorld - lastRawCursorWorld;

            if (rawMove.sqrMagnitude > 0.000001f)
            {
                Vector2 counterClockwiseTangent = new Vector2(-currentDir.y, currentDir.x);
                float tangentAmount = Vector2.Dot(rawMove, counterClockwiseTangent);

                if (Mathf.Abs(tangentAmount) > 0.001f)
                    return tangentAmount > 0f ? 1 : -1;
            }
        }

        // 穷到底，默认走上弧或下弧。
        return preferUpperArc ? 1 : -1;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void RememberRawCursor(Vector2 rawCursorWorld)
    {
        lastRawCursorWorld = rawCursorWorld;
        hasLastRawCursorWorld = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (centerTransform == null)
            return;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 1f);
        Gizmos.DrawWireSphere(centerTransform.position, forbiddenRadius);

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(centerTransform.position, forbiddenRadius + Mathf.Max(0f, releaseExtraRadius));
    }
}
