using System.Collections.Generic;

public class AnimationComposeContext
{
    public CharacterRoot root;
    public CharacterDataBus dataBus;

    public IReadOnlyDictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents;

    public bool HasWeapon
    {
        get
        {
            return dataBus != null && dataBus.currentWeaponRuntimeData != null;
        }
    }

    public bool HasIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return false;

        return activeIntents != null && activeIntents.ContainsKey(source);
    }

    public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
    {
        if (source == AnimationIntentSourceStateMachine.None)
            return null;

        activeIntents.TryGetValue(source, out AnimationIntent intent);
        return intent;
    }
}