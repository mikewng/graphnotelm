using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public class GraphContentTools
    {
        private readonly INoteNodeService _nodeService;
        public GraphContentTools(INoteNodeService noteNodeService)
        {
            _nodeService = noteNodeService;
        }

        [Description("Gets a single node's full content based off node ID. This includes the title, confidence score, note content, and relationships")]
        public NoteNode GetNodeById(
            [Description("The graph note ID based off of type Guid in C#.")]
            Guid graphNoteId,
            [Description("The node ID based off of type Guid in C#.")]
            Guid noteNodeId
            )
        {
            return new NoteNode();
        }
    }
}
