using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphRelationshipService : IGraphRelationshipService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphRepository _noteGraphRepository;

        public GraphRelationshipService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }

        public Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        
    }
}
