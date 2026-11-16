using System.Linq;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed partial class CharacterAbilities
    {
        internal ISideViewAbilityMotion ActiveSideViewMotion
        {
            get
            {
                if (!isActiveAndEnabled || owner == null || !owner.HasPlayerInput || !owner.CanSimulateIntent)
                    return null;
                var actionId = owner.StateArbitrator.Combat.Runtime.ActionId;
                return runs.FirstOrDefault(r => r is ISideViewAbilityMotion && !r.Ended &&
                    r.Context.id == actionId && r.Context.lifeGeneration == owner.LifeGeneration &&
                    r.Context.source.IsAvailable) as ISideViewAbilityMotion;
            }
        }

        private bool OwnsSideViewDiveSource(IAbilitySource source)
            => TryGetComponent<SideViewDiveMount>(out var mount) && mount.Owns(source);

        internal bool ClaimSideViewDive(CharacterInputFrame frame)
        {
            if (!frame.diveInput.Requested || !frame.attackPressed ||
                frame.attackRequestsBlocked || frame.attackPointerBlocked ||
                owner == null || !owner.HasPlayerInput || owner.StateMovement?.IsSideView != true)
                return false;
            var motion = owner.StateArbitrator.Movement;
            if (!motion.IsAirborne && !motion.TraversalBlocksAttack) return false;
            if (!TryGetComponent<SideViewDiveMount>(out var mount) || !mount.isActiveAndEnabled ||
                !mount.PreviewEnabled || mount.Definition == null ||
                !(mount.Definition.executor is SideViewDiveAbilitySO executor))
                return false;

            if (!TryResolveDiveDirection(frame.diveInput, executor.MinimumDown, out var direction))
            {
                Reject("side-view-dive-invalid-direction");
                return true;
            }

            var source = mount.PrepareSource(owner, owner.transform.position + (Vector3)direction);
            TryRequestFromSource(mount.Definition, source, AbilityOperation.Contextual, CharacterActionSource.Player);
            return true;
        }

        private bool TryResolveDiveDirection(CharacterDiveInput input, float minimumDown, out Vector2 direction)
        {
            direction = default;
            if (!input.HasDirection) return false;
            var raw = input.Direction;
            if (input.FromPointer)
            {
                var camera = owner.StateViewCamera;
                if (camera == null) return false;
                var origin = owner.transform.position;
                var ray = SideViewCameraFollow.GameplayRay(camera, input.Pointer);
                if (!new Plane(Vector3.forward, origin).Raycast(ray, out var distance)) return false;
                raw = ray.GetPoint(distance) - origin;
            }

            if (!SideViewDiveRules.TryFreezeDirection(raw.x, raw.y, minimumDown, out var x, out var y)) return false;
            direction = new Vector2(x, y);
            return true;
        }
    }
}
