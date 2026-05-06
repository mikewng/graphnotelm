using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphTagService: IGraphTagService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphRepository _noteGraphRepository;

        public GraphTagService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }
        public Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
