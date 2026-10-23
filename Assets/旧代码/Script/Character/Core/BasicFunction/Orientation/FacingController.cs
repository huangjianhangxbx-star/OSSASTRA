using UnityEngine;
//读取 requestedFacingSource
//判断是否需要即时翻转，还是走移动方向确认
//写：
//currentFacing
//targetFacing
//isFacingChanging
//facingDirection
//moveSpeed
public class FacingController
{
    private readonly Transform visualFlipRoot;
    private readonly CharacterDataBus dataBus;
    public bool IsMoveTurnPending { get; private set; }//是否有移动翻转 pending
    private readonly float moveTurnMovementLockDuration;


    private int stableSign = 1;//当前稳定朝向
    private int pendingSign = 1;//目标/待确认朝向

    public int StableSign => stableSign;
    public int PendingSign => pendingSign;

    private float pendingTimer;
    private float flipCooldownTimer;

    private int lastAppliedSign = 999;

    private readonly float moveFlipConfirmTime;
    private readonly float flipCooldownTime;
    private readonly float moveFlipConfirmTimeAfterWeapon;
    private readonly float weaponMoveTurnSlowWindow;



    public FacingController(
        CharacterDataBus dataBus,
        Transform visualFlipRoot,
        float moveFlipConfirmTime,
        float moveFlipConfirmTimeAfterWeapon,
        float weaponMoveTurnSlowWindow,
        float flipCooldownTime,
        float moveTurnMovementLockDuration)
    {
        this.dataBus = dataBus;
        this.visualFlipRoot = visualFlipRoot;
        this.moveFlipConfirmTime = moveFlipConfirmTime;
        this.moveFlipConfirmTimeAfterWeapon = moveFlipConfirmTimeAfterWeapon;
        this.weaponMoveTurnSlowWindow = weaponMoveTurnSlowWindow;
        this.flipCooldownTime = flipCooldownTime;
        this.moveTurnMovementLockDuration = moveTurnMovementLockDuration;

        stableSign = GetSign(dataBus.facingDirection);
        pendingSign = stableSign;

        dataBus.facingDirection = stableSign > 0 ? Vector3.right : Vector3.left;
        dataBus.requestedFacingDirection = dataBus.facingDirection;
        dataBus.requestedFacingSource = CharacterDataBus.RequestedFacingSource.None;

        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, true);
    }



    public void Tick(float deltaTime)
    {
        if (dataBus == null || visualFlipRoot == null)
            return;

        if (flipCooldownTimer > 0f)
            flipCooldownTimer -= deltaTime;

        int requestedSign = GetSign(dataBus.requestedFacingDirection);
        CharacterDataBus.RequestedFacingSource source = dataBus.requestedFacingSource;

        switch (source)
        {
            case CharacterDataBus.RequestedFacingSource.Attack:
            case CharacterDataBus.RequestedFacingSource.Aim:
            case CharacterDataBus.RequestedFacingSource.Target:
                TickInstantFacing(requestedSign);
                break;

            case CharacterDataBus.RequestedFacingSource.Move:
                TickMoveFacing(requestedSign, deltaTime);
                break;

            case CharacterDataBus.RequestedFacingSource.None:
            default:
                TickKeepFacing();
                break;
        }
    }

    private void TickInstantFacing(int requestedSign)
    {
        /*
         * 攻击 / 瞄准 / 锁定目标：
         * 这里是强制改变朝向，不走 Move 的延迟方向逻辑。
         */
        IsMoveTurnPending = false;

        AcceptFacingImmediately(requestedSign);

        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);
    }

    private void TickMoveFacing(int requestedSign, float deltaTime)
    {
        /*
         * 移动方向和当前稳定朝向一致时，
         * 不需要翻转，也不用进入 pending。
         */
        if (requestedSign == stableSign)
        {
            IsMoveTurnPending = false;
            ClearPending();

            WriteStableFacing();
            WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

            ApplyVisualFacing(stableSign, false);
            return;
        }

        /*
         * 冷却期：
         * 刚发生翻转，不允许立即翻转
         */
        if (flipCooldownTimer > 0f)
        {
            IsMoveTurnPending = true;

            WriteStableFacing();

            // 当前朝向是 stableSign，目标是 requestedSign
            WriteFacingRuntimeToDataBus(stableSign, requestedSign, true);

            ApplyVisualFacing(stableSign, false);
            return;
        }

        /*
         * 方向与稳定朝向不同，进入移动方向确认期。
         */
        IsMoveTurnPending = true;

        if (pendingSign != requestedSign)
        {
            pendingSign = requestedSign;
            pendingTimer = 0f;
            dataBus.facingChangeId++;

            BeginMoveTurnMovementLock();

        }

        pendingTimer += deltaTime;

        /*
         * 注意：
         * 确认期间，当前朝向保持 stableSign，
         * 目标朝向是 pendingSign，
         * 这是为了动画系统需要播放 TurnAround 过渡动画。
         */
        WriteFacingRuntimeToDataBus(stableSign, pendingSign, true);

        float confirmTime = GetCurrentMoveFlipConfirmTime();

        if (pendingTimer >= confirmTime)
        {
            AcceptFacingImmediately(requestedSign);

            WriteFacingRuntimeToDataBus(stableSign, stableSign, false);
            return;
        }

        /*
         * 尚未确认前，保持稳定朝向
         */
        WriteStableFacing();
        ApplyVisualFacing(stableSign, false);
    }

    private float GetCurrentMoveFlipConfirmTime()
    {
        if (dataBus == null)
            return moveFlipConfirmTime;

        bool recentlyUsedWeaponFacing =
            Time.time - dataBus.lastWeaponFacingActivityTime <= weaponMoveTurnSlowWindow;

        if (recentlyUsedWeaponFacing)
            return moveFlipConfirmTimeAfterWeapon;

        return moveFlipConfirmTime;
    }

    private void TickKeepFacing()
    {
        /*
         * 无明确输入时，保持稳定朝向
         */
        IsMoveTurnPending = false;

        ClearPending();

        WriteStableFacing();
        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, false);
    }

    private void AcceptFacingImmediately(int sign)
    {
        if (sign == 0)
            return;

        IsMoveTurnPending = false;

        bool changed = stableSign != sign;

        stableSign = sign;
        pendingSign = sign;
        pendingTimer = 0f;

        if (changed)
            flipCooldownTimer = flipCooldownTime;

        WriteStableFacing();
        WriteFacingRuntimeToDataBus(stableSign, stableSign, false);

        ApplyVisualFacing(stableSign, false);
    }

    private void ClearPending()
    {
        pendingSign = stableSign;
        pendingTimer = 0f;
    }

    private void WriteStableFacing()
    {
        dataBus.facingDirection = stableSign > 0
            ? Vector3.right
            : Vector3.left;
    }

    private int GetSign(Vector3 direction)
    {
        return direction.x >= 0f ? 1 : -1;
    }

    private void ApplyVisualFacing(int sign, bool force)
    {
        if (visualFlipRoot == null)
            return;

        if (!force && lastAppliedSign == sign)
            return;

        lastAppliedSign = sign;

        Vector3 scale = visualFlipRoot.localScale;

        float absX = Mathf.Abs(scale.x);
        float absY = Mathf.Abs(scale.y);
        float absZ = Mathf.Abs(scale.z);

        /*
         * 翻转成功后：
         * left  -> Y = 0
         * right -> Y = 180
         *
         * 说明：模型默认朝向
         *
         * 因此：
         * sign < 0 左：scale.x = 正
         * sign > 0 右：scale.x = 负
         */
        scale.x = sign > 0 ? -absX : absX;
        scale.y = absY;
        scale.z = absZ;

        visualFlipRoot.localScale = scale;

        // 骨节点只用做 scale 翻转，不做 Y 轴旋转
        visualFlipRoot.localRotation = Quaternion.identity;
    }


    public void ReapplyVisualOnly()
    {
        ApplyVisualFacing(stableSign, true);
    }

    private Facing SignToFacing(int sign)
    {
        return sign >= 0 ? Facing.Right : Facing.Left;
    }


    private void WriteFacingRuntimeToDataBus(int currentSign, int targetSign, bool isChanging)
    {
        if (dataBus == null)
            return;

        dataBus.currentFacing = SignToFacing(currentSign);
        dataBus.targetFacing = SignToFacing(targetSign);
        dataBus.isFacingChanging = isChanging;
    }

    private void BeginMoveTurnMovementLock()
    {
        if (dataBus == null)
            return;

        dataBus.moveTurnMovementLockId++;
        dataBus.moveTurnMovementLockUntilTime = Mathf.Max(
            dataBus.moveTurnMovementLockUntilTime,
            Time.time + moveTurnMovementLockDuration
        );
    }
}
