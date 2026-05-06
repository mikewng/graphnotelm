using graphnotelm.Core.Contexts.Contracts;
using System.Security.Claims;

namespace graphnotelm.Core.Contexts
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (claim is null || !Guid.TryParse(claim, out var userId))
                    throw new UnauthorizedAccessException("Invalid or missing user claim.");

                return userId;
            }
        }
    }
}
