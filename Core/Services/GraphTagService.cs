using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphTagService : IGraphTagService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;


        public GraphTagService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetTagListResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetTagListResponse>.Fail(graphDataResult.Error!);

            return Result<GetTagListResponse>.Ok(new GetTagListResponse
            {
                Tags = graphDataResult.Value!.Tags.Values.Select(t => t.Name).ToList()
            });
        }

        public async Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<CreateTagResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<CreateTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var tagId = Guid.NewGuid();
            graphData.Tags[tagId] = new TagDefinition { Name = createTagRequest.TagName, Color = createTagRequest.TagColor };

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<CreateTagResponse>.Ok(new CreateTagResponse { TagName = createTagRequest.TagName });
            }
            catch
            {
                return Result<CreateTagResponse>.Fail("Failed to create tag.");
            }
        }

        public async Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<EditTagResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<EditTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Tags.TryGetValue(tagId, out var tag))
                return Result<EditTagResponse>.Fail("Tag not found.");

            tag.Name = editTagRequest.TagName;
            tag.Color = editTagRequest.TagColor;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditTagResponse>.Ok(new EditTagResponse());
            }
            catch
            {
                return Result<EditTagResponse>.Fail("Failed to update tag.");
            }
        }

        public async Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<DeleteTagResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<DeleteTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Tags.TryGetValue(tagId, out var tag))
                return Result<DeleteTagResponse>.Fail("Tag not found.");

            graphData.Tags.Remove(tagId);

            foreach (var node in graphData.Nodes.Values)
                node.Tags.Remove(tagId);

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<DeleteTagResponse>.Ok(new DeleteTagResponse { TagName = tag.Name });
            }
            catch
            {
                return Result<DeleteTagResponse>.Fail("Failed to delete tag.");
            }
        }
    }
}
