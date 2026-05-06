using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]")]
    public class UserAuthController : Controller
    {
        private readonly ILogger<UserAuthController> _logger;
        private readonly IAuthService _authService;
        private readonly AppDbContext _db;
        public UserAuthController(ILogger<UserAuthController> logger, IAuthService authService, AppDbContext db)
        {
            _logger = logger;
            _authService = authService;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request, ct);

            return result.Success
                ? Ok(result.Value)
                : BadRequest(result.Error);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(request, ct);

            return result.Success
                ? Ok()
                : BadRequest(result.Error);
        }
    }
}
