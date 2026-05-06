using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteGraphService: INoteGraphService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphRepository _noteGraphRepository;

        public NoteGraphService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteGraphResponse>> DeleteNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest createNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<GetGraphListResponse>> GetNoteGraphList(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
