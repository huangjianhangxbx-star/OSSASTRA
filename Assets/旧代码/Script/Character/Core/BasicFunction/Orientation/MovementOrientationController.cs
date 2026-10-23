using UnityEngine;

public sealed class MovementOrientationController : BaseOrientationController
{
    private const float MoveInputDeadZoneX = 0.1f;

    public override void Update(float deltaTime)
    {
        OrientMethod();
    }

    public override void OrientMethod()
    {
        if (dataBus == null)
            return;

        float moveX = dataBus.moveInput.x;
        if (Mathf.Abs(moveX) <= MoveInputDeadZoneX)
        {
            dataBus.requestedFacingDirection = dataBus.facingDirection;
            dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.None;
            return;
        }

        dataBus.requestedFacingDirection = moveX > 0f
            ? Vector3.right
            : Vector3.left;
        dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.Move;
    }

    public override void UpdateOrientation(float deltaTime)
    {
        Update(deltaTime);
    }
}
