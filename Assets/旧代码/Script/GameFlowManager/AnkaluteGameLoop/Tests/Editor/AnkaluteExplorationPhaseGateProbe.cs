#if UNITY_EDITOR
public static class AnkaluteExplorationPhaseGateProbe
{
    public static string RunOwnership()
    {
        var huntData = new HuntFlowDataBus();
        var firstAuthority = new CharacterControlAuthority();
        var secondAuthority = new CharacterControlAuthority();
        CharacterControlAuthority currentAuthority = firstAuthority;
        bool gameplayInputEnabled = true;
        bool pickupBlocked = false;
        int inputWrites = 0;
        int pickupWrites = 0;

        var externalOwner = new object();
        firstAuthority.SetOverride(
            externalOwner,
            new CharacterControlOverride(CharacterControlChannel.Jump, 100));

        var gate = new AnkaluteExplorationPhaseGate(
            () => huntData,
            () => currentAuthority,
            enabled =>
            {
                gameplayInputEnabled = enabled;
                inputWrites++;
            },
            blocked =>
            {
                pickupBlocked = blocked;
                pickupWrites++;
            });

        gate.Apply(false);
        AssertFalse(gate.IsExplorationEnabled,
            "Blocked phase must be observable");
        AssertTrue(huntData.isPaused,
            "Blocked phase must pause Hunt time");
        AssertFalse(gameplayInputEnabled,
            "Blocked phase must disable the complete input handler");
        AssertTrue(pickupBlocked,
            "Blocked phase must block pickup input");
        AssertFalse(firstAuthority.IsAllowed(
            CharacterControlChannel.PrimaryAttack),
            "Blocked phase must own an all-channel authority override");

        gate.Apply(false);
        AssertEqual(2, inputWrites,
            "Repeated Apply must deterministically reassert input state");
        AssertEqual(2, pickupWrites,
            "Repeated Apply must deterministically reassert pickup state");

        currentAuthority = secondAuthority;
        gate.Apply(false);
        AssertTrue(firstAuthority.IsAllowed(
            CharacterControlChannel.PrimaryAttack),
            "Player swap must release the phase override from the old player");
        AssertFalse(firstAuthority.IsAllowed(CharacterControlChannel.Jump),
            "Player swap must not release a foreign authority owner");
        AssertFalse(secondAuthority.IsAllowed(
            CharacterControlChannel.PrimaryAttack),
            "Player swap must acquire the phase override on the new player");

        gate.Apply(true);
        AssertTrue(gate.IsExplorationEnabled,
            "Exploration phase must be observable");
        AssertFalse(huntData.isPaused,
            "Exploration phase must resume Hunt time");
        AssertTrue(gameplayInputEnabled,
            "Exploration phase must enable gameplay input");
        AssertFalse(pickupBlocked,
            "Exploration phase must enable pickup input");
        AssertTrue(secondAuthority.IsAllowed(
            CharacterControlChannel.PrimaryAttack),
            "Exploration phase must release only its authority override");

        gate.Apply(false);
        gate.Release();
        AssertTrue(secondAuthority.IsAllowed(
            CharacterControlChannel.PrimaryAttack),
            "Release must remove the phase owner");
        AssertTrue(gameplayInputEnabled,
            "Release must leave gameplay input enabled");
        AssertFalse(pickupBlocked,
            "Release must leave pickup input enabled");

        return "PASS exploration phase gate ownership";
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new System.Exception(
                message + ". Expected=" + expected + ", Actual=" + actual);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new System.Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }
}
#endif
