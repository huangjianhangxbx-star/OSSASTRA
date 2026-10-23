using UnityEngine;
public static class CombatTargetResolver
{
    public static GameObject ResolveTarget(
        Collider2D collider,
        out IDamageable damageable)
    {
        damageable = null;

        if (collider == null)
            return null;

        damageable =
            collider.GetComponentInParent<IDamageable>();

        if (damageable is Component component)
        {
            return component.gameObject;
        }

        if (collider.attachedRigidbody != null)
        {
            return collider.attachedRigidbody.gameObject;
        }

        return collider.transform.root.gameObject;
    }
}