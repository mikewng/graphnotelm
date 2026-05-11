using graphnotelm.Utils;

namespace graphnotelm.Core.Clients
{
    public class WaniKaniClient
    {
        private readonly string _apiToken;

        public WaniKaniClient(string apiToken)
        {
            _apiToken = apiToken;
        }

        private string ValidateHeaders()
        {
            return "";
        }

        public void FetchAllPages(string url)
        {

        }

        public void FetchLearningKanji()
        {
        }

    }
}
