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



