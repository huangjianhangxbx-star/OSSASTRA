using UnityEngine;
//读取角色位置与光标位置
//写 requestedFacingDirection
//写 requestedFacingSource
public class MouseOrientationController : BaseOrientationController
{
    private const float CursorDeadZoneX = 0.05f;

    public override void Update(float deltaTime)
    {
        OrientMethod();
    }

    public override void OrientMethod()
    {
        if (dataBus == null)
            return;

        Vector3 characterPosition = dataBus.characterPosition;
        Vector3 cursorWorldPosition = dataBus.mousePositionOnScreen;

        float cursorDx = cursorWorldPosition.x - characterPosition.x;

        bool hasCursorSide = Mathf.Abs(cursorDx) > CursorDeadZoneX;

        float currentSign = dataBus.facingDirection.x >= 0f ? 1f : -1f;

        float requestedSign = currentSign;
        CharacterDataBus.RequestedFacingSource source = CharacterDataBus.RequestedFacingSource.None;

        // Cursor 模式始终由光标所在的左右半平面决定朝向。
        // 瞄准和攻击输入只决定战斗姿态，不再决定光标是否拥有朝向权。
        if (hasCursorSide)
        {
            requestedSign = cursorDx > 0f ? 1f : -1f;
            source = CharacterDataBus.RequestedFacingSource.Aim;
            RecordLastWeaponFacing(requestedSign);
        }
        else
        {
            requestedSign = currentSign;
            source = CharacterDataBus.RequestedFacingSource.None;
        }

        dataBus.requestedFacingDirection = requestedSign > 0f
            ? Vector3.right
            : Vector3.left;

        dataBus.requestedFacingSource = source;
    }

    /// <summary>记录最后有效武器朝向方向</summary>
    private void RecordLastWeaponFacing(float sign)
    {
        dataBus.lastWeaponFacingDirection = sign > 0f ? Vector3.right : Vector3.left;
        dataBus.hasLastWeaponFacingDirection = true;
    }

    /// <summary>获取最后武器朝向符号，无记录时回退到 currentSign</summary>
    private float GetLastWeaponFacingSign(float fallbackSign)
    {
        if (dataBus.hasLastWeaponFacingDirection)
            return dataBus.lastWeaponFacingDirection.x >= 0f ? 1f : -1f;
        return fallbackSign;
    }

    public override void UpdateOrientation(float deltaTime)
    {
        Update(deltaTime);
    }
}
