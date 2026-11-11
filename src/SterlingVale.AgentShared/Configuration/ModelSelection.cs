namespace SterlingVale.AgentShared.Configuration;

public sealed record ModelSelection(string Mode, bool IsLive, string Reason)
{
    public static ModelSelection Parse(string? mode) => mode?.ToLowerInvariant() switch
    {
        null or "fake" => new("fake", false,
            mode is null ? "default fake control; Azure environment variables do not enable live calls"
                : "explicit fake control; Azure environment variables are ignored"),
        "live" => new("live", true, "explicit live opt-in; Azure configuration is required"),
        _ => throw new ArgumentException("--model must be fake or live."),
    };
}
