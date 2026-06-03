using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [ApiController]
    [Route("settings/mcp")]
    public class McpSettingsController : ControllerBase
    {
        private readonly McpSettings _mcpSettings;
        private readonly ICurrentUserContext _currentUser;

        public McpSettingsController(McpSettings mcpSettings, ICurrentUserContext currentUser)
        {
            _mcpSettings = mcpSettings;
            _currentUser = currentUser;
        }

        [Authorize]
        [HttpGet]
        public ActionResult<McpSettingsResponse> GetMcpSettings()
        {
            return Ok(new McpSettingsResponse
            {
                SecretKey       = _mcpSettings.SecretKey,
                IsUserConfigured = _mcpSettings.LocalUserId.HasValue
            });
        }

        [Authorize]
        [HttpPost("configure-user")]
        public ActionResult<McpSettingsResponse> ConfigureLocalUser()
        {
            var userId = _currentUser.UserId;
            _mcpSettings.LocalUserId = userId;

            try
            {
                System.IO.File.WriteAllText(_mcpSettings.UserFilePath, userId.ToString());
            }
            catch
            {
                return StatusCode(500, "MCP user configured in memory but could not be persisted to disk.");
            }

            return Ok(new McpSettingsResponse
            {
                SecretKey        = _mcpSettings.SecretKey,
                IsUserConfigured = true
            });
        }
    }

    public class McpSettingsResponse
    {
        public string SecretKey { get; set; } = string.Empty;
        public bool IsUserConfigured { get; set; }
    }
}
