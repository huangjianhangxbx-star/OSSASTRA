using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

public sealed class BattleSpineEventSourceAdapter :
    MonoBehaviour,
    IBattleSpineEventSource
{
    [SerializeField] private int runtimeId;
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    private event Action<BattleSpineMarker> MarkerRaised;
    private bool subscribed;
    private readonly Dictionary<TrackEntry, BoundPlayback> boundPlaybacks = new Dictionary<TrackEntry, BoundPlayback>();

    public int RuntimeId => runtimeId;

    internal IDisposable BindPlayback(TrackEntry entry, AnimationPlaybackIdentity identity, Action<BattleAnimationMarker> onMarker)
    {
        if (entry == null || !identity.IsValid || onMarker == null)
            throw new ArgumentException("A real TrackEntry, exact identity and marker observer are required.");
        if (boundPlaybacks.ContainsKey(entry))
            throw new InvalidOperationException("TrackEntry already belongs to a playback.");
        var binding = new BoundPlayback(this, entry, identity, onMarker);
        boundPlaybacks.Add(entry, binding);
        return binding;
    }

    public void BindRuntimeId(int value)
    {
        runtimeId = Mathf.Max(0, value);
    }

    public IDisposable Subscribe(Action<BattleSpineMarker> onMarker)
    {
        if (onMarker == null)
            throw new ArgumentNullException(nameof(onMarker));
        MarkerRaised += onMarker;
        return new Subscription(this, onMarker);
    }

    public void EmitForTesting(string markerName, int markerIndex = 0)
    {
        MarkerRaised?.Invoke(new BattleSpineMarker(markerName, markerIndex));
    }

    private void OnEnable()
    {
        ResolveSkeleton();
        SubscribeSkeleton();
    }

    private void OnDisable()
    {
        UnsubscribeSkeleton();
        foreach (var binding in new List<BoundPlayback>(boundPlaybacks.Values))
            binding.ReleaseEntry();
    }

    private void ResolveSkeleton()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
    }

    private void SubscribeSkeleton()
    {
        if (subscribed || skeletonAnimation == null ||
            skeletonAnimation.AnimationState == null)
        {
            return;
        }
        skeletonAnimation.AnimationState.Event += OnSpineEvent;
        subscribed = true;
    }

    private void UnsubscribeSkeleton()
    {
        if (!subscribed || skeletonAnimation == null ||
            skeletonAnimation.AnimationState == null)
        {
            subscribed = false;
            return;
        }
        skeletonAnimation.AnimationState.Event -= OnSpineEvent;
        subscribed = false;
    }

    private void OnSpineEvent(TrackEntry entry, Spine.Event spineEvent)
    {
        if (spineEvent?.Data == null || entry != null && boundPlaybacks.ContainsKey(entry))
            return;
        MarkerRaised?.Invoke(new BattleSpineMarker(
            spineEvent.Data.Name,
            Math.Max(0, spineEvent.Int)));
    }

    private void Remove(Action<BattleSpineMarker> callback)
    {
        MarkerRaised -= callback;
    }

    private sealed class BoundPlayback : IDisposable
    {
        private BattleSpineEventSourceAdapter owner;
        private TrackEntry entry;
        private readonly AnimationPlaybackIdentity identity;
        private Action<BattleAnimationMarker> observer;
        internal BoundPlayback(BattleSpineEventSourceAdapter owner, TrackEntry entry,
            AnimationPlaybackIdentity identity, Action<BattleAnimationMarker> observer)
        {
            this.owner = owner; this.entry = entry; this.identity = identity; this.observer = observer;
            entry.Event += OnEvent;
            entry.Dispose += OnEntryDisposed;
        }
        private void OnEvent(TrackEntry source, Spine.Event value)
        {
            if (ReferenceEquals(source, entry) && value?.Data != null)
                observer?.Invoke(new BattleAnimationMarker(identity, value.Data.Name, value.Int));
        }
        public void Dispose()
        {
            if (entry != null) entry.Event -= OnEvent;
            observer = null;
            // Retain the legacy suppression tag through this entry's lifetime,
            // including the global AnimationState.Event fired after this callback.
        }
        private void OnEntryDisposed(TrackEntry source) { ReleaseEntry(); }
        internal void ReleaseEntry()
        {
            Dispose();
            if (entry != null)
            {
                entry.Dispose -= OnEntryDisposed;
                owner.boundPlaybacks.Remove(entry);
            }
            entry = null; owner = null;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private BattleSpineEventSourceAdapter owner;
        private Action<BattleSpineMarker> callback;

        public Subscription(
            BattleSpineEventSourceAdapter owner,
            Action<BattleSpineMarker> callback)
        {
            this.owner = owner;
            this.callback = callback;
        }

        public void Dispose()
        {
            owner?.Remove(callback);
            owner = null;
            callback = null;
        }
    }
}
