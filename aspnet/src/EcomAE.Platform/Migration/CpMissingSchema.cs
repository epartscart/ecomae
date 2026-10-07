namespace EcomAE.Platform.Migration;

/// <summary>
/// A tenant database that never had a PHP table or column should render an empty
/// Control Panel screen. That is not a query failure and it is not invented data.
/// </summary>
public static class CpMissingSchema
{
    public static bool IsMissing(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
                || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
