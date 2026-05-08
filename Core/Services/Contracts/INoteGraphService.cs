using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphService
    {
        // Note Graph
        public Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct);
        public Task<Result<GetGraphListResponse>> GetNoteGraphList(CancellationToken ct);
        public Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest, CancellationToken ct);
        public Task<Result<EditGraphMetadataResponse>> EditGraphMetadataById(EditGraphMetadataRequest editGraphMetadataRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<DeleteGraphResponse>> DeleteNoteGraphById(Guid noteGraphId, CancellationToken ct);
        public Task<Result<CreateGraphResponse>> ImportNoteGraphFromJSON(NoteGraphDocumentREADONLY document, CancellationToken ct);
        public Task<Result<NoteGraphDocumentREADONLY>> ExportNoteGraphAsJSON(Guid noteGraphId, CancellationToken ct);
    }
}
