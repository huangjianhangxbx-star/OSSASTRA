using System;

public enum DevDebugActionStatus
{
    Succeeded,
    Unavailable,
    Failed
}

public readonly struct DevDebugAvailability
{
    public bool IsAvailable { get; }
    public string Reason { get; }

    private DevDebugAvailability(bool isAvailable, string reason)
    {
        IsAvailable = isAvailable;
        Reason = reason ?? string.Empty;
    }

    public static DevDebugAvailability Available() =>
        new DevDebugAvailability(true, string.Empty);

    public static DevDebugAvailability Unavailable(string reason) =>
        new DevDebugAvailability(
            false,
            string.IsNullOrWhiteSpace(reason) ? "当前条件不足" : reason);
}

public readonly struct DevDebugActionResult
{
    public DevDebugActionStatus Status { get; }
    public string Message { get; }
    public bool Success => Status == DevDebugActionStatus.Succeeded;

    private DevDebugActionResult(
        DevDebugActionStatus status,
        string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public static DevDebugActionResult Succeeded(string message) =>
        new DevDebugActionResult(
            DevDebugActionStatus.Succeeded,
            string.IsNullOrWhiteSpace(message) ? "执行成功" : message);

    public static DevDebugActionResult Unavailable(string reason) =>
        new DevDebugActionResult(
            DevDebugActionStatus.Unavailable,
            string.IsNullOrWhiteSpace(reason) ? "当前条件不足" : reason);

    public static DevDebugActionResult Failed(string message) =>
        new DevDebugActionResult(
            DevDebugActionStatus.Failed,
            string.IsNullOrWhiteSpace(message) ? "执行失败" : message);
}

public interface IDevDebugAction
{
    string Id { get; }
    string Category { get; }
    string Title { get; }
    string Summary { get; }
    string Details { get; }
    int Order { get; }
    bool CloseHubOnSuccess { get; }

    DevDebugAvailability Evaluate();
    DevDebugActionResult Execute();
}

public sealed class DelegateDevDebugAction : IDevDebugAction
{
    private readonly Func<DevDebugAvailability> evaluate;
    private readonly Func<DevDebugActionResult> execute;

    public string Id { get; }
    public string Category { get; }
    public string Title { get; }
    public string Summary { get; }
    public string Details { get; }
    public int Order { get; }
    public bool CloseHubOnSuccess { get; }

    public DelegateDevDebugAction(
        string id,
        string category,
        string title,
        string summary,
        string details,
        int order,
        bool closeHubOnSuccess,
        Func<DevDebugAvailability> evaluate,
        Func<DevDebugActionResult> execute)
    {
        Id = id;
        Category = category;
        Title = title;
        Summary = summary;
        Details = details;
        Order = order;
        CloseHubOnSuccess = closeHubOnSuccess;
        this.evaluate = evaluate;
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public DevDebugAvailability Evaluate() =>
        evaluate != null ? evaluate() : DevDebugAvailability.Available();

    public DevDebugActionResult Execute() => execute();
}
