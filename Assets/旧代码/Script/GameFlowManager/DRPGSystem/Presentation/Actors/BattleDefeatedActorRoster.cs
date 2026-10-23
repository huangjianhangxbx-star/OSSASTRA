using System.Collections.Generic;
using UnityEngine;

internal readonly struct BattleIncapacitationBinding
{
    internal BattleIncapacitationBinding(ParticipantId participant, GameObject prefab, Transform anchor)
    { Participant = participant; Prefab = prefab; Anchor = anchor; }
    internal ParticipantId Participant { get; }
    internal GameObject Prefab { get; }
    internal Transform Anchor { get; }
}

// Battle-lifetime ownership, deliberately separate from active formation/pool.
public sealed class BattleDefeatedActorRoster : MonoBehaviour
{
    private sealed class Entry
    {
        internal GameObject Model;
        internal UnityEngine.UI.Graphic Marker;
        internal Camera Camera;
    }
    private readonly Dictionary<ParticipantId, BattleIncapacitationBinding> bindings = new Dictionary<ParticipantId, BattleIncapacitationBinding>();
    private readonly Dictionary<ParticipantId, Entry> models = new Dictionary<ParticipantId, Entry>();
    private readonly Dictionary<ParticipantId, BattleWorldActorView> spriteSources = new Dictionary<ParticipantId, BattleWorldActorView>();
    private readonly List<GameObject> ownedSpriteRoots = new List<GameObject>();
    internal bool RetireAfterRenderedTransfer { get; private set; }

    // Sprite-stage fallback uses each profile's authored broken portrait. These
    // independent anchors must never be children of an actor that will depart.
    internal void ConfigureSpriteParty(IEnumerable<BattleWorldActorView> actors)
    {
        ClearBattle();
        var prepared = new List<BattleIncapacitationBinding>();
        foreach (var actor in actors)
        {
            if (actor == null || actor.BoundActor == null || actor.BoundActor.IsEnemy) continue;
            var source = actor.GetComponentInChildren<SpriteRenderer>(true);
            if (source == null || source.sprite == null) continue;
            var sprite = actor.BoundActor.Profile.DamageBrokenPortrait;
            if (sprite == null) sprite = source.sprite;
            var id = new ParticipantId(actor.RuntimeId);
            var anchor = new GameObject("DefeatedHome_" + id.Value);
            anchor.hideFlags = HideFlags.DontSave;
            anchor.transform.position = actor.transform.position;
            ownedSpriteRoots.Add(anchor);
            var template = new GameObject("AuthoredBrokenSprite", typeof(SpriteRenderer));
            template.transform.SetParent(anchor.transform, false);
            var renderer = template.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.color = new Color(.3f, .3f, .3f, 1f);
            float scale = source.bounds.size.y / Mathf.Max(.001f, sprite.bounds.size.y);
            template.transform.localScale = Vector3.one * scale;
            template.transform.localPosition = new Vector3(-sprite.bounds.center.x * scale, -sprite.bounds.min.y * scale, 0);
            template.SetActive(false);
            prepared.Add(new BattleIncapacitationBinding(id, template, anchor.transform));
            spriteSources.Add(id, actor);
        }
        Configure(prepared);
        // These fallback bodies occupy active homes, not a separate corpse row.
        // Retire them before survivors may reuse those homes.
        RetireAfterRenderedTransfer = true;
    }

    internal void ClearBattle()
    {
        foreach (var id in new List<ParticipantId>(models.Keys)) RollbackTransfer(id);
        foreach (var root in ownedSpriteRoots)
        {
            if (root == null) continue;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
        ownedSpriteRoots.Clear(); spriteSources.Clear(); bindings.Clear();
    }
    internal void Configure(IReadOnlyList<BattleIncapacitationBinding> source)
    {
        if (models.Count != 0) throw new System.InvalidOperationException("Cannot replace live defeated roster bindings.");
        bindings.Clear();
        RetireAfterRenderedTransfer = false;
        foreach (var binding in source) bindings.Add(binding.Participant, binding);
    }
    internal int Count => models.Count;
    internal bool TryGet(ParticipantId participant, out GameObject model)
    { model = models.TryGetValue(participant, out var entry) ? entry.Model : null; return model != null; }

    internal bool CanTransfer(ParticipantId participant) => !models.ContainsKey(participant) &&
        bindings.TryGetValue(participant, out var binding) && binding.Prefab != null && binding.Anchor != null &&
        binding.Anchor.gameObject.activeInHierarchy && HasRenderableBody(binding.Prefab);

    internal bool HasIndependentAnchor(ParticipantId participant, DRPGBattleActionProjectionLease lease)
    {
        if (!bindings.TryGetValue(participant, out var binding) || binding.Anchor == null || !lease.IsActive) return false;
        foreach (var actor in lease.LockBaseline.Actors)
            if (lease.TryGetView(actor.RuntimeId, out var view) && binding.Anchor.IsChildOf(view.transform)) return false;
        return true;
    }

    internal GameObject Transfer(ParticipantId participant, UnityEngine.UI.Graphic marker, Camera camera, System.Func<bool> stillOwned)
    {
        if (!CanTransfer(participant)) throw new System.InvalidOperationException("Authored incap model/anchor is unavailable.");
        var binding = bindings[participant];
        if (spriteSources.TryGetValue(participant, out var source) && source != null)
            binding.Anchor.position = source.transform.position;
        var model = Instantiate(binding.Prefab, binding.Anchor, false);
        model.name = "Incapacitated_" + participant.Value;
        models.Add(participant, new Entry { Model = model, Marker = marker, Camera = camera });
        try
        {
            model.SetActive(true);
            if (!stillOwned()) throw new System.InvalidOperationException("Incap activation lost its projection owner.");
            LateUpdate(); return model;
        }
        catch { RollbackTransfer(participant, false); throw; }
    }

    internal bool IsVisible(ParticipantId participant, int modelId)
    {
        if (!models.TryGetValue(participant, out var entry) || entry.Model == null || entry.Model.GetInstanceID() != modelId ||
            !entry.Model.activeInHierarchy || entry.Marker == null || !entry.Marker.enabled ||
            !entry.Marker.gameObject.activeInHierarchy || entry.Marker.color.a < .95f) return false;
        float alpha = entry.Marker.color.a;
        foreach (var group in entry.Marker.GetComponentsInParent<CanvasGroup>())
        { alpha *= group.alpha; if (group.ignoreParentGroups) break; }
        return alpha >= .95f && HasRenderableBody(entry.Model);
    }

    private static bool HasRenderableBody(GameObject model)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            bool childEnabled = true;
            for (Transform child = renderer.transform; child != model.transform; child = child.parent)
                if (!child.gameObject.activeSelf) { childEnabled = false; break; }
            if (!childEnabled) continue;
            if (renderer is SpriteRenderer sprite)
            { if (sprite.sprite != null && sprite.color.a > .01f) return true; }
            else return true;
        }
        return false;
    }

    internal void RollbackTransfer(ParticipantId participant, bool destroyMarker = true)
    {
        if (!models.TryGetValue(participant, out var entry)) return;
        models.Remove(participant);
        if (destroyMarker) BattleImpactFeedbackPresenter.DestroyOwnedMarker(entry.Marker);
        if (entry.Model == null) return;
        entry.Model.SetActive(false);
        if (Application.isPlaying) Destroy(entry.Model); else DestroyImmediate(entry.Model);
    }

    private void LateUpdate()
    {
        foreach (var entry in models.Values)
            if (entry.Model != null)
                BattleImpactFeedbackPresenter.PositionWorldGraphic(entry.Marker, entry.Model.transform.position + Vector3.up * .25f, entry.Camera);
    }
    private void OnDestroy()
    {
        ClearBattle();
    }
}
