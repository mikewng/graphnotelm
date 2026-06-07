using System.ComponentModel;

namespace graphnotelm.API.Mcp
{
    public sealed class McpNodeInput
    {
        [Description("Title of the node")]
        public string Title { get; set; } = string.Empty;

        [Description("Full note content for this node")]
        public string Note { get; set; } = string.Empty;

        [Description("Tags to apply to this node. Tags are created automatically if they do not exist.")]
        public List<McpTagInput> Tags { get; set; } = [];

        [Description("Relationships from this node to other nodes in the same batch")]
        public List<McpRelationshipInput> Relationships { get; set; } = [];
    }

    public sealed class McpTagInput
    {
        [Description("Name of the tag (e.g. 'sorting', 'dynamic programming')")]
        public string Name { get; set; } = string.Empty;

        [Description("Hex color for this tag (e.g. '#6366F1'). Only needs to be set once per unique tag name.")]
        public string Color { get; set; } = "#6366F1";
    }

    public sealed class McpRelationshipInput
    {
        [Description("Title of the target node (must match another node's title in this batch)")]
        public string TargetNodeTitle { get; set; } = string.Empty;

        [Description("Name of the relationship type (e.g. 'prerequisite of', 'relates to')")]
        public string RelationshipName { get; set; } = string.Empty;

        [Description("Hex color for this relationship type (e.g. '#EF4444'). Only needs to be set once per unique relationship name.")]
        public string Color { get; set; } = "#64748B";

        [Description("Optional inverse label for this relationship type (e.g. 'required by'). Only needs to be set once per unique relationship name.")]
        public string? InverseRelationshipName { get; set; }
    }
}
