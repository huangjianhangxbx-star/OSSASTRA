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
