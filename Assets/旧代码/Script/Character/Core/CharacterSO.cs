using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "ScriptableObject/��ɫ/��ɫ����")]
public class CharacterSO : ScriptableObject
{
    //[AssetSelector(AssetPreview = true)]
    //[PreviewField(100, ObjectFieldAlignment.Left)]
    //[HideLabel]
    [PreviewField(80)] // �ص㣺������ Odin ���κεط����ܿ���ͼ��ǰ��
    public Sprite characterIcon;

    //==========���ݽ���=============
    //[LabelText("��ɫstats")]
    public CharacterStatProfileSO characterStatProfileSO;//��ɫ��������


    //[LabelText("��������")]
    public SkeletonDataAsset chracterModel;//Spineģ������



    //===========�������ݽ���===================

    [AssetSelector]
    public AnimationPlanMappingSO basicAnimationPlanMapping;

    [AssetSelector]
    public List<AnimationPlanOverrideRuleSetSO> animationPlanOverrideRuleSet;



    //=============��ɫ����

    //[LabelText("��ɫ����")]
    public string characterName;

    [Header("�߼�����")]//�ṩ��ɫ��ר��������dataBus��ȡ

    public BattleCamp battleCamp;//��ɫ������Ӫ����Ҫ��SO����ָ��

    public Facing assetDefaultFacing = Facing.Left; // ������������Ĭ�ϳ���(��ֹ����ģ�巽��㷴��



    public float maxHp = 100f;//����ֵ����ʼֵ��SO����ָ����ս���и����ܻ��������

    public float maxRemainHp = 10f;


    //public bool isAlive;//�Ƿ����ʼֵΪtrue��ս���и����ܻ��������

    //[Header("��ʼ���ܼ�")]
    //public List<SkillSO> availableSkills; // ��ְҵ��ѧ������м���



    //[Header("��ʼװ��")]
    //public WeaponConfigSO initialWeaponConfig;










}
