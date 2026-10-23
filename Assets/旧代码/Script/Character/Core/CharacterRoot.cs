using MoreMountains.Feel;
using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

[DefaultExecutionOrder(-500)]
public class CharacterRoot : MonoBehaviour
{
    public CharacterControlAuthority controlAuthority { get; } =
        new CharacterControlAuthority();

    //    在 Unity 开发中，“初始化防御（Initialization Guard）”不仅是一个好习惯，更是构建复杂系统（如你现在的回合制战斗系统）时的底层安全准则。

    //简单的回答是：是的，凡是涉及到“生成新实例（new）”或“绑定事件（+=）”的初始化逻辑，都必须进行防御。
    //以下是为什么要这么做的三个深度原因，以及未来你该如何避坑：
    //1. 解决 Unity 生命周期的“不可控性”
    //Unity 的 Awake 和 Start 方法是由系统调用的。当你动态生成（Instantiate）一个物体时：
    //同步执行：Awake 会在 Instantiate 返回前立即执行。
    //异步冲突：你的脚本（如 BattleSpawner）通常在 Instantiate 之后紧接着调用 Initialize()。
    //后果：如果你在 Awake 里 new 了一次状态机，在 Initialize 里又 new 了一次，你就亲手制造了一个“状态黑洞”。
    //外界赋予第一个实例的所有数据（比如 isForceActive = true），都会在第二个实例生成时被瞬间抹除。
    //2. 保证操作的“幂等性”（Idempotency）
    //在软件工程中，幂等性意味着：无论你调用一个方法 1 次还是 100 次，结果都应该是一样的，不会产生副作用。
    //如果你的初始化逻辑不防重：
    //重复执行 new：会导致旧引用丢失、内存泄漏或逻辑断裂
    //重复执行 events += Func：会导致事件触发时跑两遍（比如一个技能放出两个火球、结算两次伤害）。
    private bool _isInitialized = false;//防止重复初始化的标志位，虽然目前的设计里不太可能，但以防万一，还是加上了这个保险措施。


    private bool _isBootstrapped = false;
    private bool _isGameplayInitialized = false;



    //遵循良好的设计原则：组合优于继承、依赖注入（通过 Initialize(this)）、防御性编程（??= 和 AutoGetComponent）。



    //[Header("Core References")]
    //=============将所有需要挂载的核心脚本作为属性公开=============
    //自动属性{  get; private set; }：它无法直接序列化（保存到磁盘）这种带 { get; set; } 的属性。
    //强制存盘：[field: SerializeField] 告诉 Unity：“虽然这是一个属性，但请把那个隐藏变量序列化（保存）到硬盘里。”
    //纯数据类（DataBus）：千万别序列化！：因为它每次运行都是 new 出来的，绝对不要加 [field: SerializeField]。
    //不要删 { get; private set; }：它是最好的保护，防止外部脚本乱改 Root 里的组件。
    //补全 GetComponent：对于那些没有 [field: SerializeField] 的属性，必须在 Awake/Initialize 里写 GetComponent。
    //DataLog/DataBus：去掉那里的 [field: SerializeField]，逻辑生成的东西不需要序列化。
    #region 数据
    public CharacterDataBus characterDataBus { get; private set; }//N
    [field: SerializeField] public CharacterStatsController characterStatsController { get; private set; }//N
    #endregion
    #region 输入
    [field: SerializeField] public InputHandler inputHandler{ get; private set; }//update、lateupdate
    #endregion
    #region 动作
    [field: SerializeField] public StateArbitrator stateArbitrator{ get; private set; }//update、fixedupdate
    [field: SerializeField] public OrientationSystem orientationSystem{ get; private set; }//update、lateupdate
    [field: SerializeField] public CharacterAnimationConfigManager characterAnimationConfigManager { get; private set; }//N
    [field: SerializeField] public AnimationHandleSystem animationHandleSystem{ get; private set; }//N

    #endregion
    #region 皮肤
    [field: SerializeField] public WardrobeManager wardrobeManager { get; private set; }//N
    [field: SerializeField] public SpineSkinApplicator spineSkinApplicator { get; private set; }//N
    #endregion
    #region 战斗
    [field: SerializeField] public AimCursorSystem aimCursorSystem { get; private set; }//update
    [field: SerializeField] public CharacterAttackController characterAttackController { get; private set; }//N

    [field: SerializeField] public AimIKController aimIKController {get; private set; }//lateupdate
    [field: SerializeField] public LockTargetIKController lockTargetIKController { get; private set; }//lateupdate
    [field: SerializeField] public HunterGunCursorFacingController hunterGunCursorFacingController { get; private set; }//lateupdate
    [field: SerializeField] public HunterGunBonePositionMatcher hunterGunBonePositionMatcher { get; private set; }//update、fixedupdate
    [field: SerializeField] public CharacterEquipmentController characterEquipmentController { get; private set; }//N
    [field: SerializeField] public GunSystemController gunSystemController { get; private set; }//N

    [field: SerializeField] public CharacterSpineEventSystem characterSpineEventSystem { get; private set; }//N

    [field: SerializeField] public CharacterWeaponInventory characterWeaponInventory{ get; private set; }//N


    #endregion
    #region 背包系统
    [field: SerializeField] public CharacterPackageSystem characterPackageSystem { get; private set; }
    [field: SerializeField] public CharacterPickupController characterPickupController { get; private set; }
    [field: SerializeField] public CharacterUIInputRouter characterUIInputRouter { get; private set; }
    #endregion

    #region 视觉
    //[Header("Spine - Hound 角色本体")]
    [field: SerializeField] public SkeletonAnimation houndSkeletonAnimation { get; private set; }//N
    //[Header("Spine - HunterGun 枪械承载骨架")]
    [field: SerializeField] public SkeletonAnimation hunterGunSkeletonAnimation { get; private set; }//N
    public Spine.Skeleton houndSkeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;
    public Spine.Skeleton hunterGunSkeleton =>
        hunterGunSkeletonAnimation != null ? hunterGunSkeletonAnimation.Skeleton : null;
    // 临时兼容旧代码：旧的 characterRoot.skeletonAnimation 默认指向 Hound
    public SkeletonAnimation skeletonAnimation => houndSkeletonAnimation;
    public Spine.Skeleton skeleton =>
        houndSkeletonAnimation != null ? houndSkeletonAnimation.Skeleton : null;

    [field: SerializeField] public CharacterAnimationComposer characterAnimationComposer { get; private set; }//update

    [field: SerializeField] public GunHandOcclusionByAimAngle gunHandOcclusionByAimAngle { get; private set; }//LateUpdate

    #endregion
    #region Unity物理
    [field: SerializeField] public CharacterMovementMode movementMode { get; private set; } = CharacterMovementMode.TopDown;
    [field: SerializeField] public Rigidbody2D rigidBody2d { get; private set; }//N
    [field: SerializeField] public BoxCollider2D boxCollider2d { get; private set; }//N
    [field: SerializeField] public JumpConfigSO jumpConfig { get; private set; }
    [field: SerializeField] public LayerMask groundLayerMask { get; private set; } = 1 << 8; // 默认 Enemy 层，需在 Inspector 配置为地面层
    //public Transform CachedTransform { get; private set; }
    //Unity 里，所有的 GameObject 在诞生的那一刻，就一定且必须自带一个 Transform 组件。
    //你不能删除 Transform,不能给一个物体挂载第二个 Transform。
    //GetComponent<Transform>() 就永远能拿到值，永远不会走到 AddComponent 那一步。
    //在任何 MonoBehaviour 脚本里，直接用小写的 transform 即可。不需要定义，不需要 GetComponent，它是 Unity 送给你的“入职大礼包”。

    #endregion

    private void Reset() // 当脚本被挂载到物体上，或者点击组件右上角的 Reset 时触发
    {
        AutoGetComponent();
    }

    //private T GetOrAddComponent<T>() where T : Component//适合“这个组件必定应该挂在当前物体上，而且没有任何 Inspector 配置”的组件。
    //{
    //    T comp = GetComponent<T>();
    //    if (comp == null) comp = gameObject.AddComponent<T>();
    //    return comp;
    //}
    //=============统一在root初始化=============
    private void Awake()
    {
        BootstrapOnly();
    }

    private void Update()
    {
        if (!_isGameplayInitialized)
            return;
        float dt = Time.deltaTime;
        // 1. 输入先更新
        TickUpdate(inputHandler, dt);

        // 2. 光标位置先更新，否则 Orientation 用到的 mousePositionOnScreen 可能是旧的
        TickUpdate(aimCursorSystem, dt);

        // 3. 攻击/瞄准状态先更新，因为 Orientation 要读 attackPressed / aimHeld
        TickUpdate(characterAttackController, dt);

        // 枪械内部 burst / cooldown 更新
        TickUpdate(gunSystemController, dt);

        // 4. 朝向必须早于 StateArbitrator
        //    否则 MovementStateMachine 会基于旧 facing 算 Jog_Back，下一帧又变 Jog
        TickUpdate(orientationSystem, dt);

        // 5. 状态机此时读取到的是本帧稳定后的 facing
        TickUpdate(stateArbitrator, dt);

        // 6. 动画 Composer 本身主要处理过期 oneShot
        TickUpdate(characterAnimationComposer, dt);

        // 7. 装备等后置
        TickUpdate(characterEquipmentController, dt);

        // AnimationHandleSystem 是事件驱动的，不需要 TickUpdate
        // TickUpdate(animationHandleSystem, dt);
    }
    private void LateUpdate()
    {
        if (!_isGameplayInitialized)
            return;

        if (!_isGameplayInitialized)
            return;

        float dt = Time.deltaTime;

        TickLateUpdate(hunterGunBonePositionMatcher, dt);
        TickLateUpdate(hunterGunCursorFacingController, dt);

        TickLateUpdate(aimIKController, dt);
        TickLateUpdate(lockTargetIKController, dt);
        // 枪械朝向和 IK 都完成后，再决定手部 Separator 是否隐藏。
        TickLateUpdate(gunHandOcclusionByAimAngle, dt);
        // 最后压视觉朝向
        TickLateUpdate(orientationSystem, dt);

        // 最后清理本帧输入标记
        // 必须放在所有 Update / LateUpdate 逻辑之后
        TickLateUpdate(inputHandler, dt);

    }

    private void FixedUpdate()
    {
        if (!_isGameplayInitialized)
            return;

        float fdt = Time.fixedDeltaTime;
        TickFixedUpdate(stateArbitrator, fdt);
    }

    public void BootstrapOnly()//第一步为准备角色数据外壳
    {
        if (_isBootstrapped) return;
        AutoGetComponent();
        characterDataBus = new CharacterDataBus();
        _isBootstrapped = true;

        // 角色(Render_Character=22)忽略与 Enemy(8) 的物理碰撞
        // 伤害由 TakeDamage 系统处理，不需要物理碰撞阻挡
        Physics2D.IgnoreLayerCollision(22, 8, true);

        Debug.Log("[CharacterRoot] BootstrapOnly 完成");
    }

    public void InitializeWithSO(CharacterSO so)
    {
        //先 LoadFromSO
        //先换 Spine SkeletonDataAsset
        //再初始化依赖 Skeleton 的系统
        //最后加载动画映射 / 技能
        BootstrapOnly();

        if (_isGameplayInitialized)
        {
            Debug.LogWarning("[CharacterRoot] InitializeWithSO 重复调用，已忽略");
            return;
        }

        if (so == null)
        {
            Debug.LogError("[CharacterRoot] InitializeWithSO 失败：so == null");
            return;
        }

        // 1. 加载数据
        characterDataBus.LoadFromSO(so);

        Debug.Log($"[CharacterRoot] 已加载角色SO: {so.name}");
        Debug.Log($"[CharacterRoot] statProfile = {characterDataBus.statProfile}");

        // 2. 先应用 Hound 视觉骨架
        ApplySkeletonFromSO(so);

        // 3. 再初始化依赖 root / dataBus / skeleton 的子系统
        InitializeGameplaySubsystems();

        // 4. 再加载动画、技能、武器覆盖等配置
        ApplyGameplayConfigFromSO(so);

        _isGameplayInitialized = true;

        Debug.Log("[CharacterRoot] InitializeWithSO 完成");
    }



    private void ApplySkeletonFromSO(CharacterSO so)
    {
        if (so == null)
            return;

        if (so.chracterModel != null && houndSkeletonAnimation != null)
        {
            houndSkeletonAnimation.skeletonDataAsset = so.chracterModel;
            houndSkeletonAnimation.Initialize(true);
        }

        // HunterGun 目前是 prefab 固定骨架，不从 CharacterSO 加载
    }

    private void ApplyGameplayConfigFromSO(CharacterSO so)
    {
        if (so == null)
            return;

        if (characterAnimationConfigManager != null)
        {
            characterAnimationConfigManager.LoadCharacterBasicAnimation();
            characterAnimationConfigManager.RefreshWeaponAnimationOverride();
        }


        //if (lockTargetIKController != null)
        //{
        //    lockTargetIKController.ForceRefresh();
        //}

    }









    private void InitializeGameplaySubsystems()
    {
        // 1. 先初始化基础数据相关
        if (characterStatsController != null)
            characterStatsController.Initialize(this);

        // 2. 动画相关先于状态机
        if (characterAnimationConfigManager != null)
            characterAnimationConfigManager.Initialize(this);

        if (characterAnimationComposer != null)
            characterAnimationComposer.Initialize(this);

        if (animationHandleSystem != null)
            animationHandleSystem.Initialize(this);

        if (characterAttackController != null)
            characterAttackController.Initialize(this);

        // 3. 输入 / 朝向 / 战斗辅助
        if (inputHandler != null)
            inputHandler.Initialize(this);

        if (orientationSystem != null)
            orientationSystem.Initialize(this);

        if (aimCursorSystem != null)
            aimCursorSystem.Initialize(this);

        if (hunterGunBonePositionMatcher != null)
            hunterGunBonePositionMatcher.Initialize(this);

        if (hunterGunCursorFacingController != null)
            hunterGunCursorFacingController.Initialize(this);

        if (aimIKController != null)
            aimIKController.Initialize(this);

        if (lockTargetIKController != null)
            lockTargetIKController.Initialize(this);

        if (gunHandOcclusionByAimAngle != null)
            gunHandOcclusionByAimAngle.Initialize(this);

        if (gunSystemController != null)//在状态机之前，装备系统之前
            gunSystemController.Initialize(this);

        if (characterPackageSystem != null)
        {
            characterPackageSystem.Initialize(this);
            characterPackageSystem.InitializeNewGame();
        }

        if (characterWeaponInventory != null)
            characterWeaponInventory.Initialize(this);

        if (characterPickupController != null)
            characterPickupController.Initialize(this);

        if (characterUIInputRouter != null)
            characterUIInputRouter.Initialize(this);

        if (characterEquipmentController != null)
            characterEquipmentController.Initialize(this);

        if (wardrobeManager != null)
            wardrobeManager.Initialize(this);

        if (characterSpineEventSystem != null)
            characterSpineEventSystem.Initialize(this);

        // 4. 最后状态机
        if (stateArbitrator != null)
            stateArbitrator.Initialize(this);


    }


    public void AutoGetComponent()
    {
        #region 数据
        if (characterStatsController == null)
            characterStatsController = GetComponent<CharacterStatsController>();
        #endregion

        #region 输入
        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();
        #endregion

        #region 动作
        if (stateArbitrator == null)
            stateArbitrator = GetComponent<StateArbitrator>();

        if (orientationSystem == null)
            orientationSystem = GetComponent<OrientationSystem>();

        if (characterAnimationConfigManager == null)
            characterAnimationConfigManager = GetComponent<CharacterAnimationConfigManager>();

        if (characterAnimationComposer == null)
            characterAnimationComposer = GetComponent<CharacterAnimationComposer>();

        if (animationHandleSystem == null)
            animationHandleSystem = GetComponent<AnimationHandleSystem>();


        if (characterSpineEventSystem == null)
            characterSpineEventSystem = GetComponent<CharacterSpineEventSystem>();
        #endregion

        #region 皮肤
        if (wardrobeManager == null)
            wardrobeManager = GetComponent<WardrobeManager>();

        if (spineSkinApplicator == null)
            spineSkinApplicator = GetComponent<SpineSkinApplicator>();
        #endregion

        #region 战斗
        if (aimCursorSystem == null)
            aimCursorSystem = GetComponent<AimCursorSystem>();

        if(characterAttackController == null)
            characterAttackController = GetComponent<CharacterAttackController>();

        if (lockTargetIKController == null)
            lockTargetIKController = GetComponent<LockTargetIKController>();


        if (aimIKController == null)
            aimIKController = GetComponent<AimIKController>();

        if (characterEquipmentController == null)
            characterEquipmentController = GetComponent<CharacterEquipmentController>();

        if (hunterGunCursorFacingController == null)
            hunterGunCursorFacingController = GetComponentInChildren<HunterGunCursorFacingController>(true);

        if (hunterGunBonePositionMatcher == null)
            hunterGunBonePositionMatcher = GetComponentInChildren<HunterGunBonePositionMatcher>(true);
        
        if (gunHandOcclusionByAimAngle == null)
            gunHandOcclusionByAimAngle = GetComponentInChildren<GunHandOcclusionByAimAngle>(true);

        if (gunSystemController == null)
            gunSystemController = GetComponentInChildren<GunSystemController>(true);

        if(characterWeaponInventory == null)
            characterWeaponInventory = GetComponentInChildren<CharacterWeaponInventory>(true);

        if (characterPackageSystem == null)
            characterPackageSystem = GetComponentInChildren<CharacterPackageSystem>(true);

        if (characterPickupController == null)
            characterPickupController = GetComponentInChildren<CharacterPickupController>(true);

        if (characterUIInputRouter == null)
            characterUIInputRouter = GetComponentInChildren<CharacterUIInputRouter>(true);


        #endregion

        #region Spine组件
        //更专业的做法是手动把角色本体的 SkeletonAnimation 和枪械承载骨架的 SkeletonAnimation 分别拖到 Inspector 上的 houndSkeletonAnimation 和 hunterGunSkeletonAnimation 里。
        //不要长期依赖名字查找。

        if (houndSkeletonAnimation == null || hunterGunSkeletonAnimation == null)
        {
            SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);

            foreach (SkeletonAnimation skel in skeletonAnimations)
            {
                if (skel == null)
                    continue;

                string goName = skel.gameObject.name;

                if (houndSkeletonAnimation == null)
                {
                    if (goName.Contains("Hound") || goName.Contains("VisualFlipRoot"))
                    {
                        houndSkeletonAnimation = skel;
                    }
                }

                if (hunterGunSkeletonAnimation == null)
                {
                    if (goName.Contains("HunterGun"))
                    {
                        hunterGunSkeletonAnimation = skel;
                    }
                }
            }

            // 兜底：如果只有一个，默认当 Hound
            if (houndSkeletonAnimation == null && skeletonAnimations.Length > 0)
                houndSkeletonAnimation = skeletonAnimations[0];
        }
        #endregion

        #region Unity物理
        if (rigidBody2d == null)
            rigidBody2d = GetComponent<Rigidbody2D>();

        if (boxCollider2d == null)
            boxCollider2d = GetComponent<BoxCollider2D>();
        #endregion

        ValidateComponents();
    }


    private void ValidateComponents()
    {
        if (characterStatsController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterStatsController> ", this);
        if (houndSkeletonAnimation == null)
            Debug.LogError("[CharacterRoot] 缺少 houndSkeletonAnimation。它应该指向 VisualRoot_Hound 上的 SkeletonAnimation。", this);

        if (hunterGunSkeletonAnimation == null)
            Debug.LogWarning("[CharacterRoot] 缺少 hunterGunSkeletonAnimation。HunterGun 位置同步和枪械 IK 不会生效。", this);
        //if(characterCameraTargetSystem ==null)
        //    Debug.LogError("[CharacterRoot] 缺少 CharacterCameraTargetSystem", this);
        if (stateArbitrator == null)
            Debug.LogError("[CharacterRoot] 缺少 StateArbitrator", this);
        if (orientationSystem == null)
            Debug.LogError("[CharacterRoot] 缺少 OrientationSystem", this);
        if (characterAnimationConfigManager == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAnimationConfigManager", this);
        if (animationHandleSystem == null)
            Debug.LogError("[CharacterRoot] 缺少 AnimationHandleSystem", this);
        if (wardrobeManager == null)
            Debug.LogWarning("[CharacterRoot] 缺少 WardrobeManager", this);
        if(aimCursorSystem ==null)
            Debug.LogWarning("[CharacterRoot] 缺少 AimCursorSystem", this);

        if(aimIKController == null)
            Debug.LogError("[CharacterRoot] 缺少 AimIKController ", this);
        if (lockTargetIKController == null)
            Debug.LogWarning("[CharacterRoot] 缺少 LockTargetIKController，锁定目标 IK_Hund 不会生效", this);
        if (characterEquipmentController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterEquipmentController> ", this);
        if(characterAnimationComposer == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAnimationComposer> ", this);
        if (hunterGunCursorFacingController == null)
            Debug.LogWarning("[CharacterRoot] 缺少 HunterGunCursorFacingController，枪口朝向鼠标的功能不会生效", this);
        if (hunterGunBonePositionMatcher == null)
            Debug.LogWarning("[CharacterRoot] 缺少 HunterGunBonePositionMatcher，HunterGun 位置同步功能不会生效", this);
        if (gunHandOcclusionByAimAngle == null)
            Debug.LogWarning("[CharacterRoot] 缺少 GunHandOcclusionByAimAngle，持枪手部遮挡不会自动处理", this);
        if (characterAttackController == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterAttackController> ", this);
        if (gunSystemController == null)
            Debug.LogError("[CharacterRoot] 缺少 GunSystemController> ", this);
        if (characterSpineEventSystem == null)
            Debug.LogWarning("[CharacterRoot] 缺少 CharacterSpineEventSystem，Spine 动画事件不会转发到角色逻辑。", this);
        if(characterWeaponInventory == null)
            Debug.LogError("[CharacterRoot] 缺少 CharacterWeaponInventory> ", this);
        if (characterPackageSystem == null)
            Debug.LogWarning("[CharacterRoot] 缺少 CharacterPackageSystem，背包功能不可用", this);
    }




    //特殊的给每个角色各自的SkeletonAnimation、characterSkillBook赋值的方法
    public void LoadCharacterDataFromSO(CharacterSO so)
    {
        if (so == null)
        {
            Debug.LogError("[CharacterRoot] LoadCharacterDataFromSO 失败：so == null");
            return;
        }

        // 1. 同步数据
        characterDataBus.LoadFromSO(so);

        // 2. 重新计算属性，这是关键
        if (characterStatsController != null)
        {
            characterStatsController.RecalculateStats();

            Debug.Log($"[CharacterRoot] 加载角色数据后 MoveSpeed = {characterStatsController.GetStat(CharacterStats.MoveSpeed)}");
        }
        else
        {
            Debug.LogError("[CharacterRoot] characterStatsController == null");
        }

        // 3. 更换 Spine 皮肤
        if (so.chracterModel != null)
        {
            skeletonAnimation.skeletonDataAsset = so.chracterModel;
            skeletonAnimation.Initialize(true);
        }

        characterAnimationConfigManager.LoadCharacterBasicAnimation();

    }


    private void TickUpdate(object system, float deltaTime)
    {
        if (system is ICharacterTickable tickable)
            tickable.ManagedUpdate(deltaTime);
    }

    private void TickLateUpdate(object system, float deltaTime)
    {
        if (system is ICharacterLateTickable tickable)
            tickable.ManagedLateUpdate(deltaTime);
    }

    private void TickFixedUpdate(object system, float fixedDeltaTime)
    {
        if (system is ICharacterFixedTickable tickable)
            tickable.ManagedFixedUpdate(fixedDeltaTime);
    }











}
