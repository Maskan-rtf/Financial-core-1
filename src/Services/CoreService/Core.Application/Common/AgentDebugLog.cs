using System.Text.Json;

namespace Core.Application.Common;

public static class AgentDebugLog
{
    private const string LogPath = @"d:\work\Maskan\Panel\Financial-Core\debug-35307a.log";

    public static void Write(string hypothesisId, string location, string message, object? data = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = "35307a",
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
