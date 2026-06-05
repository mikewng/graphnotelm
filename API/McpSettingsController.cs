using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace graphnotelm.API
{
    [ApiController]
    [Route("settings/mcp")]
    [Authorize]
    public class McpSettingsController : ControllerBase
    {
        private readonly McpSettings _mcpSettings;
        private readonly ICurrentUserContext _currentUser;

        public McpSettingsController(McpSettings mcpSettings, ICurrentUserContext currentUser)
        {
            _mcpSettings = mcpSettings;
            _currentUser = currentUser;
        }

        [HttpGet]
        public ActionResult<McpSettingsResponse> GetMcpSettings()
        {
            return Ok(BuildResponse());
        }

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

            return Ok(BuildResponse());
        }

        [HttpPost("regenerate-key")]
        public ActionResult<McpSettingsResponse> RegenerateKey()
        {
            var newKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            try
            {
                System.IO.File.WriteAllText(_mcpSettings.KeyFilePath, newKey);
            }
            catch
            {
                return StatusCode(500, "Could not persist new MCP key to disk.");
            }

            _mcpSettings.SecretKey = newKey;
            return Ok(BuildResponse());
        }

        [HttpPatch("toggle")]
        public ActionResult<McpSettingsResponse> Toggle([FromBody] McpToggleRequest request)
        {
            try
            {
                System.IO.File.WriteAllText(_mcpSettings.EnabledFilePath, request.IsEnabled ? "true" : "false");
            }
            catch
            {
                return StatusCode(500, "Could not persist MCP enabled state to disk.");
            }

            _mcpSettings.IsEnabled = request.IsEnabled;
            return Ok(BuildResponse());
        }

        private McpSettingsResponse BuildResponse()
        {
            var url = $"http://localhost:{_mcpSettings.Port}/mcp?key={_mcpSettings.SecretKey}";
            var claudeConfig = $$"""
                {
                  "mcpServers": {
                    "graphnotelm": {
                      "command": "npx",
                      "args": ["mcp-remote", "{{url}}"]
                    }
                  }
                }
                """;

            return new McpSettingsResponse
            {
                SecretKey        = _mcpSettings.SecretKey,
                IsUserConfigured = _mcpSettings.LocalUserId.HasValue,
                IsEnabled        = _mcpSettings.IsEnabled,
                Url              = url,
                ClaudeDesktopConfig = claudeConfig
            };
        }
    }

    public class McpSettingsResponse
    {
        public string SecretKey { get; set; } = string.Empty;
        public bool IsUserConfigured { get; set; }
        public bool IsEnabled { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ClaudeDesktopConfig { get; set; } = string.Empty;
    }

    public class McpToggleRequest
    {
        public bool IsEnabled { get; set; }
    }
}
