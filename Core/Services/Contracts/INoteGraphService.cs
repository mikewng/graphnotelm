using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphService
    {
        // Note Graph
        public Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId);
        public Task<Result<GetGraphListResponse>> GetNoteGraphList();
        public Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest);
        public Task<Result<DeleteGraphResponse>> DeleteNoteGraphById(Guid noteGraphId);

        // Note Nodes 
        public Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId);
        public Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest createNodeRequest, Guid noteGraphId, Guid noteNodeId);
        public Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId);

        // Tags
        public Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId);
        public Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId);
        public Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId);
        public Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId);

        // Relationships
        public Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId);
        public Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId);
        public Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId);
        public Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId);

    }
}
