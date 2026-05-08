using System.ComponentModel.DataAnnotations;
using graphnotelm.Core.Models;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphResponse
    {
        public Guid Id { get; set; }
        public bool IsSuccess { get; set; }
    }

    public class EditGraphMetadataResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class GetGraphResponse
    {
        public Guid Id { get; set; }
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class GetGraphListResponse
    {
        public List<NoteGraphMetadata> GraphList { get; set; } = new List<NoteGraphMetadata>();
    }

    public class DeleteGraphResponse
    {
        public Guid id { get; set; }
        public bool isDeleted { get; set; }
    }

    public class CreateNodeResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
    }

    public class EditNodeResponse
    {
        public NoteNode NoteNodeContent { get; set; }
    }

    public class DeleteNodeResponse
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
    }


    public class GetTagListResponse
    {
        public List<string> Tags { get; set; } = new();
    }

    public class CreateTagResponse
    {
        public string TagName { get; set; }
    }

    public class EditTagResponse
    {

    }

    public class DeleteTagResponse {
        public string TagName { get; set; }
    }

    public class GetRelationshipListResponse
    {
        public List<string> Relationships { get; set; } = new();

    }

    public class CreateRelationshipResponse
    {

    }

    public class EditRelationshipResponse
    {

    }

    public class DeleteRelationshipResponse
    {

    }
    public class SaveNodeContentResponse
    {
        public bool IsSuccess = false;
    }

    public class EditNodeMetadataResponse
    {
        public Guid NodeId { get; set; }
        public NoteNodeMetadata Metadata { get; set; } = new();
    }

    public class AddNodeTagResponse
    {
        public Guid NodeId { get; set; }
        public List<Guid> Tags { get; set; } = new();
    }

    public class RemoveNodeTagResponse
    {
        public Guid NodeId { get; set; }
        public Guid RemovedTagId { get; set; }
    }

    public class AddNodeRelationshipResponse
    {
        public Guid NodeId { get; set; }
        public List<NodeRelationship> Relationships { get; set; } = new();
    }

    public class RemoveNodeRelationshipResponse
    {
        public Guid NodeId { get; set; }
        public Guid RemovedTargetNodeId { get; set; }
        public Guid RemovedRelationshipId { get; set; }
    }
}
