using Ossastra.Characters;
using SliceActor = Ossastra.Characters.CharacterRoot;
using UnityEngine;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SVSliceElevatorCall : SVSliceWorldInteractable
    {
        public SVSliceElevator elevator;
        public bool lowerLanding;

        public override string Prompt => "呼叫升降机";
        protected override bool CanUse(SliceActor actor) => elevator != null && elevator.CanCall(lowerLanding, actor);

        public override void Use(SliceActor actor)
        {
            if (IsAccessible(actor)) elevator.Call(lowerLanding);
        }
    }
}