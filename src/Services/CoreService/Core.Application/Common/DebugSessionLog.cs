using System.Text.Json;

namespace Core.Application.Common;

internal static class DebugSessionLog
{
    private const string LogPath = @"d:\work\Maskan\Panel\Financial-Core\debug-f414f7.log";

    public static void Write(string hypothesisId, string location, string message, object? data = null, string? runId = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = "f414f7",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            File.AppendAllText(LogPath, payload + Environment.NewLine);
        }
        catch
        {
            // ignore debug logging failures
        }
    }
}
