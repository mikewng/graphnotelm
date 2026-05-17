namespace graphnotelm.Core.Models
{
    public abstract record AgentEvent;

    public sealed record ContentDelta(string Text) : AgentEvent;

    public sealed record ToolInvoked(string ToolName) : AgentEvent;

    public sealed record ToolResult(string ToolName) : AgentEvent;

    public sealed record TurnComplete : AgentEvent;
}
