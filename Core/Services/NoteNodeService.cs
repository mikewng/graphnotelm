using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteNodeService : INoteNodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphRepository _noteGraphRepository;

        public NoteNodeService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }

        public Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest createNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        
    }
}
