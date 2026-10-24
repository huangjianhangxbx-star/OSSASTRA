using System;
using Ossastra.AnimationCore;
using Spine.Unity;
namespace Ossastra.Characters
{
    public interface ICharacterDefaultActionAnimation
    {
        ConfiguredActionSnapshot FreezeDefaultAction(Ossastra.CharacterCore.BasicAttackDefinition timeline,int stage);
        void CaptureDefaultAction(Guid actionId,Ossastra.CharacterCore.BasicAttackDefinition timeline,int stage);
    }
    public interface ICharacterAnimationStateView
    {
        SkeletonAnimation Skeleton {get;}
        AnimationSemantic CurrentSemantic {get;}
        string CurrentClip {get;}
        string CurrentSource {get;}
        string FallbackReason {get;}
        Guid CurrentReplayId {get;}
    }
}
