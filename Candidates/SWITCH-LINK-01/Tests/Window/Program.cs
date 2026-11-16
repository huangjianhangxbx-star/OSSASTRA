using System;
using Ossastra.CharacterCore;
using static Ossastra.CharacterCore.SwitchLinkReleaseSource;
using static Ossastra.CharacterCore.SwitchLinkWindowStatus;

internal static class Program
{
    private static readonly SwitchLinkIdentity A = new SwitchLinkIdentity("blue", 1, 4);
    private static readonly SwitchLinkIdentity B = new SwitchLinkIdentity("freycia", 2, 5);
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static int failures;

    private static void Main()
    {
        Test("Eligibility starts at observed release and lasts exactly one battle second", Timing);
        Test("Ordinary attacks and opted-in player abilities are the only eligible sources", Sources);
        Test("Release must come from the authoritative actor, life and control revision", StaleIdentity);
        Test("Repeated emissions cannot reopen or extend the same execution", RepeatRelease);
        Test("New independent release replaces the old opportunity and its prepared ticket", Replacement);
        Test("Preparation is read-only and failed preparation never starts cooldown", ReadOnlyPreparation);
        Test("A valid ticket commits once and starts a team-wide two-and-a-half-second cooldown", SharedCooldown);
        Test("Clear invalidates the opportunity and preserves both cooldown and deduplication", Clear);
        Test("Cooldown expiry can use a fresh opportunity created just before expiry", OpenAcrossCooldown);
        Test("Wrong actor, default ticket and invalid clocks cannot spend the opportunity", InvalidCommit);
        Test("Time values and configured durations must be valid", InvalidTimes);
        Console.WriteLine(failures == 0 ? "11/11" : $"{11 - failures}/11");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    private static void Timing()
    {
        var window = new SwitchLinkWindow();
        Status(window, A, 10, NoOpportunity);
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        Status(window, A, 10, Ready);
        Near(window.OpportunityRemaining(A, 10.25), .75);
        Status(window, A, 10.999, Ready);
        Status(window, A, 11, Expired);
        Near(window.OpportunityRemaining(A, 11), 0);
    }

    private static void Sources()
    {
        var window = new SwitchLinkWindow();
        foreach (var excluded in new[] { Other, Companion, SwitchAttack, (SwitchLinkReleaseSource)99 })
        {
            False(window.ObserveRelease(A, A, Guid.NewGuid(), excluded, 10));
            Status(window, A, 10, NoOpportunity);
        }
        False(window.ObserveRelease(A, A, Guid.Empty, PlayerAttack, 10));
        True(window.ObserveRelease(A, A, First, PlayerOptInAbility, 10));
        var opening = Status(window, A, 10, Ready);
        Eq(opening.Source, PlayerOptInAbility);
        Eq(opening.ExecutionId, First);
    }

    private static void StaleIdentity()
    {
        var window = new SwitchLinkWindow();
        var staleLife = new SwitchLinkIdentity(A.ActorId, A.LifeGeneration - 1, A.ControlRevision);
        var staleControl = new SwitchLinkIdentity(A.ActorId, A.LifeGeneration, A.ControlRevision - 1);
        False(window.ObserveRelease(staleLife, A, First, PlayerAttack, 10));
        False(window.ObserveRelease(staleControl, A, First, PlayerAttack, 10));
        False(window.ObserveRelease(B, A, First, PlayerAttack, 10));
        False(window.ObserveRelease(default, default, First, PlayerAttack, 10));
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        Status(window, staleLife, 10, OwnerChanged);
        Status(window, staleControl, 10, OwnerChanged);
        Status(window, B, 10, OwnerChanged);
        Status(window, A, 10, Ready);
    }

    private static void RepeatRelease()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        False(window.ObserveRelease(A, A, First, PlayerAttack, 10.8));
        Status(window, A, 11, Expired);
        False(window.ObserveRelease(A, A, First, PlayerAttack, 13));
        True(window.ObserveRelease(A, A, Second, PlayerAttack, 13));
        False(window.ObserveRelease(A, A, First, PlayerAttack, 13.1));
        Eq(Status(window, A, 13.1, Ready).ExecutionId, Second);
    }

    private static void Replacement()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        var old = Status(window, A, 10, Ready);
        True(window.ObserveRelease(A, A, Second, PlayerAttack, 10.7));
        False(window.TryCommit(old, A, 10.75));
        Near(window.CooldownRemaining(10.75), 0);
        Eq(Status(window, A, 11.5, Ready).ExecutionId, Second);
        Status(window, A, 11.75, Expired);
    }

    private static void ReadOnlyPreparation()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        var ticketOne = Status(window, A, 10.1, Ready);
        var ticketTwo = Status(window, A, 10.2, Ready);
        Eq(ticketOne.ExecutionId, ticketTwo.ExecutionId);
        Eq(ticketOne.ExpiresAt, ticketTwo.ExpiresAt);
        Near(window.CooldownRemaining(10.2), 0);
        False(window.TryCommit(ticketOne, A, 11.1));
        Near(window.CooldownRemaining(11.1), 0);
    }

    private static void SharedCooldown()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        var ticket = Status(window, A, 10.25, Ready);
        True(window.TryCommit(ticket, A, 10.25));
        False(window.TryCommit(ticket, A, 10.25));
        Status(window, A, 10.25, NoOpportunity);
        Near(window.CooldownRemaining(10.25), 2.5);
        True(window.ObserveRelease(B, B, Second, PlayerAttack, 11.8));
        Status(window, B, 12, CoolingDown);
        Status(window, B, 12.75, Ready);
        True(window.TryCommit(Status(window, B, 12.75, Ready), B, 12.75));
        Near(window.CooldownRemaining(12.75), 2.5);
    }

    private static void Clear()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        var ticket = Status(window, A, 10.1, Ready);
        window.ClearOpportunity();
        Status(window, A, 10.1, NoOpportunity);
        False(window.TryCommit(ticket, A, 10.1));
        False(window.ObserveRelease(A, A, First, PlayerAttack, 10.2));
        True(window.ObserveRelease(A, A, Second, PlayerAttack, 10.3));
        True(window.TryCommit(Status(window, A, 10.3, Ready), A, 10.3));
        window.ClearOpportunity();
        Near(window.CooldownRemaining(10.4), 2.4);
        False(window.ObserveRelease(A, B, Second, PlayerAttack, 10.4));
    }

    private static void OpenAcrossCooldown()
    {
        var window = new SwitchLinkWindow(.8, 2);
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        True(window.TryCommit(Status(window, A, 10, Ready), A, 10));
        True(window.ObserveRelease(B, B, Second, PlayerAttack, 11.7));
        Near(window.OpportunityRemaining(B, 11.8), .7);
        Status(window, B, 11.8, CoolingDown);
        Status(window, B, 12, Ready);
    }

    private static void InvalidCommit()
    {
        var window = new SwitchLinkWindow();
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        var ticket = Status(window, A, 10.1, Ready);
        False(window.TryCommit(ticket, B, 10.1));
        False(window.TryCommit(default, A, 10.1));
        False(window.TryCommit(ticket, A, double.NaN));
        False(window.TryCommit(ticket, A, 9.9));
        Status(window, A, 10.2, Ready);
        Near(window.CooldownRemaining(10.2), 0);
    }

    private static void InvalidTimes()
    {
        Reject(() => new SwitchLinkWindow(0, 1));
        Reject(() => new SwitchLinkWindow(1, -1));
        Reject(() => new SwitchLinkWindow(double.NaN, 1));
        var window = new SwitchLinkWindow(1, 0);
        False(window.ObserveRelease(A, A, First, PlayerAttack, double.PositiveInfinity));
        False(window.ObserveRelease(A, A, First, PlayerAttack, -1));
        True(window.ObserveRelease(A, A, First, PlayerAttack, 10));
        False(window.ObserveRelease(A, A, Second, PlayerAttack, 9.9));
        Status(window, A, double.NaN, InvalidTime);
        Status(window, A, 9.9, InvalidTime);
        Status(window, A, 10, Ready);
    }

    private static SwitchLinkOpportunity Status(SwitchLinkWindow window, SwitchLinkIdentity owner, double time,
        SwitchLinkWindowStatus expected)
    {
        bool available = window.TryPrepare(owner, time, out var opportunity, out var status);
        Eq(status, expected);
        Eq(available, expected == Ready);
        return opportunity;
    }

    private static void Test(string name, Action verify)
    {
        try { verify(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + " - " + error.Message); }
    }

    private static void True(bool value) => Eq(value, true);
    private static void False(bool value) => Eq(value, false);
    private static void Eq<T>(T actual, T expected)
    {
        if (!Equals(actual, expected)) throw new InvalidOperationException($"Expected {expected}; got {actual}");
    }

    private static void Near(double actual, double expected)
    {
        if (Math.Abs(expected - actual) > 1e-9) throw new InvalidOperationException($"Expected {expected}; got {actual}");
    }

    private static void Reject(Action operation)
    {
        try { operation(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException("Expected invalid-argument rejection");
    }
}
