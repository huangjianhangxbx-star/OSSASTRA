using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MeleeAttackStep
{
    [Header("动画语义")]
    public EAnimSemantic semantic =
        EAnimSemantic.Attack_Normal_1;

    [Header("动画结束保底")]
    [Min(0.05f)]
    public float fallbackDuration = 0.5f;

    [Header("连段")]
    public bool allowInputBuffer = true;

    [Min(0f)]
    public float fallbackComboWindowDuration = 0.35f;

    [Header("事件名称")]
    public string hitOpenEvent = "HitOpen";
    public string hitCloseEvent = "HitClose";
    public string comboOpenEvent = "ComboOpen";
    public string comboCloseEvent = "ComboClose";
    public string cancelOpenEvent = "CancelOpen";
    public string cancelCloseEvent = "CancelClose";
    public string actionEndEvent = "ActionEnd";

    [Header("命中窗口")]
    public List<MeleeHitWindowDefinition> hitWindows = new List<MeleeHitWindowDefinition>();

    [Header("表现配置")]
    public AudioEventSO swingAudio;

    [Min(0f)]
    public float hitStopDuration;

    public void Validate()
    {
        fallbackDuration =
             Mathf.Max(0.05f, fallbackDuration);

        fallbackComboWindowDuration =
            Mathf.Max(
                0f,
                fallbackComboWindowDuration
            );

        hitStopDuration =
            Mathf.Max(0f, hitStopDuration);

        if (hitWindows == null)
        {
            hitWindows =
                new List<MeleeHitWindowDefinition>();
        }
    }
}