using Spine;
using Spine.Unity;
using System;
using UnityEngine;
public enum CharacterSpineSignalType
{
    EventReached,
    AnimationCompleted
}

public readonly struct CharacterSpineAnimationSignal
{
    public readonly CharacterSpineSignalType type;
    public readonly string animationName;
    public readonly string eventName;
    public readonly int trackIndex;
    public readonly float trackTime;

    public CharacterSpineAnimationSignal(
        CharacterSpineSignalType type,
        string animationName,
        string eventName,
        int trackIndex,
        float trackTime
    )
    {
        this.type = type;
        this.animationName = animationName;
        this.eventName = eventName;
        this.trackIndex = trackIndex;
        this.trackTime = trackTime;
    }
}
public class CharacterSpineEventSystem : MonoBehaviour, ICharacterSubsystem
{
    private CharacterRoot characterRoot;
    private SkeletonAnimation houndSkeletonAnimation;

    private bool isBound;
    private bool isInitialized;

    public event Action<CharacterSpineAnimationSignal> OnSpineSignal;

    public void Initialize(CharacterRoot root)
    {
        if (isInitialized)
            return;

        characterRoot = root;

        if (characterRoot == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] Initialize failed: characterRoot == null", this);
            return;
        }

        houndSkeletonAnimation = characterRoot.houndSkeletonAnimation;

        Bind();

        isInitialized = true;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        if (isBound)
            return;

        if (houndSkeletonAnimation == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] houndSkeletonAnimation == null", this);
            return;
        }

        if (houndSkeletonAnimation.AnimationState == null)
        {
            Debug.LogError("[CharacterSpineEventSystem] AnimationState == null", this);
            return;
        }

        houndSkeletonAnimation.AnimationState.Event += HandleSpineEvent;
        houndSkeletonAnimation.AnimationState.Complete += HandleSpineAnimationComplete;
        Debug.Log($"[SpineEvent] skeleton={houndSkeletonAnimation.name}, id={houndSkeletonAnimation.GetInstanceID()}", this);
        isBound = true;
    }

    private void Unbind()
    {
        if (!isBound)
            return;

        if (houndSkeletonAnimation != null && houndSkeletonAnimation.AnimationState != null)
        {
            houndSkeletonAnimation.AnimationState.Event -= HandleSpineEvent;
            houndSkeletonAnimation.AnimationState.Complete -= HandleSpineAnimationComplete;
        }

        isBound = false;
    }

    private void HandleSpineEvent(TrackEntry trackEntry, Spine.Event spineEvent)
    {
        if (trackEntry == null || spineEvent == null || spineEvent.Data == null)
            return;

        string animationName = trackEntry.Animation != null
            ? trackEntry.Animation.Name
            : string.Empty;

        CharacterSpineAnimationSignal signal = new CharacterSpineAnimationSignal(
            CharacterSpineSignalType.EventReached,
            animationName,
            spineEvent.Data.Name,
            trackEntry.TrackIndex,
            trackEntry.TrackTime
        );

        OnSpineSignal?.Invoke(signal);
    }

    private void HandleSpineAnimationComplete(TrackEntry trackEntry)
    {
        if (trackEntry == null || trackEntry.Animation == null)
            return;

        CharacterSpineAnimationSignal signal = new CharacterSpineAnimationSignal(
            CharacterSpineSignalType.AnimationCompleted,
            trackEntry.Animation.Name,
            null,
            trackEntry.TrackIndex,
            trackEntry.TrackTime
        );

        OnSpineSignal?.Invoke(signal);
    }
}