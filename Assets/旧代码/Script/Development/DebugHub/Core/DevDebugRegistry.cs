using System;
using System.Collections.Generic;

public sealed class DevDebugRegistry
{
    private readonly Dictionary<string, IDevDebugAction> actions =
        new Dictionary<string, IDevDebugAction>(StringComparer.Ordinal);

    public event Action Changed;

    public IDisposable Register(IDevDebugAction action)
    {
        Validate(action);
        if (actions.ContainsKey(action.Id))
        {
            throw new InvalidOperationException(
                "A development debug action is already registered: " +
                action.Id);
        }

        actions.Add(action.Id, action);
        Changed?.Invoke();
        return new Registration(this, action);
    }

    public IReadOnlyList<IDevDebugAction> Snapshot()
    {
        var snapshot = new List<IDevDebugAction>(actions.Values);
        snapshot.Sort(CompareActions);
        return snapshot;
    }

    public DevDebugActionResult Execute(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !actions.TryGetValue(id, out IDevDebugAction action))
            return DevDebugActionResult.Failed("调试项不存在：" + (id ?? "<null>"));

        try
        {
            DevDebugAvailability availability = action.Evaluate();
            if (!availability.IsAvailable)
                return DevDebugActionResult.Unavailable(availability.Reason);

            return action.Execute();
        }
        catch (Exception exception)
        {
            return DevDebugActionResult.Failed(
                "调试项执行异常：" + exception.Message);
        }
    }

    private void Unregister(IDevDebugAction action)
    {
        if (action == null ||
            !actions.TryGetValue(action.Id, out IDevDebugAction current) ||
            !ReferenceEquals(current, action))
        {
            return;
        }

        actions.Remove(action.Id);
        Changed?.Invoke();
    }

    private static int CompareActions(
        IDevDebugAction left,
        IDevDebugAction right)
    {
        int category = string.Compare(
            left.Category,
            right.Category,
            StringComparison.Ordinal);
        if (category != 0)
            return category;

        int order = left.Order.CompareTo(right.Order);
        if (order != 0)
            return order;

        int title = string.Compare(
            left.Title,
            right.Title,
            StringComparison.Ordinal);
        return title != 0
            ? title
            : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
    }

    private static void Validate(IDevDebugAction action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (string.IsNullOrWhiteSpace(action.Id))
            throw new ArgumentException("Debug action ID is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(action.Category))
            throw new ArgumentException("Debug action category is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(action.Title))
            throw new ArgumentException("Debug action title is required.", nameof(action));
    }

    private sealed class Registration : IDisposable
    {
        private DevDebugRegistry owner;
        private IDevDebugAction action;

        public Registration(DevDebugRegistry owner, IDevDebugAction action)
        {
            this.owner = owner;
            this.action = action;
        }

        public void Dispose()
        {
            if (owner == null)
                return;

            owner.Unregister(action);
            owner = null;
            action = null;
        }
    }
}
