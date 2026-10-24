/* AM02：迁自旧代码原实现；保留作者字段和分组，生产类型/GUID独立。 */
namespace Ossastra.CharacterAnimation
{
using CharacterRoot = Ossastra.Characters.CharacterRoot;
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class CharacterAnimationClip
{
    [ValueDropdown("GetAnimationNames")]
    [LabelText("Animation")]
    [LabelWidth(70)]
    public string animaName;

    [HorizontalGroup("Params", Width = 90)]
    [LabelWidth(40)]
    public int track = 0;

    [HorizontalGroup("Params", Width = 70)]
    [LabelWidth(35)]
    public bool loop = true;

    [HorizontalGroup("Params", Width = 140)]
    [LabelWidth(85)]
    public float mixDuration = 0f;

    [HorizontalGroup("Params", Width = 130)]
    [LabelWidth(85)]
    public bool isAdditive = false;

    [ShowInInspector, ReadOnly, LabelText("Skeleton")]
    [HideInInspector]
    public SkeletonDataAsset skeletonDataAsset;

    [HideInInspector] public CharacterRoot characterRoot;

    [System.NonSerialized]
    public AnimationIntentSourceStateMachine runtimeSourceStateMachine;

    [System.NonSerialized]
    public int runtimeReplayId;

    [Min(.001f)] public float playbackRate = 1;
    [Tooltip("仅显式勾选的动作轨按玩法阶段长度同步；普通姿态保持自身速率。")]
    public bool scalesWithAction;
    [Min(0)] public float sourceDuration;
    [System.NonSerialized] public System.Guid runtimeActionId, runtimePhaseId;
    [System.NonSerialized] public int runtimeGeneration;
    [System.NonSerialized] public int runtimeLifeGeneration;
    [System.NonSerialized] public string runtimeActor, runtimeTarget;
    [System.NonSerialized] public bool runtimeStructural;
    private IEnumerable<string> GetAnimationNames()
    {
        if (skeletonDataAsset == null)
            return Enumerable.Empty<string>();

        var data = skeletonDataAsset.GetSkeletonData(true);
        if (data == null)
            return Enumerable.Empty<string>();

        var names = new List<string>(data.Animations.Count);
        foreach (var anim in data.Animations)
            names.Add(anim.Name);
        names.Sort();
        return names;
    }

    public CharacterAnimationClip() { }

    public CharacterAnimationClip(string name, int track = 0)
    {
        this.animaName = name;
        this.track = track;
        this.loop = loop;
        this.mixDuration = mixDuration;
    }

    public CharacterAnimationClip Clone()
    {
        CharacterAnimationClip copy = new CharacterAnimationClip();

        copy.animaName = animaName;
        copy.track = track;
        copy.loop = loop;
        copy.mixDuration = mixDuration;
        copy.isAdditive = isAdditive;
        copy.skeletonDataAsset = skeletonDataAsset;
        copy.characterRoot = characterRoot;
        copy.runtimeSourceStateMachine = runtimeSourceStateMachine;
        copy.runtimeReplayId = runtimeReplayId;
        copy.playbackRate = playbackRate;
        copy.scalesWithAction = scalesWithAction; copy.sourceDuration = sourceDuration;
        copy.runtimeActionId = runtimeActionId; copy.runtimePhaseId = runtimePhaseId;
        copy.runtimeGeneration = runtimeGeneration; copy.runtimeActor = runtimeActor; copy.runtimeTarget = runtimeTarget;
        copy.runtimeLifeGeneration = runtimeLifeGeneration;
        copy.runtimeStructural = runtimeStructural;
        return copy;
    }
}

}
