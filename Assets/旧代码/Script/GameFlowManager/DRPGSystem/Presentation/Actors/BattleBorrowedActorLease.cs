using System;
using UnityEngine;

/// <summary>Restores a scene-owned actor after the battle pool releases it.</summary>
public sealed class BattleBorrowedActorLease : IDisposable
{
    private readonly BattleWorldActorView view;
    private readonly Transform parent;
    private readonly int siblingIndex;
    private Vector3 localPosition;
    private readonly Quaternion localRotation;
    private Vector3 localScale;
    private readonly bool activeSelf;
    private readonly RendererState[] renderers;
    private readonly Rigidbody2DState[] rigidbodies2D;
    private readonly RigidbodyState[] rigidbodies;
    private bool disposed;

    public BattleBorrowedActorLease(BattleWorldActorView actorView)
    {
        view = actorView != null
            ? actorView
            : throw new ArgumentNullException(nameof(actorView));
        Transform transform = view.transform;
        parent = transform.parent;
        siblingIndex = transform.GetSiblingIndex();
        localPosition = transform.localPosition;
        localRotation = transform.localRotation;
        localScale = transform.localScale;
        activeSelf = view.gameObject.activeSelf;
        Renderer[] found = view.GetComponentsInChildren<Renderer>(true);
        renderers = new RendererState[found.Length];
        for (int i = 0; i < found.Length; i++)
            renderers[i] = new RendererState(found[i]);
        Rigidbody2D[] found2D = view.GetComponentsInChildren<Rigidbody2D>(true);
        rigidbodies2D = new Rigidbody2DState[found2D.Length];
        for (int i = 0; i < found2D.Length; i++)
        {
            rigidbodies2D[i] = new Rigidbody2DState(found2D[i]);
            found2D[i].simulated = false;
        }
        Rigidbody[] found3D = view.GetComponentsInChildren<Rigidbody>(true);
        rigidbodies = new RigidbodyState[found3D.Length];
        for (int i = 0; i < found3D.Length; i++)
        {
            rigidbodies[i] = new RigidbodyState(found3D[i]);
            found3D[i].isKinematic = true;
        }
    }

    public BattleWorldActorView View => view;

    internal void CommitFormationHome()
    {
        if (disposed || view == null) return;
        localPosition = parent != null ? parent.InverseTransformPoint(view.transform.position) : view.transform.position;
        localScale = view.transform.localScale;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (view == null)
            return;
        Transform transform = view.transform;
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = localScale;
        if (parent != null)
            transform.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].Restore();
        view.gameObject.SetActive(activeSelf);
        for (int i = 0; i < rigidbodies2D.Length; i++)
            rigidbodies2D[i].Restore();
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].Restore();
    }

    private readonly struct Rigidbody2DState
    {
        private readonly Rigidbody2D body;
        private readonly bool simulated;
        private readonly Vector2 linearVelocity;
        private readonly float angularVelocity;

        internal Rigidbody2DState(Rigidbody2D source)
        {
            body = source;
            simulated = source != null && source.simulated;
            linearVelocity = source != null ? source.velocity : Vector2.zero;
            angularVelocity = source != null ? source.angularVelocity : 0f;
        }

        internal void Restore()
        {
            if (body == null)
                return;
            body.simulated = simulated;
            if (simulated)
            {
                body.velocity = linearVelocity;
                body.angularVelocity = angularVelocity;
            }
        }
    }

    private readonly struct RigidbodyState
    {
        private readonly Rigidbody body;
        private readonly bool isKinematic;
        private readonly Vector3 linearVelocity;
        private readonly Vector3 angularVelocity;

        internal RigidbodyState(Rigidbody source)
        {
            body = source;
            isKinematic = source != null && source.isKinematic;
            linearVelocity = source != null ? source.velocity : Vector3.zero;
            angularVelocity = source != null ? source.angularVelocity : Vector3.zero;
        }

        internal void Restore()
        {
            if (body == null)
                return;
            body.isKinematic = isKinematic;
            if (!isKinematic)
            {
                body.velocity = linearVelocity;
                body.angularVelocity = angularVelocity;
            }
        }
    }

    private readonly struct RendererState
    {
        private readonly Renderer renderer;
        private readonly bool enabled;
        private readonly int sortingLayerId;
        private readonly int sortingOrder;
        private readonly Sprite sprite;
        private readonly Color color;
        private readonly Vector3 localPosition;
        private readonly Vector3 localScale;
        private readonly Quaternion localRotation;

        internal RendererState(Renderer source)
        {
            renderer = source;
            localPosition = source.transform.localPosition;
            localScale = source.transform.localScale;
            localRotation = source.transform.localRotation;
            enabled = source != null && source.enabled;
            sortingLayerId = source != null ? source.sortingLayerID : 0;
            sortingOrder = source != null ? source.sortingOrder : 0;
            SpriteRenderer spriteRenderer = source as SpriteRenderer;
            sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            color = spriteRenderer != null ? spriteRenderer.color : Color.white;
        }

        internal void Restore()
        {
            if (renderer == null)
                return;
            renderer.transform.localPosition = localPosition;
            renderer.transform.localScale = localScale;
            renderer.transform.localRotation = localRotation;
            renderer.enabled = enabled;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            if (renderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = color;
            }
        }
    }
}
