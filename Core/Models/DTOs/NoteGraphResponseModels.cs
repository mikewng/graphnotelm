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

    public class NodeSkeletonMetadata
    {
        public float UserConfidenceRate { get; set; }
        public bool IsPinned { get; set; }
    }

    public class NodeSkeleton
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        // Measured when the node has been reviewed, its self-rating (Metadata.UserConfidenceRate) otherwise.
        public float Confidence { get; set; }
        public NodeSkeletonMetadata Metadata { get; set; } = new();
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();
        public Guid? FolderId { get; set; }
    }

    public class GetGraphSkeletonResponse
    {
        public Guid Id { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, FolderDefinition> Folders { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NodeSkeleton> Nodes { get; set; } = new();
    }

    public class GetNodeResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public NoteNodeMetadata Metadata { get; set; } = new();
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();
        public Guid? FolderId { get; set; }
    }

    public class UploadImageResponse
    {
        public Guid ImageId { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class GetImageResponse
    {
        public Stream Content { get; set; } = Stream.Null;
        public string ContentType { get; set; } = string.Empty;
    }

    public class GetNodeBatchResponse
    {
        public Dictionary<Guid, GetNodeResponse> Nodes { get; set; } = new();
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

    public class GetFolderListResponse
    {
        public Dictionary<Guid, FolderDefinition> Folders { get; set; } = new();
    }

    public class CreateFolderResponse
    {
        public Guid Id { get; set; }
        public string FolderName { get; set; }
    }

    public class EditFolderResponse
    {
    }

    public class DeleteFolderResponse
    {
        public string FolderName { get; set; }
    }

    public class MoveNodeToFolderResponse
    {
        public Guid NodeId { get; set; }
        public Guid? FolderId { get; set; }
    }

    public class MoveNodesToFolderResponse
    {
        public Guid? FolderId { get; set; }
        public List<Guid> UpdatedNodeIds { get; set; } = new();
        public List<Guid> SkippedNodeIds { get; set; } = new();
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
    public class NodeSearchResult
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public bool MatchedTitle { get; set; }
        public bool MatchedNote { get; set; }
    }

    public class SearchNodesResponse
    {
        public List<NodeSearchResult> Results { get; set; } = new();
    }

    public class AnalysisNodeResult
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public float Confidence { get; set; }
    }

    public class WeakestPathResponse
    {
        // Start to target; empty when the target can't be reached.
        public List<AnalysisNodeResult> Path { get; set; } = new();
    }

    public class KnowledgeFrontierResponse
    {
        public List<AnalysisNodeResult> Known { get; set; } = new();
        public List<AnalysisNodeResult> Frontier { get; set; } = new();
    }

    public class LearningOrderResponse
    {
        public List<AnalysisNodeResult> Order { get; set; } = new();
        // Nodes in, or waiting on, a prerequisite cycle.
        public List<AnalysisNodeResult> Cyclic { get; set; } = new();
    }

    public class SaveNodeContentResponse
    {
        public bool IsSuccess = false;
    }

    public class PinnedNodeResult
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class GetPinnedNodesResponse
    {
        public List<PinnedNodeResult> Nodes { get; set; } = new();
    }

    public class SetPinnedResponse
    {
        public Guid NodeId { get; set; }
        public bool IsPinned { get; set; }
    }

    public class SetPinnedManyResponse
    {
        public bool IsPinned { get; set; }
        public List<Guid> UpdatedNodeIds { get; set; } = new();
        public List<Guid> SkippedNodeIds { get; set; } = new();
    }

    public class EditNodeMetadataResponse
    {
        public Guid NodeId { get; set; }
        public float Confidence { get; set; }
        public NoteNodeMetadata Metadata { get; set; } = new();
    }

    public class FlashcardResult
    {
        public Guid Id { get; set; }
        public Guid NodeId { get; set; }
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class GetFlashcardsResponse
    {
        public List<FlashcardResult> Cards { get; set; } = new();
    }

    // One note in the graph-wide flashcards list, with its review status.
    public class FlashcardNoteGroup
    {
        public Guid NodeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public float Confidence { get; set; }
        // Null until the note's first review.
        public DateTime? DueAt { get; set; }
        public int Reviews { get; set; }
        // Same rules as the review queue: reviewed and due now, or never reviewed but has cards.
        public bool IsDue { get; set; }
        public bool IsNew { get; set; }
        public List<FlashcardResult> Cards { get; set; } = new();
    }

    public class GetGraphFlashcardsResponse
    {
        // Every note in the graph, including those without cards, ordered by title.
        public List<FlashcardNoteGroup> Notes { get; set; } = new();
    }

    public class DeleteFlashcardResponse
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ReviewSummaryResponse
    {
        public int DueCount { get; set; }
        // Never-reviewed notes with cards, capped at the session's new-note limit.
        public int NewCount { get; set; }
    }

    public class ReviewQueueItem
    {
        public Guid NodeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsNew { get; set; }
        public DateTime? DueAt { get; set; }
        public float Confidence { get; set; }
        // Empty when the note is reviewed from its own title and text.
        public List<FlashcardResult> Cards { get; set; } = new();
    }

    public class ReviewQueueResponse
    {
        // Due notes, most overdue first, then new notes, lowest self-rating first.
        public List<ReviewQueueItem> Items { get; set; } = new();
        public int DueCount { get; set; }
        public int NewCount { get; set; }
    }

    public class SubmitReviewResponse
    {
        public Guid NodeId { get; set; }
        public float Confidence { get; set; }
        public MemoryState Memory { get; set; } = new();
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

    public class NodeTagPair
    {
        public Guid NodeId { get; set; }
        public Guid TagId { get; set; }
    }

    public class AddTagToNodesResponse
    {
        public List<NodeTagPair> Applied { get; set; } = new();
        public List<NodeTagPair> Skipped { get; set; } = new();
    }

    public class RemoveTagFromNodesResponse
    {
        public List<NodeTagPair> Applied { get; set; } = new();
        public List<NodeTagPair> Skipped { get; set; } = new();
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
