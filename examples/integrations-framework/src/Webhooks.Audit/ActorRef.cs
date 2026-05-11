namespace Webhooks.Audit;

/// <summary>
/// Strongly-typed reference to who performed an action.
/// </summary>
public sealed record ActorRef(string Kind, string Id, string DisplayName)
{
    public static ActorRef User(string id, string display) => new("user", id, display);
    public static ActorRef System(string display = "system") => new("system", "system", display);
}
