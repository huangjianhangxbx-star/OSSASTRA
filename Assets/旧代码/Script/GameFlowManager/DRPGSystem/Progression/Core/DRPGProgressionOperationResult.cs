public sealed class DRPGProgressionOperationResult
{
    private DRPGProgressionOperationResult(
        bool success,
        DRPGProgressionError error,
        string message)
    {
        Success = success;
        Error = error;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public DRPGProgressionError Error { get; }
    public string Message { get; }

    public static DRPGProgressionOperationResult Ok()
    {
        return new DRPGProgressionOperationResult(
            true,
            DRPGProgressionError.None,
            string.Empty);
    }

    public static DRPGProgressionOperationResult Fail(
        DRPGProgressionError error,
        string message)
    {
        return new DRPGProgressionOperationResult(false, error, message);
    }
}
