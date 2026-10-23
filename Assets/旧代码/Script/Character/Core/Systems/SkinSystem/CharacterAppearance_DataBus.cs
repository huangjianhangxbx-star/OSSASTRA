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
