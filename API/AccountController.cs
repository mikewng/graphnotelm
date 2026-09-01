using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [ApiController]
    [Route("account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICurrentUserContext _currentUser;

        public AccountController(IAuthService authService, ICurrentUserContext currentUser)
        {
            _authService = authService;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetAccount(CancellationToken ct)
        {
            var result = await _authService.GetAccountAsync(_currentUser.UserId, ct);

            return result.Success
                ? Ok(result.Value)
                : BadRequest(result.Error);
        }

        [HttpPatch]
        public async Task<IActionResult> UpdateAccount([FromBody] UpdateAccountRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.UpdateAccountAsync(_currentUser.UserId, request, ct);

            return result.Success
                ? Ok(result.Value)
                : BadRequest(result.Error);
        }

        [HttpPatch("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ChangePasswordAsync(_currentUser.UserId, request, ct);

            return result.Success
                ? Ok()
                : BadRequest(result.Error);
        }

        [HttpPatch("session")]
        public async Task<IActionResult> SetSessionPreference([FromBody] SessionPreferenceRequest request, CancellationToken ct)
        {
            var result = await _authService.SetSessionPreferenceAsync(_currentUser.UserId, request.NeverExpire, ct);

            return result.Success
                ? Ok(result.Value)
                : BadRequest(result.Error);
        }
    }
}
