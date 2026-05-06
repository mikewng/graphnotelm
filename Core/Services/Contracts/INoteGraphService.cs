using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphService
    {
        public Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId);
        public Task<Result<GetGraphListResponse>> GetNoteGraphList();
        public Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest);
        public Task<Result<DeleteGraphResponse>> DeleteNoteGraphById(Guid noteGraphId);

    }
}
