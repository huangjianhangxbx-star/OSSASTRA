# ProjectScript Code Documentation - Part 6

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 20

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WearableSlotPlaceholderView.cs` | 31 | - | class WearableSlotPlaceholderView | 0 |
| `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WorldPickupPrompt.cs` | 120 | - | class WorldPickupPrompt | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/CharacterAppearance_DataBus.cs` | 18 | - | class CharacterAppearance_DataBus | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/ISkinApplicator.cs` | 12 | - | interface ISkinApplicator | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/SkinItemSO.cs` | 79 | - | enum SkinPartPath, class SkinItemSO | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/SkinSetSO.cs` | 12 | - | class SkinSetSO | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/SpineSkinApplicator.cs` | 45 | - | class SpineSkinApplicator | 0 |
| `Assets/Script/Character/Core/Systems/SkinSystem/WardrobeManager.cs` | 214 | - | class WardrobeManager | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/CharacterBaseStatsSO.cs` | 20 | - | class CharacterBaseStatsSO | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/CharacterLevelGrowthSO.cs` | 19 | - | class CharacterLevelGrowthSO | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatProfileSO.cs` | 8 | - | class CharacterStatProfileSO | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatsController.cs` | 370 | - | class CharacterStatsController | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/StatsEntry.cs` | 132 | - | class StatsEntry, class StatSheet | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/StatsModifier.cs` | 22 | - | class StatsModifier | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/FirearmWeaponStats.cs` | 55 | - | class FirearmWeaponStats | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/MeleeWeaponStats.cs` | 21 | - | class MeleeWeaponStats | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCharacterBonusStats.cs` | 68 | - | class WeaponCharacterBonusStats | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCommonCoreStats.cs` | 23 | - | class WeaponCommonCoreStats | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponRuntimeData.cs` | 234 | - | struct FirearmFinalStats, class WeaponRuntimeData | 0 |
| `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSO.cs` | 53 | - | class WeaponStatsSO | 0 |

## Public API

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WearableSlotPlaceholderView.cs`
- `public class WearableSlotPlaceholderView : MonoBehaviour
{`
- `public int SlotIndex {`
- `public void Initialize(int index, string displayLabel)
    {`
### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WorldPickupPrompt.cs`
- `public class WorldPickupPrompt : MonoBehaviour
{`
- `public void Initialize(Camera cam)
    {`
- `public void Show(Vector3 worldAnchor, string displayName, bool backpackFull)
    {`
- `public void Hide()
    {`
### `Assets/Script/Character/Core/Systems/SkinSystem/CharacterAppearance_DataBus.cs`
- `public class CharacterAppearance_DataBus
{`
- `public void SetSkin(SkinItemSO item)
    {`
- `public IEnumerable<SkinItemSO> GetAllEquipped() => currentSkins.Values;`
### `Assets/Script/Character/Core/Systems/SkinSystem/ISkinApplicator.cs`
- `public interface ISkinApplicator
{`
### `Assets/Script/Character/Core/Systems/SkinSystem/SkinItemSO.cs`
- `public enum SkinPartPath
{`
- `public class SkinItemSO : ScriptableObject
{`
- `public string spinessSkinName;`
- `public SkeletonDataAsset skeletonDataAsset;`
- `public SkinPartPath partPath;`
- `public string skinNameChinese;`
- `public SkinPartPath slot;`
- `public string displayName;`
- `public string spineSkinName    // 这是最终提供给 Manager 或 Applicator 使用的属性
    {`
- `public string debugFullResult;`
### `Assets/Script/Character/Core/Systems/SkinSystem/SkinSetSO.cs`
- `public class SkinSetSO : ScriptableObject
{`
- `public string setName = "新套装";`
- `public List<SkinItemSO> items;`
### `Assets/Script/Character/Core/Systems/SkinSystem/SpineSkinApplicator.cs`
- `public class SpineSkinApplicator : MonoBehaviour, ISkinApplicator
{`
- `public void ApplySkins(IEnumerable<SkinItemSO> items)
    {`
### `Assets/Script/Character/Core/Systems/SkinSystem/WardrobeManager.cs`
- `public class WardrobeManager : MonoBehaviour,ICharacterSubsystem
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public void EquipItem(SkinItemSO item)
    {`
- `public void EquipItems(params SkinItemSO[] items)//重载
    {`
- `public void UnequipSlot(SkinPartPath slotName)
    {`
- `public void ChangeEquipment(SkinItemSO newItem)
    {`
- `public void ChangeFullSet(SkinSetSO set)
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterBaseStatsSO.cs`
- `public class CharacterBaseStatsSO : ScriptableObject
{`
- `public StatSheet baseStats = new StatSheet();`
### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterLevelGrowthSO.cs`
- `public class CharacterLevelGrowthSO : ScriptableObject
{`
- `public StatSheet growthPerLevel = new StatSheet();`
### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatProfileSO.cs`
- `public class CharacterStatProfileSO : ScriptableObject
{`
- `public CharacterBaseStatsSO baseStats;`
- `public CharacterLevelGrowthSO levelGrowth;`
### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatsController.cs`
- `public class CharacterStatsController : MonoBehaviour, ICharacterSubsystem
{`
- `public void Initialize(CharacterRoot root)
    {`
- `public float GetStat(CharacterStats statType)
    {`
- `public void AddModifier(StatsModifier modifier)
    {`
- `public void RemoveModifiersFromSource(object source)
    {`
- `public void RecalculateStats()
    {`
- `public void DebugPrintStatBreakdownFromContextMenu()
    {`
- `public void DebugPrintStatBreakdown()
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/StatsEntry.cs`
- `public class StatsEntry
{`
- `public CharacterStats CharacterStats => characterStats;`
- `public float Value
    {`
- `public StatsEntry(CharacterStats characterStats, float value)
    {`
- `public class StatSheet
{`
- `public IReadOnlyList<StatsEntry> Values => values;`
- `public float GetValue(CharacterStats type)
    {`
- `public List<StatsModifier> ToAddModifiers(object source)
    {`
- `public void EnsureCompleteAndUnique()
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/StatsModifier.cs`
- `public class StatsModifier
{`
- `public CharacterStats characterStats;`
- `public StatModifierMode mode;`
- `public float value;`
- `public object source;`
- `public StatsModifier() {`
- `public StatsModifier(CharacterStats statType, StatModifierMode mode, float value, object source = null)
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/FirearmWeaponStats.cs`
- `public class FirearmWeaponStats
{`
- `public float range = 30f;`
- `public float projectileSpeed = 60f;`
- `public int magazineSize = 30;`
- `public float reloadTime = 1.2f;`
- `public float spreadAngle = 1.5f;`
- `public float movingSpreadAdd = 1.0f;`
- `public float spreadIncreasePerShot = 0.2f;`
- `public float maxSpreadAngle = 5f;`
- `public float recoil = 1f;`
- `public float recoilRecoverSpeed = 8f;`
- `public float aimEnterTime = 0.15f;`
- `public float aimMoveSpeedMultiplier = 0.65f;`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/MeleeWeaponStats.cs`
- `public class MeleeWeaponStats
{`
- `public float attackSpeed = 1f;`
- `public float staminaEfficiency = 1f;`
- `public float rangeMultiplier = 1f;`
- `public float hitboxScaleMultiplier = 1f;`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCharacterBonusStats.cs`
- `public class WeaponCharacterBonusStats
{`
- `public float maxHpAdd;`
- `public float defenseAdd;`
- `public float moveSpeedAdd;`
- `public float moveSpeedPercentAdd;`
- `public float critChanceAdd;`
- `public float critDamageAdd;`
- `public float firearmDamagePercentAdd;`
- `public float fireRatePercentAdd;`
- `public float reloadSpeedPercentAdd;`
- `public float recoilControlAdd;`
- `public float accuracyPercentAdd;`
- `public float meleeDamagePercentAdd;`
- `public float meleeAttackSpeedPercentAdd;`
- `public void AppendModifiers(List<StatsModifier> result, object source)
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCommonCoreStats.cs`
- `public class WeaponCommonCoreStats
{`
- `public float damage = 10f;`
- `public float critChance = 0f;`
- `public float critDamage = 1.5f;`
- `public float poiseDamage = 0f;`
- `public float knockbackForce = 0f;`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponRuntimeData.cs`
- `public struct FirearmFinalStats
{`
- `public float damage;`
- `public float critChance;`
- `public float critDamage;`
- `public float poiseDamage;`
- `public float knockbackForce;`
- `public float range;`
- `public int magazineSize;`
- `public float reloadTime;`
- `public float projectileSpeed;`
- `public float spreadAngle;`
- `public float recoil;`
- `public class WeaponRuntimeData
{`
- `public WeaponConfigSO config;`
- `public WeaponRoot weaponRoot;`
- `public List<StatsModifier> runtimeModifiers = new List<StatsModifier>();`
- `public WeaponRuntimeData(WeaponConfigSO config)
    {`
- `public WeaponType WeaponType
    {`
- `public bool IsFirearm => WeaponType == WeaponType.Firearm;`
- `public bool IsMelee => WeaponType == WeaponType.Melee;`
- `public WeaponStatsSO Stats
    {`
- `public WeaponCommonCoreStats CommonStats
    {`
- `public FirearmWeaponStats FirearmStats
    {`
- `public MeleeWeaponStats MeleeStats
    {`
- `public AnimationPlanOverrideRuleSetSO AnimationOverride
    {`
- `public WeaponActionProfileSO ActionProfile
    {`
- `public WeaponAudioProfileSO AudioProfile
    {`
- `public FirearmActionProfileSO FirearmActionProfile
    {`
- `public float GetFinalFireInterval(GunFireModeConfig fireModeConfig, CharacterStatsController statsController)
    {`
- `public WeaponRoot WeaponRoot => weaponRoot;`
- `public FirearmFinalStats BuildFirearmFinalStats(CharacterStatsController statsController)
    {`
### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSO.cs`
- `public class WeaponStatsSO : ScriptableObject
{`
- `public WeaponType weaponType = WeaponType.Firearm;`
- `public WeaponCommonCoreStats commonCoreStats = new WeaponCommonCoreStats();`
- `public WeaponCharacterBonusStats characterBonusStats = new WeaponCharacterBonusStats();`
- `public FirearmWeaponStats firearmStats = new FirearmWeaponStats();`
- `public MeleeWeaponStats meleeStats = new MeleeWeaponStats();`
- `public bool IsFirearm => weaponType == WeaponType.Firearm;`
- `public bool IsMelee => weaponType == WeaponType.Melee;`
- `public List<StatsModifier> ToCharacterStatModifiers(object source)
    {`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WearableSlotPlaceholderView.cs`

```cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Placeholder view for wearable equipment slots that are not yet implemented.
/// Displays a locked/disabled state and does NOT accept drag/drop.
/// </summary>
public class WearableSlotPlaceholderView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image lockOverlay;

    public int SlotIndex { get; private set; }

    public void Initialize(int index, string displayLabel)
    {
        SlotIndex = index;
        if (label != null)
            label.text = displayLabel;

        // Ensure no raycast interception — these slots can't receive drops
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        if (icon != null) icon.raycastTarget = false;
        if (lockOverlay != null) lockOverlay.raycastTarget = false;
        if (label != null) label.raycastTarget = false;
    }
}
```

### `Assets/Script/Character/Core/Systems/PackageSystem/Runtime/UI/WorldPickupPrompt.cs`

```cs
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Screen-space pickup prompt. Follows a world anchor position.
/// Lives inside a HUD Canvas (PickupPromptLayer), never under the character Transform.
/// Owned and instantiated by CharacterPickupController.
/// </summary>
public class WorldPickupPrompt : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private UnityEngine.UI.Image background;
    [SerializeField] private TMPro.TextMeshProUGUI labelText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color fullColor = new Color(0.6f, 0.1f, 0.1f, 0.85f);

    private Camera _mainCamera;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (background == null)
            background = GetComponentInChildren<UnityEngine.UI.Image>();
        if (labelText == null)
            labelText = GetComponentInChildren<TMPro.TextMeshProUGUI>();

        if (canvasGroup != null)
        {
            // Prompt 永不阻挡 UI Raycast
            canvasGroup.blocksRaycasts = false;
        }
        Hide();
    }

    public void Initialize(Camera cam)
    {
        _mainCamera = cam;
    }

    public void Show(Vector3 worldAnchor, string displayName, bool backpackFull)
    {
        if (_mainCamera == null) return;

        // 目标在相机后方（背面）时隐藏
        Vector3 viewport = _mainCamera.WorldToViewportPoint(worldAnchor);
        if (viewport.z < 0f)
        {
            Hide();
            return;
        }

        var parentRT = transform.parent as RectTransform;
        if (parentRT == null)
        {
            // 父节点必须是 RectTransform（位于某个 Canvas 图层内）
            Hide();
            return;
        }

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldAnchor);

        // 依 Canvas 渲染模式选择正确的 UI 相机：
        //  Screen Space - Overlay：UI 相机为 null
        //  Screen Space - Camera：Canvas.worldCamera
        Canvas canvas = GetComponentInParent<Canvas>();
        DecideUiCamera(canvas, out Camera uiCamera);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                screenPos,
                uiCamera,
                out Vector2 localPos))
        {
            // 转换失败（通常目标在屏幕外），隐藏
            Hide();
            return;
        }

        ((RectTransform)transform).anchoredPosition = localPos;

        if (labelText != null)
        {
            if (backpackFull)
                labelText.text = $"[E] {displayName}\n<color=#ff6666>Backpack Full</color>";
            else
                labelText.text = $"[E] {displayName}";
        }

        if (background != null)
            background.color = backpackFull ? fullColor : normalColor;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void DecideUiCamera(Canvas canvas, out Camera uiCamera)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = null; // Overlay canvas 使用 null UI camera
        }
        else
        {
            uiCamera = canvas.worldCamera;
        }
    }

    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/CharacterAppearance_DataBus.cs`

```cs
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAppearance_DataBus
{
    // 存储当前选中的皮肤项：部位 -> 数据
    private Dictionary<SkinPartPath, SkinItemSO> currentSkins = new Dictionary<SkinPartPath, SkinItemSO>();

    public void SetSkin(SkinItemSO item)
    {
        currentSkins[item.slot] = item;
    }

    public IEnumerable<SkinItemSO> GetAllEquipped() => currentSkins.Values;//获取当前皮肤信息的方法？
}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/ISkinApplicator.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISkinApplicator
{

    void ApplySkins(IEnumerable<SkinItemSO> items);


}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/SkinItemSO.cs`

```cs
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SkinPartPath
{
    Accessary_Body,
    Accessary_Hand,
    Accessary_Head,
    Accessary_Leg,
    BasicSkin_Body_In,
    BasicSkin_Body_Out,
    BasicSkin_Face,
    BasicSkin_Foot,
    BasicSkin_Hand,
    BasicSkin_Leg,
    BasicSkin_Hair

}
[CreateAssetMenu(fileName = "NewSkinItem", menuName = "ScriptableObject/换装系统/新皮肤")]
public class SkinItemSO : ScriptableObject
{
    //=================方案1：spine官方api直接链接骨骼搜寻皮肤名====================（次选）
    [SpineSkin(dataField: "skeletonDataAsset")]
    public string spinessSkinName;
    public SkeletonDataAsset skeletonDataAsset; // 必须引用这个文件来提供数据源

    //================方案2：=========================（主选，解耦）
    [Header("1. 选择皮肤路径前缀")]
    public SkinPartPath partPath;
    [Header("2. 输入中文名 (Spine内名称)")]
    public string skinNameChinese;
    [Header("所属的皮肤名称")]
    public SkinPartPath slot;        // 所属部位(slot和SkinPartPath共通）
    public string displayName;   // 游戏中显示的名称
    public string spineSkinName    // 这是最终提供给 Manager 或 Applicator 使用的属性
    {
        get
        {
            string prefix = GetPathPrefix(partPath);
            return prefix + skinNameChinese;
        }
    }
    // 将枚举转为实际的路径字符串
    // 假设 skinItem 是你从 ScriptableObject 加载的实例
    //string actualSkinName = skinItem.spineSkinName; // ← 自动拼好！不用管 GetPathPrefix
    //skeleton.SetSkin(actualSkinName); // 或 skeleton.Data.FindSkin(actualSkinName)
    private string GetPathPrefix(SkinPartPath path)
    {
        return path switch
        {
            SkinPartPath.Accessary_Body => "Accessary/Body/",
            SkinPartPath.Accessary_Hand => "Accessary/Hand//",
            SkinPartPath.Accessary_Head => "Accessary/Head/",
            SkinPartPath.Accessary_Leg => "Accessary/Leg/",
            SkinPartPath.BasicSkin_Body_In => "BasicSkin/Body_In/",
            SkinPartPath.BasicSkin_Body_Out => "BasicSkin/Body_Out/",
            SkinPartPath.BasicSkin_Face => "BasicSkin/Face/",
            SkinPartPath.BasicSkin_Foot => "BasicSkin/Foot/",
            SkinPartPath.BasicSkin_Hand => "BasicSkin/Hand/",
            SkinPartPath.BasicSkin_Leg => "BasicSkin/Leg/",
            SkinPartPath.BasicSkin_Hair => "BasicSkin/Hair/",
            _ => ""
        };
    }

    // 为了调试方便，在 Inspector 里看一眼组合出来的到底对不对
    [TextArea]
    public string debugFullResult;
    private void OnValidate() // 只要面板改了，就实时刷新预览
    {
        debugFullResult = spineSkinName;
    }
}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/SkinSetSO.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkinSet", menuName = "ScriptableObject/换装系统/皮肤组合")]
public class SkinSetSO : ScriptableObject
{
    public string setName = "新套装";

    // 关键：这一套衣服包含的所有单件（如：头、身、腿）
    public List<SkinItemSO> items;
}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/SpineSkinApplicator.cs`

```cs
using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpineSkinApplicator : MonoBehaviour, ISkinApplicator
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    private Skin combinedSkin;

    public void ApplySkins(IEnumerable<SkinItemSO> items)
    {
        var skeleton = skeletonAnimation.Skeleton;
        var skeletonData = skeleton.Data;

        // 1. 创建/清空组合皮肤
        combinedSkin = combinedSkin ?? new Skin("combined-skin");
        combinedSkin.Clear();

        // 2. 遍历数据进行合并
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.spineSkinName)) continue;

            Skin foundSkin = skeletonData.FindSkin(item.spineSkinName);
            if (foundSkin != null)
            {
                combinedSkin.AddSkin(foundSkin);
            }
            else
            {
                Debug.LogWarning($"Spine皮肤不存在: {item.spineSkinName}");
            }
        }
        // 3. 应用并刷新
        skeleton.SetSkin(combinedSkin);
 
        skeleton.SetupPoseSlots();
        skeletonAnimation.Update(0);
        //这是 Spine 的强制刷新 API。Update(0) 表示“跳过时间步进，只做一次骨骼计算并更新 Mesh”。
        //因为在切换皮肤后，骨骼姿势没变，但贴图变了，必须手动触发一次更新才能看到效果。
    }
}
```

### `Assets/Script/Character/Core/Systems/SkinSystem/WardrobeManager.cs`

```cs
using Spine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Cinemachine.CinemachinePathBase;

public class WardrobeManager : MonoBehaviour,ICharacterSubsystem
{
    private CharacterAppearance_DataBus appearance = new CharacterAppearance_DataBus();
    private ISkinApplicator applicator;
    private CharacterRoot characterRoot;

    [SerializeField] private SkinItemSO Face;
    [SerializeField] private SkinItemSO Body_In;
    [SerializeField] private SkinItemSO Body_Out;
    [SerializeField] private SkinItemSO Foot;
    [SerializeField] private SkinItemSO Hair;
    [SerializeField] private SkinItemSO Hand;
    [SerializeField] private SkinItemSO Leg;
    private bool _isInitialized;


    // 管理当前穿上的部件列表（按 Slot 分类）
    private readonly Dictionary<SkinPartPath, SkinItemSO> currentSkinItems = new();//用于数个独立部件穿着

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;
        this.characterRoot = root;
        applicator = GetComponent<ISkinApplicator>();
        _isInitialized = true;
    }


    private void Start()
    {
        EquipItem(Face);
        EquipItem(Body_In);
        EquipItem(Body_Out);
        EquipItem(Foot);
        EquipItem(Hair);
        EquipItem(Hand);
        EquipItem(Leg);
        //Debug.Log("ssss");


    }



    // === 外部调用入口 ===
    /// <summary>
    /// 穿上一件单独的服装部件并记录（例如：点击“戴头盔”）
    /// </summary>
    public void EquipItem(SkinItemSO item)
    {
        if (item == null) return;

        // 获取该部件应作用的 Slot 名称（比如 "head", "body"）
        SkinPartPath slotName = item.slot;

        // 记录当前装备
        currentSkinItems[slotName] = item;

        // 应用到骨骼
        ApplyAllSkins();
    }

    /// <summary>
    /// 一次性穿上多个部件（例如：加载一套搭配）
    /// </summary>
    public void EquipItems(params SkinItemSO[] items)//重载
    {
        foreach (var item in items)
        {
            if (item != null)
            {
                SkinPartPath slotName = item.slot;

                if (slotName!= null )
                    currentSkinItems[slotName] = item;
            }
        }
        ApplyAllSkins();
    }
    /// <summary>
    /// 卸下某部位的装备
    /// </summary>
    public void UnequipSlot(SkinPartPath slotName)
    {
        currentSkinItems.Remove(slotName);
        ApplyAllSkins();
    }
    // === 内部核心逻辑 ===
    private void ApplyAllSkins()
    {
        // --- 诊断检查 ---
        if (characterRoot == null) return;

        // 1. 检查组件是否存在
        if (characterRoot.skeletonAnimation == null)
        {
            Debug.LogError("未找到 SkeletonAnimation 组件");
            return;
        }

        // 2. 强制初始化并直接检查组件内部的 skeleton
        if (characterRoot.skeletonAnimation.skeleton == null)
        {
            characterRoot.skeletonAnimation.Initialize(false);
        }
        // 3. 最终确认
        Skeleton skeleton = characterRoot.skeleton; // 这时通过属性拿到的是最新的
        if (skeleton == null)
        {
            Debug.LogError("Spine 骨骼初始化失败，请检查 Skeleton Data Asset 是否已指派！");
            return;
        }
        if (currentSkinItems == null)
        {
            Debug.LogError($"[Wardrobe] 错误：currentItems 字典未初始化！", this);
            return;
        }
        // ----------------
        // 1. 创建一个全新的、空的临时皮肤，起名叫 "CombinedSkin"
        var combinedSkin = new Spine.Skin("CombinedSkin");

        // 2. 获取 SkeletonData（为了获取 Default Skin 里的基础附件，比如内衬或基础肤色）
        var skeletonData = characterRoot.skeleton.Data;
        combinedSkin.AddSkin(skeletonData.DefaultSkin);

        // 3. 遍历你字典里存的所有装备
        foreach (var kvp in currentSkinItems)
        {
            SkinItemSO item = kvp.Value;
            if (item == null) continue;

            string fullPath = item.spineSkinName;
            // 寻找到 Spine 资源里对应的皮肤片段
            Spine.Skin partSkin = skeletonData.FindSkin(fullPath);

            if (partSkin != null)
            {
                // ✅ 核心操作：将这个皮肤片段“添加”到组合皮肤中，而不是“替换”
                combinedSkin.AddSkin(partSkin);
            }
            else
            {
                Debug.LogWarning($"[Wardrobe] 找不到皮肤路径: {fullPath}");
            }
        }

        // 4. 最后，一次性应用这个组合了所有部件的皮肤
        characterRoot.skeleton.SetSkin(combinedSkin);

        // 5. 刷新
        characterRoot.skeleton.SetupPoseSlots(); // 这行等同于 SetSlotsToSetupPose()，会把所有 Slot 的附件切回默认状态（但不改变骨骼姿势）

        //haracterRoot.skeleton.SetSlotsToSetupPose();
        characterRoot.skeletonAnimation.Update(0f);
        //// 逻辑开始
        //characterRoot.skeleton.SetSkin((Spine.Skin)null); // 清空皮肤

        //foreach (var kvp in currentSkinItems)
        //{
        //    SkinPartPath slotName = kvp.Key;
        //    SkinItemSO item = kvp.Value;

        //    if (item == null) continue;

        //    string fullPath = item.spineSkinName;
        //    Skin skin = characterRoot.skeleton.Data.FindSkin(fullPath);
        //    if (skin != null)
        //    {
        //        // 如果你使用了之前讨论的 SetSkinInSlot 扩展方法
        //        characterRoot.skeleton.SetSkin(skin);
        //    }
        //    else
        //    {
        //        Debug.LogWarning($"[Wardrobe] 找不到皮肤路径: {fullPath}");
        //    }
        //}

        //// 刷新显示
        //characterRoot.skeleton.SetSlotsToSetupPose();
        //characterRoot.skeletonAnimation.Update(0f);
    }
    // 外部调用接口：换一件衣服
    public void ChangeEquipment(SkinItemSO newItem)
    {
        appearance.SetSkin(newItem);
        UpdateAppearance();
    }
    // --- 穿一整套 ---
    public void ChangeFullSet(SkinSetSO set)
    {
        if (set == null || set.items == null) return;

        // 循环遍历套装里的每一个散件，逐个塞进数据总线的字典里
        foreach (var item in set.items)
        {
            // SetSkin 会自动覆盖字典中相同 slot 的旧装备
            appearance.SetSkin(item);
        }
        // 全部塞完后，统一调用一次渲染刷新
        UpdateAppearance();
    }
    private void UpdateAppearance()
    {
        applicator.ApplySkins(appearance.GetAllEquipped());
    }
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterBaseStatsSO.cs`

```cs
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterBaseStats",menuName = "ScriptableObject/角色/角色基础数值")]
public class CharacterBaseStatsSO : ScriptableObject
{
    public StatSheet baseStats = new StatSheet();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (baseStats == null)
            baseStats = new StatSheet();

        baseStats.EnsureCompleteAndUnique();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterLevelGrowthSO.cs`

```cs
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterLevelGrowth", menuName = "ScriptableObject/角色/角色等级成长")]
public class CharacterLevelGrowthSO : ScriptableObject
{
    public StatSheet growthPerLevel = new StatSheet();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (growthPerLevel == null)
            growthPerLevel = new StatSheet();

        growthPerLevel.EnsureCompleteAndUnique();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatProfileSO.cs`

```cs
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStatProfile",menuName = "ScriptableObject/角色/角色数值档案")]
public class CharacterStatProfileSO : ScriptableObject
{
    public CharacterBaseStatsSO baseStats;
    public CharacterLevelGrowthSO levelGrowth;
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/CharacterStatsController.cs`

```cs
using PixelCrushers.DialogueSystem.Articy.Articy_4_0;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CharacterStatsController : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private CharacterDataBus dataBus;
    private Dictionary<CharacterStats, float> finalStats = new Dictionary<CharacterStats, float>();

    private List<StatsModifier> externalModifiers = new List<StatsModifier>();

    private bool _isInitialized;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;
        dataBus = root.characterDataBus;

        //if (dataBus.currentWeapon == null && dataBus.initialWeaponConfig != null)
        //{
        //    dataBus.currentWeapon = new WeaponRuntimeData(dataBus.initialWeaponConfig);
        //}

        RecalculateStats();

        DebugPrintStatBreakdownFromContextMenu();

        _isInitialized = true;
    }

    public float GetStat(CharacterStats statType)
    {
        if (finalStats.TryGetValue(statType, out float value))
            return value;

        return 0f;
    }

    public void AddModifier(StatsModifier modifier)
    {
        if (modifier == null)
            return;

        externalModifiers.Add(modifier);
        RecalculateStats();
    }

    public void RemoveModifiersFromSource(object source)
    {
        externalModifiers.RemoveAll(m => m.source == source);
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        List<StatsModifier> allModifiers = new List<StatsModifier>();

        CollectCharacterBaseStats(allModifiers);
        CollectLevelGrowthStats(allModifiers);
        CollectWeaponStats(allModifiers);
        CollectWeaponRuntimeModifiers(allModifiers);
        CollectExternalModifiers(allModifiers);

        BuildFinalStats(allModifiers);
    }

    private void CollectCharacterBaseStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.statProfile == null)
            return;

        if (dataBus.statProfile.baseStats == null)
            return;

        result.AddRange(dataBus.statProfile.baseStats.baseStats.ToAddModifiers(dataBus.statProfile.baseStats));
    }

    private void CollectLevelGrowthStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.statProfile == null)
            return;

        if (dataBus.statProfile.levelGrowth == null)
            return;

        if (dataBus.statProfile.levelGrowth.growthPerLevel == null)
            return;

        int level = Mathf.Max(1, dataBus.level);
        int growthCount = level - 1;

        StatSheet growth = dataBus.statProfile.levelGrowth.growthPerLevel;

        IReadOnlyList<StatsEntry> entries = growth.Values;

        for (int i = 0; i < entries.Count; i++)
        {
            StatsEntry entry = entries[i];

            result.Add(new StatsModifier(
                entry.CharacterStats,
                StatModifierMode.Add,
                entry.Value * growthCount,
                dataBus.statProfile.levelGrowth
            ));
        }
    }

    private void CollectWeaponStats(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.currentWeaponRuntimeData == null)
            return;

        if (dataBus.currentWeaponRuntimeData.Stats == null)
            return;

        result.AddRange(dataBus.currentWeaponRuntimeData.Stats.ToCharacterStatModifiers(dataBus.currentWeaponRuntimeData.Stats));
    }
    private void CollectWeaponRuntimeModifiers(List<StatsModifier> result)
    {
        if (dataBus == null)
            return;

        if (dataBus.currentWeaponRuntimeData == null)
            return;

        result.AddRange(dataBus.currentWeaponRuntimeData.runtimeModifiers);
    }

    private void CollectExternalModifiers(List<StatsModifier> result)
    {
        result.AddRange(externalModifiers);
    }

    private void BuildFinalStats(List<StatsModifier> modifiers)
    {
        finalStats.Clear();

        foreach (CharacterStats statType in System.Enum.GetValues(typeof(CharacterStats)))
        {
            if (ShouldSkipStat(statType))
                continue;

            float add = 0f;
            float percentAdd = 0f;
            float multiply = 1f;

            bool hasOverride = false;
            float overrideValue = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatsModifier modifier = modifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.characterStats != statType)
                    continue;

                switch (modifier.mode)
                {
                    case StatModifierMode.Add:
                        add += modifier.value;
                        break;

                    case StatModifierMode.PercentAdd:
                        percentAdd += modifier.value;
                        break;

                    case StatModifierMode.Multiply:
                        multiply *= modifier.value;
                        break;

                    case StatModifierMode.Override:
                        hasOverride = true;
                        overrideValue = modifier.value;
                        break;
                }
            }

            float finalValue = add * (1f + percentAdd) * multiply;

            if (hasOverride)
                finalValue = overrideValue;

            finalStats[statType] = finalValue;
        }
    }

    private bool ShouldSkipStat(CharacterStats statType)
    {
        string name = statType.ToString();

        if (name == "None")
            return true;

        if (name == "Invalid")
            return true;

        if (name == "Count")
            return true;

        return false;
    }


    [ContextMenu("Debug/Print Stat Breakdown")]
    public void DebugPrintStatBreakdownFromContextMenu()
    {
        DebugPrintStatBreakdown();
    }

    public void DebugPrintStatBreakdown()
    {
        List<StatsModifier> allModifiers = new List<StatsModifier>();

        CollectCharacterBaseStats(allModifiers);
        CollectLevelGrowthStats(allModifiers);
        CollectWeaponStats(allModifiers);
        CollectWeaponRuntimeModifiers(allModifiers);
        CollectExternalModifiers(allModifiers);

        BuildFinalStats(allModifiers);

        StringBuilder sb = new StringBuilder();

        string characterName = "Unknown";

        if (dataBus != null && !string.IsNullOrEmpty(dataBus.characterName))
            characterName = dataBus.characterName;
        else if (characterRoot != null)
            characterName = characterRoot.name;

        sb.AppendLine("========== Character Stat Breakdown ==========");
        sb.AppendLine($"Character: {characterName}");

        if (dataBus != null)
        {
            sb.AppendLine($"Level: {dataBus.level}");

            if (dataBus.currentWeaponRuntimeData != null &&
                dataBus.currentWeaponRuntimeData.config != null)
                sb.AppendLine($"Weapon: {dataBus.currentWeaponRuntimeData.config.weaponName}");
            else
                sb.AppendLine("Weapon: null");
        }

        sb.AppendLine("----------------------------------------------");

        foreach (CharacterStats statType in System.Enum.GetValues(typeof(CharacterStats)))
        {
            float add = 0f;
            float percentAdd = 0f;
            float multiply = 1f;

            bool hasAny = false;
            bool hasOverride = false;
            float overrideValue = 0f;

            sb.AppendLine($"[{statType}]");

            for (int i = 0; i < allModifiers.Count; i++)
            {
                StatsModifier modifier = allModifiers[i];

                if (modifier == null)
                    continue;

                if (modifier.characterStats != statType)
                    continue;

                hasAny = true;

                sb.AppendLine(
                    $"  - {modifier.mode} {modifier.value} | Source: {GetModifierSourceName(modifier.source)}"
                );

                switch (modifier.mode)
                {
                    case StatModifierMode.Add:
                        add += modifier.value;
                        break;

                    case StatModifierMode.PercentAdd:
                        percentAdd += modifier.value;
                        break;

                    case StatModifierMode.Multiply:
                        multiply *= modifier.value;
                        break;

                    case StatModifierMode.Override:
                        hasOverride = true;
                        overrideValue = modifier.value;
                        break;
                }
            }

            if (!hasAny)
            {
                sb.AppendLine("  no modifiers");
                sb.AppendLine($"  Final: {GetStat(statType)}");
                continue;
            }

            float calculated = add * (1f + percentAdd) * multiply;

            if (hasOverride)
                calculated = overrideValue;

            sb.AppendLine($"  AddSum: {add}");
            sb.AppendLine($"  PercentAddSum: {percentAdd}");
            sb.AppendLine($"  MultiplyProduct: {multiply}");

            if (hasOverride)
                sb.AppendLine($"  Override: {overrideValue}");

            sb.AppendLine($"  Final: {calculated}");
            sb.AppendLine();
        }

        sb.AppendLine("==============================================");

        Debug.Log(sb.ToString(), this);
    }

    private string GetModifierSourceName(object source)
    {
        if (source == null)
            return "null";

        if (source is Object unityObject)
            return unityObject.name + $" ({unityObject.GetType().Name})";

        return source.GetType().Name;
    }


















}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/StatsEntry.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StatsEntry
{
    [SerializeField]
    private CharacterStats characterStats;

    [SerializeField]
    private float value;

    public CharacterStats CharacterStats => characterStats;

    public float Value
    {
        get => value;
        set => this.value = value;
    }

    public StatsEntry(CharacterStats characterStats, float value)
    {
        this.characterStats = characterStats;
        this.value = value;
    }
}

[Serializable]
public class StatSheet
{
    [SerializeField]
    private List<StatsEntry> values = new List<StatsEntry>();

    public IReadOnlyList<StatsEntry> Values => values;

    public float GetValue(CharacterStats type)
    {
        EnsureCompleteAndUnique();

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i].CharacterStats == type)
                return values[i].Value;
        }

        return 0f;
    }

    public List<StatsModifier> ToAddModifiers(object source)
    {
        EnsureCompleteAndUnique();

        List<StatsModifier> result = new List<StatsModifier>();

        for (int i = 0; i < values.Count; i++)
        {
            StatsEntry entry = values[i];

            result.Add(new StatsModifier(
                entry.CharacterStats,
                StatModifierMode.Add,
                entry.Value,
                source
            ));
        }

        return result;
    }

    public void EnsureCompleteAndUnique()
    {
        Dictionary<CharacterStats, float> oldValueMap = new Dictionary<CharacterStats, float>();

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                StatsEntry entry = values[i];

                if (entry == null)
                    continue;

                CharacterStats statType = entry.CharacterStats;

                if (ShouldSkipStat(statType))
                    continue;

                if (!oldValueMap.ContainsKey(statType))
                    oldValueMap.Add(statType, entry.Value);
            }
        }

        if (values == null)
            values = new List<StatsEntry>();

        values.Clear();

        Array statTypes = Enum.GetValues(typeof(CharacterStats));

        for (int i = 0; i < statTypes.Length; i++)
        {
            CharacterStats statType = (CharacterStats)statTypes.GetValue(i);

            if (ShouldSkipStat(statType))
                continue;

            float oldValue = 0f;

            if (oldValueMap.TryGetValue(statType, out float cachedValue))
                oldValue = cachedValue;

            values.Add(new StatsEntry(statType, oldValue));
        }
    }

    private bool ShouldSkipStat(CharacterStats statType)
    {
        string name = statType.ToString();

        if (name == "None")
            return true;

        if (name == "Invalid")
            return true;

        if (name == "Count")
            return true;

        return false;
    }
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/StatsModifier.cs`

```cs
using System;
using UnityEngine;

[Serializable]
public class StatsModifier
{
    public CharacterStats characterStats;
    public StatModifierMode mode;
    public float value;

    [NonSerialized]
    public object source;
    public StatsModifier() { }

    public StatsModifier(CharacterStats statType, StatModifierMode mode, float value, object source = null)
    {
        this.characterStats = statType;
        this.mode = mode;
        this.value = value;
        this.source = source;
    }
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/FirearmWeaponStats.cs`

```cs
using System;
using UnityEngine;

[Serializable]
public class FirearmWeaponStats
{
    [Header("射击")]
    [Tooltip("射程")]
    [Min(0f)]
    public float range = 30f;

    [Tooltip("子弹 / 射线速度。Hitscan 武器可以不用。")]
    [Min(0f)]
    public float projectileSpeed = 60f;

    [Header("弹药")]
    [Min(1)]
    public int magazineSize = 30;

    [Min(0f)]
    public float reloadTime = 1.2f;

    [Header("散布 / 精度")]
    [Tooltip("基础散布角度，单位：度")]
    [Min(0f)]
    public float spreadAngle = 1.5f;

    [Tooltip("移动时额外散布")]
    [Min(0f)]
    public float movingSpreadAdd = 1.0f;

    [Tooltip("连续射击时每发增加的散布")]
    [Min(0f)]
    public float spreadIncreasePerShot = 0.2f;

    [Tooltip("最大散布")]
    [Min(0f)]
    public float maxSpreadAngle = 5f;

    [Header("后坐力")]
    [Min(0f)]
    public float recoil = 1f;

    [Min(0f)]
    public float recoilRecoverSpeed = 8f;

    [Header("瞄准")]
    [Tooltip("开镜/进入瞄准状态的速度")]
    [Min(0f)]
    public float aimEnterTime = 0.15f;

    [Tooltip("瞄准时移动速度倍率")]
    [Range(0f, 1f)]
    public float aimMoveSpeedMultiplier = 0.65f;
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/MeleeWeaponStats.cs`

```cs
using System;
using UnityEngine;

[Serializable]
public class MeleeWeaponStats
{
    [Header("武器性能")]
    [Min(0.01f)]
    public float attackSpeed = 1f;

    [Header("资源")]
    [Min(0f)]
    public float staminaEfficiency = 1f;

    [Header("判定修正")]
    [Min(0f)]
    public float rangeMultiplier = 1f;

    [Min(0f)]
    public float hitboxScaleMultiplier = 1f;
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCharacterBonusStats.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponCharacterBonusStats
{
    [Header("生存加成")]
    public float maxHpAdd;
    public float defenseAdd;

    [Header("移动加成")]
    public float moveSpeedAdd;
    public float moveSpeedPercentAdd;

    [Header("暴击加成")]
    public float critChanceAdd;
    public float critDamageAdd;

    [Header("枪械能力加成")]
    public float firearmDamagePercentAdd;
    public float fireRatePercentAdd;
    public float reloadSpeedPercentAdd;
    public float recoilControlAdd;
    public float accuracyPercentAdd;

    [Header("近战能力加成")]
    public float meleeDamagePercentAdd;
    public float meleeAttackSpeedPercentAdd;

    public void AppendModifiers(List<StatsModifier> result, object source)
    {
        if (result == null)
            return;

        AddIfNotZero(result, CharacterStats.MaxHP, StatModifierMode.Add, maxHpAdd, source);
        AddIfNotZero(result, CharacterStats.Defense, StatModifierMode.Add, defenseAdd, source);

        AddIfNotZero(result, CharacterStats.MoveSpeed, StatModifierMode.Add, moveSpeedAdd, source);
        AddIfNotZero(result, CharacterStats.MoveSpeed, StatModifierMode.PercentAdd, moveSpeedPercentAdd, source);

        AddIfNotZero(result, CharacterStats.CritChanceBonus, StatModifierMode.Add, critChanceAdd, source);
        AddIfNotZero(result, CharacterStats.CritDamageBonus, StatModifierMode.Add, critDamageAdd, source);

        AddIfNotZero(result, CharacterStats.FirearmDamageBonus, StatModifierMode.PercentAdd, firearmDamagePercentAdd, source);
        AddIfNotZero(result, CharacterStats.FireRateBonus, StatModifierMode.PercentAdd, fireRatePercentAdd, source);
        AddIfNotZero(result, CharacterStats.ReloadSpeedBonus, StatModifierMode.PercentAdd, reloadSpeedPercentAdd, source);
        AddIfNotZero(result, CharacterStats.RecoilControl, StatModifierMode.Add, recoilControlAdd, source);
        AddIfNotZero(result, CharacterStats.AccuracyBonus, StatModifierMode.PercentAdd, accuracyPercentAdd, source);

        AddIfNotZero(result, CharacterStats.MeleeDamageBonus, StatModifierMode.PercentAdd, meleeDamagePercentAdd, source);
        AddIfNotZero(result, CharacterStats.MeleeAttackSpeedBonus, StatModifierMode.PercentAdd, meleeAttackSpeedPercentAdd, source);
    }

    private void AddIfNotZero(
        List<StatsModifier> result,
        CharacterStats stat,
        StatModifierMode mode,
        float value,
        object source
    )
    {
        if (Mathf.Approximately(value, 0f))
            return;

        result.Add(new StatsModifier(stat, mode, value, source));
    }
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponCommonCoreStats.cs`

```cs
using UnityEngine;

[System.Serializable]
public class WeaponCommonCoreStats
{
    [Header("基础伤害")]
    [Min(0f)]
    public float damage = 10f;

    [Header("暴击基础值")]
    [Range(0f, 1f)]
    public float critChance = 0f;

    [Min(1f)]
    public float critDamage = 1.5f;

    [Header("削韧 / 击退")]
    [Min(0f)]
    public float poiseDamage = 0f;

    [Min(0f)]
    public float knockbackForce = 0f;
}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponRuntimeData.cs`

```cs
using System.Collections.Generic;
using UnityEngine;


//WeaponRuntimeData(config)
//  ->Stats
//  ->FirearmStats
//  ->ActionProfile
//  ->FirearmActionProfile


public struct FirearmFinalStats
{
    public float damage;
    public float critChance;
    public float critDamage;
    public float poiseDamage;
    public float knockbackForce;

    public float range;
    public int magazineSize;
    public float reloadTime;
    public float projectileSpeed;
    public float spreadAngle;
    public float recoil;
}
public class WeaponRuntimeData
{
    public WeaponConfigSO config;
    public WeaponRoot weaponRoot;

    public List<StatsModifier> runtimeModifiers = new List<StatsModifier>();

    public WeaponRuntimeData(WeaponConfigSO config)
    {
        this.config = config;
    }

    public WeaponType WeaponType
    {
        get
        {
            if (ActionProfile != null)
                return ActionProfile.WeaponType;

            if (config != null)
                return config.weaponType;

            return WeaponType.Melee;
        }
    }

    public bool IsFirearm => WeaponType == WeaponType.Firearm;
    public bool IsMelee => WeaponType == WeaponType.Melee;


    public WeaponStatsSO Stats
    {
        get
        {
            if (config == null)
                return null;

            return config.stats;
        }
    }

    public WeaponCommonCoreStats CommonStats
    {
        get
        {
            if (Stats == null)
                return null;

            return Stats.commonCoreStats;
        }
    }

    public FirearmWeaponStats FirearmStats
    {
        get
        {
            if (Stats == null)
                return null;

            if (!Stats.IsFirearm)
                return null;

            return Stats.firearmStats;
        }
    }

    public MeleeWeaponStats MeleeStats
    {
        get
        {
            if (Stats == null)
                return null;

            if (!Stats.IsMelee)
                return null;

            return Stats.meleeStats;
        }
    }

    public AnimationPlanOverrideRuleSetSO AnimationOverride
    {
        get
        {
            if (config == null)
                return null;

            return config.animationOverride;
        }
    }

    public WeaponActionProfileSO ActionProfile
    {
        get
        {
            if (config == null)
                return null;

            return config.weaponActionProfile;
        }
    }
    public WeaponAudioProfileSO AudioProfile
    {
        get
        {
            if (config == null)
                return null;

            return config.audioProfile;
        }
    }
    public FirearmActionProfileSO FirearmActionProfile
    {
        get
        {
            return ActionProfile as FirearmActionProfileSO;
        }
    }

    public float GetFinalFireInterval(GunFireModeConfig fireModeConfig, CharacterStatsController statsController)
    {
        if (fireModeConfig == null)
            return 0.15f;

        float fireRateBonus = statsController != null
            ? statsController.GetStat(CharacterStats.FireRateBonus)
            : 0f;

        return fireModeConfig.fireInterval / Mathf.Max(0.01f, 1f + fireRateBonus);
    }














    public WeaponRoot WeaponRoot => weaponRoot;


    public FirearmFinalStats BuildFirearmFinalStats(CharacterStatsController statsController)
    {
        FirearmFinalStats result = new FirearmFinalStats();

        if (Stats == null || !Stats.IsFirearm)
            return result;

        WeaponCommonCoreStats core = Stats.commonCoreStats;
        FirearmWeaponStats firearm = Stats.firearmStats;

        if (core == null || firearm == null)
            return result;

        float firearmDamageBonus = statsController != null
            ? statsController.GetStat(CharacterStats.FirearmDamageBonus)
            : 0f;

        float reloadSpeedBonus = statsController != null
            ? statsController.GetStat(CharacterStats.ReloadSpeedBonus)
            : 0f;

        float recoilControl = statsController != null
            ? statsController.GetStat(CharacterStats.RecoilControl)
            : 0f;

        float accuracyBonus = statsController != null
            ? statsController.GetStat(CharacterStats.AccuracyBonus)
            : 0f;

        float critChanceBonus = statsController != null
            ? statsController.GetStat(CharacterStats.CritChanceBonus)
            : 0f;

        float critDamageBonus = statsController != null
            ? statsController.GetStat(CharacterStats.CritDamageBonus)
            : 0f;

        float projectileSpeedBonus = statsController != null
            ? statsController.GetStat(CharacterStats.ProjectileSpeedBonus)
            : 0f;

        result.damage = core.damage * (1f + firearmDamageBonus);
        result.critChance = core.critChance + critChanceBonus;
        result.critDamage = core.critDamage + critDamageBonus;
        result.poiseDamage = core.poiseDamage;
        result.knockbackForce = core.knockbackForce;

        result.range = firearm.range;
        result.magazineSize = firearm.magazineSize;
        result.reloadTime = firearm.reloadTime / Mathf.Max(0.01f, 1f + reloadSpeedBonus);
        result.projectileSpeed = firearm.projectileSpeed * (1f + projectileSpeedBonus);
        result.spreadAngle = firearm.spreadAngle / Mathf.Max(0.01f, 1f + accuracyBonus);
        result.recoil = firearm.recoil * Mathf.Max(0f, 1f - recoilControl);

        return result;
    }




}
```

### `Assets/Script/Character/Core/Systems/StatsSystem/WeaponStats/WeaponStatsSO.cs`

```cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponStats", menuName = "ScriptableObject/武器/武器数值")]
public class WeaponStatsSO : ScriptableObject
{
    [Header("武器类型")]
    public WeaponType weaponType = WeaponType.Firearm;

    [Header("武器核心硬数值")]
    public WeaponCommonCoreStats commonCoreStats = new WeaponCommonCoreStats();

    [Header("装备后对角色的加成")]
    public WeaponCharacterBonusStats characterBonusStats = new WeaponCharacterBonusStats();

    [Header("枪械硬数值")]
    public FirearmWeaponStats firearmStats = new FirearmWeaponStats();

    [Header("近战硬数值")]
    public MeleeWeaponStats meleeStats = new MeleeWeaponStats();

    public bool IsFirearm => weaponType == WeaponType.Firearm;
    public bool IsMelee => weaponType == WeaponType.Melee;

    public List<StatsModifier> ToCharacterStatModifiers(object source)
    {
        List<StatsModifier> result = new List<StatsModifier>();

        if (characterBonusStats != null)
            characterBonusStats.AppendModifiers(result, source);

        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (commonCoreStats == null)
            commonCoreStats = new WeaponCommonCoreStats();

        if (characterBonusStats == null)
            characterBonusStats = new WeaponCharacterBonusStats();

        if (firearmStats == null)
            firearmStats = new FirearmWeaponStats();

        if (meleeStats == null)
            meleeStats = new MeleeWeaponStats();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
```
