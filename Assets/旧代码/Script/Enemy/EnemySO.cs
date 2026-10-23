using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "ScriptableObject/敌人/敌人创建")]
public class EnemySO : ScriptableObject
{

    [PreviewField(80)] // 重点：这是让 Odin 在任何地方都能看到图的前提
    public Sprite enemyIcon;

    //[LabelText("骨骼数据")]
    public SkeletonDataAsset enemyModel;//Spine模型数据

    //===========动画数据接入===================

    public AnimationPlanMappingSO basicAnimationPlanMapping;
    public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;



    //=============角色属性

    public string enemyName;

    [Header("逻辑属性")]//提供角色的专属属性由dataBus读取

    public EnemyRank enemyRank;//敌人等级，影响属性和技能，需要在SO具体指定

    public BattleCamp battleCamp;//所属阵营，需要在SO具体指定

    public Facing assetDefaultFacing = Facing.Right; // 美术画出来的默认朝向(防止美术模板方向搞反）


    public float maxHp = 100f;//生命值，初始值由SO具体指定，战斗中根据受击情况更新


    public EnemyRoot enemyPrefab;









}
