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
        return copy;
    }
}
