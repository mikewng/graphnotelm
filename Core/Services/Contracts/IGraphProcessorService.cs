using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphProcessorService
    {
        // Goal of this function is to take in a bunch of adjustments to confidence ranks across multiple nodes via kv pairs and adjust each node's UserConfidenceRank metadata.
        // This might be useful for AI Agents to decide whether or not the user feels like the node is ready.
        public void AdjustConfidenceRanksByIds(Dictionary<Guid, float> adjustmentsKVPairs, CancellationToken ct);
        public void GetFailingNodes();
        public void FindWeakNodes(Guid noteNodeId);

    }
}
