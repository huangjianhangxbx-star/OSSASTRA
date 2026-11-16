using Ossastra.Characters;
using SliceActor = Ossastra.Characters.CharacterRoot;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.SideView.Slice
{
    public abstract class SVSliceWorldInteractable : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float interactionRange = 2.7f;
        public abstract string Prompt { get; }

        public bool IsAccessible(SliceActor actor)
        {
            return isActiveAndEnabled && actor != null && actor.TryGetComponent<CombatVitals>(out var vitals) && vitals.IsAlive &&
                (actor.transform.position - transform.position).sqrMagnitude <= interactionRange * interactionRange &&
                actor.TryGetComponent<CharacterMovement>(out var mover) && mover.Grounded && CanUse(actor);
        }

        protected virtual bool CanUse(SliceActor actor) => true;
        public abstract void Use(SliceActor actor);
    }
}