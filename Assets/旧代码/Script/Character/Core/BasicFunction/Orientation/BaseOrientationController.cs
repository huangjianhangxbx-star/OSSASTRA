using UnityEngine;

public abstract class BaseOrientationController : IOrientationController
{
    protected Vector3 currentDirection;
    protected Transform visualTransform;
    protected CharacterDataBus dataBus;

    public virtual void Initialize(CharacterDataBus bus, Transform visualTr)
    {
        visualTransform = visualTr;
        dataBus = bus;

        if (dataBus == null)
            Debug.LogError("BaseOrientationController: dataBus 为 null!");

        if (visualTransform == null)
            Debug.LogError("BaseOrientationController: visualTransform 为 null!");
    }

    public abstract void UpdateOrientation(float deltaTime);
    public abstract void OrientMethod();

    public virtual void Update(float deltaTime) { }

    public Vector3 GetCurrentDirection() => currentDirection;

    protected void ApplyVisualRotation()
    {
        //if (visualTransform == null || dataBus == null)
        //    return;

        //// 你的旧逻辑：Vector3.left 对应 0，Vector3.right 对应 180
        //float targetAngle = dataBus.facingDirection == Vector3.left ? 0f : 180f;

        //Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);

        //// 只旋转视觉节点，不旋转 CharacterRoot
        //visualTransform.localRotation = targetRotation;
    }
}