using graphnotelm.Core.Utils;

namespace graphnotelm.Core.Contexts.Contracts
{
    public interface IGraphContext
    {
        public Guid GraphId { get; }
        public GraphView Graph {  get; }
    }
}
