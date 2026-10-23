using System;
using System.Collections.Generic;

// Observers cannot undo a committed journal or interrupt its remaining
// notifications. Diagnostics are bounded, local to the owning Session.
internal sealed class DRPGBattleSafeEventPublisher
{
    private const int Capacity = 64;
    private readonly Queue<string> diagnostics = new Queue<string>();
    internal IReadOnlyList<string> Diagnostics => Array.AsReadOnly(diagnostics.ToArray());

    internal void Publish(Action listeners, string kind)
    {
        if (listeners == null) return;
        foreach (Action listener in listeners.GetInvocationList())
            try { listener(); } catch (Exception error) { Record(kind, listener, error); }
    }

    internal void Publish<T>(Action<T> listeners, T value, string kind)
    {
        if (listeners == null) return;
        foreach (Action<T> listener in listeners.GetInvocationList())
            try { listener(value); } catch (Exception error) { Record(kind, listener, error); }
    }

    internal void Publish<TFirst, TSecond>(Action<TFirst, TSecond> listeners, TFirst first, TSecond second, string kind)
    {
        if (listeners == null) return;
        foreach (Action<TFirst, TSecond> listener in listeners.GetInvocationList())
            try { listener(first, second); } catch (Exception error) { Record(kind, listener, error); }
    }

    private void Record(string kind, Delegate subscriber, Exception error)
    {
        if (diagnostics.Count == Capacity) diagnostics.Dequeue();
        diagnostics.Enqueue(kind + " | " + subscriber.Method.DeclaringType?.FullName + "." +
            subscriber.Method.Name + " | " + error.GetType().Name + ": " + error.Message);
    }
}
