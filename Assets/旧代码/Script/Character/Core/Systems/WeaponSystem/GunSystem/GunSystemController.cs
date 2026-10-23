using UnityEngine;

// GunSystemController.ApplyWeaponRuntimeData(runtimeData)
//  -> 读取 damage / range / magazine
//  -> 读取 FirearmActionProfileSO
//  -> 读取 defaultFireMode
//  -> 读取 GunFireModeConfig
//  -> 计算 fireInterval 等运行时数据
//  -> 支持半自动 / 点射 / 全自动
public class GunSystemController : MonoBehaviour, ICharacterSubsystem,ICharacterTickable
{
    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;

    [Header("Fire Point")]
    [SerializeField] private Transform muzzlePoint;

    [Header("Hitscan")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float range = 30f;
    [SerializeField] private float damage = 20f;

    [Header("Fire Rate")]
    [SerializeField] private float fireCooldown = 0.15f;
    private float nextFireTime;

    [Header("Ammo")]
    [SerializeField] private bool infiniteAmmo = false;//无限子弹
    [SerializeField] private int magazineSize = 0;
    [SerializeField] private int ammoInMagazine = 0;

    [Header("Aim")]
    [SerializeField] private bool preferLockTargetPoint = true;
    private LockTargetPoint currentLockTargetPoint;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = true;
    [SerializeField] private bool debugLogFire = true;

    [Header("Fire Mode")]

    [SerializeField] private FirearmActionProfileSO firearmProfile;
    [SerializeField] private GunFireModeType currentFireModeType;
    [SerializeField] private GunFireModeConfig currentFireModeConfig;
    [Header("Runtime Stats")]
    [SerializeField] private FirearmWeaponStats currentFirearmStats;
    [SerializeField] private WeaponAudioProfileSO currentWeaponAudioProfile;
    [SerializeField] private GunShotVisualController shotVisualController;
    [Header("Hand IK")]
    [SerializeField] private GunHandIKBinder gunHandIKBinder;

    private bool burstActive;
    private int burstShotsRemaining;
    private float nextBurstShotTime;
    private float nextFireInputAllowedTime;


    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineSize => magazineSize;



    /// <summary>
    /// Debug 
    /// </summary>
    /// <param name="root"></param>

    [SerializeField] private float emptyMagazineLogInterval = 0.25f;
    private float nextEmptyMagazineLogTime;




    public void Initialize(CharacterRoot root)
    {
        characterRoot = root;

        if (characterRoot != null)
            dataBus = characterRoot.characterDataBus;
        if (gunHandIKBinder == null && characterRoot != null)
            gunHandIKBinder = characterRoot.GetComponentInChildren<GunHandIKBinder>(true);
        if (ammoInMagazine <= 0)
            ammoInMagazine = magazineSize;
    }


    public void ManagedUpdate(float deltaTime)
    {
        UpdateBurstFire();
    }


    private void UpdateBurstFire()
    {
        if (!burstActive)
            return;

        if (currentFireModeConfig == null)
        {
            burstActive = false;
            return;
        }

        if (burstShotsRemaining <= 0)
        {
            burstActive = false;
            nextFireInputAllowedTime = Time.time + currentFireModeConfig.burstCycleInterval;
            return;
        }

        if (Time.time < nextBurstShotTime)
            return;

        bool fired = TryFire();

        if (!fired)
        {
            // 没子弹或超时间时直接中断 burst
            burstActive = false;
            return;
        }

        burstShotsRemaining--;

        nextBurstShotTime = Time.time + currentFireModeConfig.burstShotInterval;

        if (burstShotsRemaining <= 0)
        {
            burstActive = false;
            nextFireInputAllowedTime = Time.time + currentFireModeConfig.burstCycleInterval;
        }
    }






    public void SetCurrentLockTargetPoint(LockTargetPoint targetPoint)
    {
        currentLockTargetPoint = targetPoint;
    }


    public bool CanFire()
    {
        if (characterRoot == null || dataBus == null)
            return false;

        if (muzzlePoint == null)
            return false;

        if (Time.time < nextFireTime)
            return false;

        if (!infiniteAmmo && ammoInMagazine <= 0)
            return false;

        return true;
    }



    public bool TryFire()
    {
        if (!CanFire())
        {
            LogCannotFire();
            return false;
        }

        FireInternal();
        return true;
    }
    private void LogCannotFire()
    {
        if (!debugLogFire)
            return;

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            if (Time.time >= nextEmptyMagazineLogTime)
            {
                nextEmptyMagazineLogTime = Time.time + emptyMagazineLogInterval;

                Debug.Log(
                    $"[GunSystemController] Empty magazine. ammo={ammoInMagazine}/{magazineSize}",
                    this
                );
            }

            return;
        }

        Debug.Log(
            $"[GunSystemController] Cannot fire. time={Time.time:F2}, next={nextFireTime:F2}, ammo={ammoInMagazine}/{magazineSize}",
            this
        );
    }

    private void FireInternal()
    {
        nextFireTime = Time.time + fireCooldown;

        if (!infiniteAmmo)
            ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);

        Vector2 origin = muzzlePoint.position;
        Vector2 direction = GetShootDirection(origin);

        if (direction.sqrMagnitude <= 0.0001f)
            direction = characterRoot.transform.right;

        direction.Normalize();

        PublishWeaponFiredEvent(origin, direction);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range, hitMask);



        Vector2 endPoint = origin + direction * range;

        if (hit.collider != null)
        {
            endPoint = hit.point;
            HandleHit(hit, direction);
        }
        else if (debugLogFire)
        {
            Debug.Log(
                $"[GunSystemController] Fire miss. ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }

        shotVisualController?.PlayShot(
    origin,
    endPoint,
    hit,
    muzzlePoint
);

        SpawnShotVisual(origin, endPoint);

        if (debugDrawRay)
            Debug.DrawLine(origin, endPoint, Color.yellow, 0.25f);

        if (debugLogFire)
        {
            Debug.Log(
                $"[GunSystemController] Shot fired. ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }
    }



    private Vector2 GetShootDirection(Vector2 origin)
    {
        if (preferLockTargetPoint
            && currentLockTargetPoint != null
            && currentLockTargetPoint.IsValid())
        {
            return (Vector2)currentLockTargetPoint.GetWorldPosition() - origin;
        }

        if (dataBus != null)
        {
            return (Vector2)dataBus.mousePositionOnScreen - origin;
        }

        return Vector2.right;
    }
    private void HandleHit(RaycastHit2D hit, Vector2 shotDirection)
    {
        if (hit.collider == null)
            return;

        EnemyRoot enemyRoot = hit.collider.GetComponentInParent<EnemyRoot>();

        if (enemyRoot != null)
        {
            Debug.Log(
                $"[GunSystemController] Hit enemy: {enemyRoot.name}, collider={hit.collider.name}, damage={damage}, ammo={ammoInMagazine}/{magazineSize}",
                this
            );
        }
        else
        {
            Debug.Log(
                $"[GunSystemController] Hit non-enemy: {hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}",
                this
            );

            SpawnImpactEffect(hit.point, hit.normal, null);
            return;
        }

        if (!enemyRoot.IsAlive || !enemyRoot.CanBeTargeted)
        {
            SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
            return;
        }

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

        if (damageable == null || !damageable.CanTakeDamage)
        {
            SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
            return;
        }

        DamageInfo damageInfo = new DamageInfo
        {
            attacker = characterRoot != null ? characterRoot.gameObject : gameObject,
            damage = damage,
            hitPoint = hit.point,
            hitNormal = hit.normal,
            shotDirection = shotDirection,
            impactType = CombatImpactType.Gunshot
        };

        damageable.TakeDamage(damageInfo);

        SpawnImpactEffect(hit.point, hit.normal, enemyRoot);
    }

  

    private void SpawnShotVisual(Vector2 origin, Vector2 endPoint)
    {
        // 第一版先空开。
        // 后续是枪口火花、LineRenderer 弹道、枪口烟雾等。
    }

    private void SpawnImpactEffect(Vector2 hitPoint, Vector2 hitNormal, EnemyRoot enemyRoot)
    {

        PublishBulletImpactEvent(hitPoint, hitNormal, enemyRoot);
        // 第一版先空开。
        // 后续是血溅、火花、受击特效。
    }
    public bool CanStartReload()
    {
        if (infiniteAmmo)
            return false;

        if (magazineSize <= 0)
            return false;

        if (ammoInMagazine >= magazineSize)
            return false;

        return true;
    }

    public void BeginReload()
    {
        // 当前阶段可以先只标记，以后在这里发 WeaponReloadStartedEvent
    }

    public void CommitReload()
    {
        ammoInMagazine = Mathf.Max(0, magazineSize);

        // 当前阶段可以 Debug
        Debug.Log($"[GunSystemController] Reload committed. ammo={ammoInMagazine}/{magazineSize}", this);
    }


    public void EndReload()
    {
        // 当前阶段可以先只标记，以后在这里发 WeaponReloadFinishedEvent
    }



    public void ApplyWeaponRuntimeData(WeaponRuntimeData weaponRuntimeData)
    {
        if (weaponRuntimeData == null)
        {
            ClearWeaponRuntimeData();
            return;
        }

        /*
         * 切换到非枪械时也要清空枪械数据。
         */
        if (!weaponRuntimeData.IsFirearm)
        {
            ClearWeaponRuntimeData();
            return;
        }

        WeaponCommonCoreStats commonStats = weaponRuntimeData.CommonStats;
        FirearmWeaponStats firearmStats = weaponRuntimeData.FirearmStats;

        currentFirearmStats = firearmStats;
        currentWeaponAudioProfile = weaponRuntimeData.AudioProfile;

        if (commonStats != null)
        {
            damage = commonStats.damage;
        }

        if (firearmStats != null)
        {
            range = firearmStats.range;
            magazineSize = Mathf.Max(1, firearmStats.magazineSize);
            ammoInMagazine = magazineSize;
        }

        firearmProfile = weaponRuntimeData.FirearmActionProfile;

        if (firearmProfile != null)
        {
            currentFireModeType = firearmProfile.defaultFireMode;
            currentFireModeConfig = firearmProfile.GetDefaultFireModeConfig();

            if (currentFireModeConfig != null)
                fireCooldown = currentFireModeConfig.fireInterval;
        }
        else
        {
            currentFireModeConfig = null;
        }

        burstActive = false;
        burstShotsRemaining = 0;
        nextBurstShotTime = 0f;
        nextFireInputAllowedTime = 0f;
        nextFireTime = 0f;


//        Debug.Log(
//    $"[GunSystemController] ApplyWeaponRuntimeData: damage={damage}, range={range}, magazine={ammoInMagazine}/{magazineSize}, fireMode={currentFireModeType}",
//    this
//);


    }


   

    /// <summary>
    /// 切换射击模式接口
    /// </summary>
    public GunFireModeType CurrentFireModeType => currentFireModeType;
    public GunFireModeConfig CurrentFireModeConfig => currentFireModeConfig;

    public void SwitchToNextFireMode()
    {
        if (firearmProfile == null || firearmProfile.fireModes == null)
            return;

        if (firearmProfile.fireModes.Count == 0)
            return;

        int currentIndex = -1;

        for (int i = 0; i < firearmProfile.fireModes.Count; i++)
        {
            if (firearmProfile.fireModes[i] == null)
                continue;

            if (firearmProfile.fireModes[i].fireModeType == currentFireModeType)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = currentIndex + 1;

        if (nextIndex >= firearmProfile.fireModes.Count)
            nextIndex = 0;

        SetFireMode(firearmProfile.fireModes[nextIndex].fireModeType);
    }

    public bool SetFireMode(GunFireModeType modeType)
    {
        if (firearmProfile == null)
            return false;

        GunFireModeConfig config = firearmProfile.GetFireModeConfig(modeType);

        if (config == null)
            return false;

        currentFireModeType = modeType;
        currentFireModeConfig = config;

        fireCooldown = currentFireModeConfig.fireInterval;

        burstActive = false;
        burstShotsRemaining = 0;

        Debug.Log($"[GunSystemController] Switch fire mode: {currentFireModeType}", this);

        return true;
    }

    public bool TryHandleFireInput(bool attackPressedThisFrame, bool attackHeld)
    {
        if (currentFireModeConfig == null)
            return TryFire();

        switch (currentFireModeConfig.fireModeType)
        {
            case GunFireModeType.SemiAuto:
                return HandleSemiAutoFire(attackPressedThisFrame);

            case GunFireModeType.FullAuto:
                return HandleFullAutoFire(attackHeld);

            case GunFireModeType.Burst:
                return HandleBurstFireInput(attackPressedThisFrame);

            default:
                return HandleSemiAutoFire(attackPressedThisFrame);
        }
    }
    private bool HandleSemiAutoFire(bool attackPressedThisFrame)
    {
        if (!attackPressedThisFrame)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        bool fired = TryFire();

        if (fired)
        {
            float interval = currentFireModeConfig != null
                ? currentFireModeConfig.fireInterval
                : fireCooldown;

            nextFireInputAllowedTime = Time.time + interval;
        }

        return fired;
    }
    private bool HandleFullAutoFire(bool attackHeld)
    {
        if (currentFireModeConfig != null && !currentFireModeConfig.allowHoldToFire)
            return false;

        if (!attackHeld)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        bool fired = TryFire();

        if (fired)
        {
            float interval = currentFireModeConfig != null
                ? currentFireModeConfig.fireInterval
                : fireCooldown;

            nextFireInputAllowedTime = Time.time + interval;
        }

        return fired;
    }

    private bool HandleBurstFireInput(bool attackPressedThisFrame)
    {
        if (!attackPressedThisFrame)
            return false;

        if (burstActive)
            return false;

        if (Time.time < nextFireInputAllowedTime)
            return false;

        StartBurst();

        return true;
    }

    private void StartBurst()
    {
        if (currentFireModeConfig == null)
            return;

        burstActive = true;
        burstShotsRemaining = Mathf.Max(1, currentFireModeConfig.burstCount);
        nextBurstShotTime = Time.time;
    }

    private void PublishWeaponFiredEvent(Vector2 origin, Vector2 direction)
    {
        if (GameFlowRoot.Instance == null)
            return;

        if (GameFlowRoot.Instance.EventHub == null)
            return;

        GameFlowRoot.Instance.EventHub.Hunt.Publish(new WeaponFiredEvent
        {
            owner = characterRoot != null ? characterRoot.gameObject : gameObject,
            position = origin,
            direction = direction,
            audioProfile = currentWeaponAudioProfile,
            isPlayer = true
        });
    }
    private void PublishBulletImpactEvent(Vector2 hitPoint, Vector2 hitNormal, EnemyRoot enemyRoot)
    {
        if (GameFlowRoot.Instance == null)
            return;

        if (GameFlowRoot.Instance.EventHub == null)
            return;

        GameFlowRoot.Instance.EventHub.Hunt.Publish(new BulletImpactEvent
        {
            position = hitPoint,
            normal = hitNormal,
            hitObject = enemyRoot != null ? enemyRoot.gameObject : null,
            surfaceType = enemyRoot != null ? SurfaceType.Flesh : SurfaceType.Default,
            hitEnemy = enemyRoot != null,
            audioProfile = currentWeaponAudioProfile
        });
    }


    public void SetRightHandIKLocked(bool locked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetRightHandIKBindingEnabled(locked);
    }

    public void SetLeftHandIKLocked(bool locked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetLeftHandIKBindingEnabled(locked);
    }

    public void SetHandIKLocked(bool rightLocked, bool leftLocked)
    {
        if (gunHandIKBinder == null)
            return;

        gunHandIKBinder.SetHandIKBindingEnabled(rightLocked, leftLocked);
    }

    public void ReleaseLeftHandForAction()
    {
        SetLeftHandIKLocked(false);
    }

    public void RestoreLeftHandIK()
    {
        SetLeftHandIKLocked(true);
    }


    public void ClearWeaponRuntimeData()
    {
        /*
         * 停止所有未完成的射击行为。
         */
        burstActive = false;
        burstShotsRemaining = 0;
        nextBurstShotTime = 0f;
        nextFireInputAllowedTime = 0f;
        nextFireTime = 0f;

        /*
         * 清空当前枪械配置。
         */
        firearmProfile = null;
        currentFireModeConfig = null;
        currentFirearmStats = null;
        currentWeaponAudioProfile = null;

        /*
         * 关闭精确射击相关状态。
         */
        currentFireModeType = default;

        magazineSize = 0;
        ammoInMagazine = 0;

        /*
         * damage/range 可以保留 Inspector 默认值。
         * 也可以清零。这里建议清零，防止卸枪时
         * 仍然使用上一个枪械数据。
         */
        damage = 0f;
        range = 0f;

        currentLockTargetPoint = null;

        Debug.Log(
            "[GunSystemController] Weapon runtime data cleared.",
            this
        );
    }



}
