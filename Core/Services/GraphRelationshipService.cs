using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphRelationshipService : IGraphRelationshipService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;

        public GraphRelationshipService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetRelationshipListResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetRelationshipListResponse>.Fail(graphDataResult.Error!);

            return Result<GetRelationshipListResponse>.Ok(new GetRelationshipListResponse
            {
                Relationships = graphDataResult.Value!.Relationships.Values.Select(r => r.Name).ToList()
            });
        }

        public async Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<CreateRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<CreateRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var relId = Guid.NewGuid();
            graphData.Relationships[relId] = new RelationshipDefinition
            {
                Name = createRelationshipRequest.Type,
                Color = createRelationshipRequest.Color,
                Inverse = createRelationshipRequest.Inverse
            };

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }

        public async Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<EditRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<EditRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Relationships.TryGetValue(relationId, out var relationship))
                return Result<EditRelationshipResponse>.Fail("Relationship not found.");

            relationship.Name = editRelationshipRequest.Type;
            relationship.Color = editRelationshipRequest.Color;
            relationship.Inverse = editRelationshipRequest.Inverse;

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<DeleteRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<DeleteRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Relationships.Remove(relationId))
                return Result<DeleteRelationshipResponse>.Fail("Relationship not found.");

            foreach (var node in graphData.Nodes.Values)
                node.Relationships.RemoveAll(r => r.RelationshipId == relationId);

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }
    }
}
