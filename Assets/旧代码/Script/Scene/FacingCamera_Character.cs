using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacingCamera_Character : MonoBehaviour
{
    [Header("倾斜设置")]
    [SerializeField] private float tiltAngle = -45f;  // X轴倾斜角度

    // ================= 旋转存储 =================
    // 关键：使用两个独立的四元数存储不同的旋转分量
    private Quaternion tiltRotation;  // 存储X轴倾斜（摄像机视角）
    private Quaternion turnRotation;  // 存储Y轴转身（左右方向）

    // ================= 初始化 =================
    void Start()
    {
        // 初始化倾斜旋转：只绕X轴旋转（俯仰）
        tiltRotation = Quaternion.Euler(tiltAngle, 0, 0);

        // 初始化转身旋转：初始面向右边（Y轴0度）
        turnRotation = Quaternion.identity;  // identity = 无旋转

        // 应用初始旋转
        UpdateRotation();
    }

    // ================= 核心方法：更新旋转 =================
    /// <summary>
    /// 更新最终旋转（关键！避免万向锁的核心方法）
    /// 原理：使用四元数乘法组合多个旋转，而不是欧拉角
    /// </summary>
    private void UpdateRotation()
    {
        // ====== 关键原理说明 ======
        // transform.rotation = tiltRotation * turnRotation;
        // 
        // 四元数乘法顺序：从右往左应用
        // 1. 先应用 turnRotation（Y轴转身）
        // 2. 再应用 tiltRotation（X轴倾斜）
        // 
        // 视觉顺序：
        //   角色先转身面向目标方向（Y轴旋转）
        //   然后摄像机让角色倾斜（X轴旋转）
        //   
        // 为什么这样能避免万向锁？
        // - 欧拉角（Euler）有旋转顺序问题（Unity默认是Z→X→Y）
        // - 当X轴接近90度时，Z轴和Y轴会重合，导致万向锁
        // - 四元数没有顺序问题，直接描述3D空间中的旋转

        transform.rotation = tiltRotation * turnRotation;
    }

    // ================= 外部调用接口 =================
    /// <summary>
    /// 设置角色面向方向
    /// 注意：这里的方向是在倾斜前的2D平面上的方向
    /// </summary>
    public void SetFacingDirection(Vector3 direction)
    {
        // 计算Y轴转身角度
        // 重要：这里的方向是在水平2D平面上的方向判断
        float yAngle = direction == Vector3.left ? 180f : 0f;

        // 设置转身旋转（只修改Y轴）
        turnRotation = Quaternion.Euler(0, yAngle, 0);

        // 更新最终旋转
        UpdateRotation();
    }

    /// <summary>
    /// 直接控制转身方向
    /// </summary>
    public void TurnCharacter(bool faceRight)
    {
        float yAngle = faceRight ? 0f : 180f;
        turnRotation = Quaternion.Euler(0, yAngle, 0);
        UpdateRotation();
    }

    /// <summary>
    /// 设置倾斜角度
    /// </summary>
    public void SetTiltAngle(float angle)
    {
        tiltAngle = angle;
        tiltRotation = Quaternion.Euler(tiltAngle, 0, 0);
        UpdateRotation();
    }

    /// <summary>
    /// 跟随摄像机X轴旋转
    /// </summary>
    public void FollowCameraX()
    {
        float cameraX = Camera.main.transform.rotation.eulerAngles.x;
        tiltRotation = Quaternion.Euler(cameraX, 0, 0);
        UpdateRotation();
    }
}
