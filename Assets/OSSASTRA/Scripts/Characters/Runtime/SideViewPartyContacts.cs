using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>横版会话中己方本体的成对物理排除，触发器和其他碰撞层仍使用原规则。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterControlCoordinator))]
    public sealed class SideViewPartyContacts : MonoBehaviour
    {
        private readonly List<Pair> pairs = new List<Pair>();
        private readonly List<CharacterMovement> members = new List<CharacterMovement>();

        private sealed class Pair
        {
            public CharacterController First;
            public CharacterController Second;
            public bool Owned;

            public bool Active => First != null && First.enabled && First.gameObject.activeInHierarchy &&
                Second != null && Second.enabled && Second.gameObject.activeInHierarchy;

            public bool Involves(Collider body) => First == body || Second == body;

            public bool Matches(Collider first, Collider second)
                => First == first && Second == second || First == second && Second == first;
        }

        private void OnEnable()
        {
            foreach (var actor in GetComponent<CharacterControlCoordinator>().Characters)
            {
                if (actor == null || !actor.TryGetComponent(out CharacterMovement movement) || !movement.IsSideView ||
                    !actor.TryGetComponent(out CharacterController body)) continue;
                foreach (var member in members)
                    pairs.Add(new Pair { First = member.GetComponent<CharacterController>(), Second = body });
                members.Add(movement);
                movement.SetSideViewContacts(this);
            }
        }

        internal void Prepare(CharacterController body)
        {
            foreach (var pair in pairs)
            {
                if (!pair.Involves(body) || !pair.Active || Physics.GetIgnoreCollision(pair.First, pair.Second)) continue;
                Physics.IgnoreCollision(pair.First, pair.Second);
                pair.Owned = true;
            }
        }

        internal bool IsPeer(Collider body, Collider candidate)
        {
            foreach (var pair in pairs)
                if (pair.Matches(body, candidate)) return true;
            return false;
        }

        private void OnDisable()
        {
            foreach (var member in members)
                if (member != null) member.SetSideViewContacts(null);
            foreach (var pair in pairs)
                if (pair.Owned && pair.Active && Physics.GetIgnoreCollision(pair.First, pair.Second))
                    Physics.IgnoreCollision(pair.First, pair.Second, false);
            members.Clear();
            pairs.Clear();
        }
    }
}
