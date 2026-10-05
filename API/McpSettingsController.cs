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
            var clients = BuildClientConfigs(url);

            return new McpSettingsResponse
            {
                SecretKey        = _mcpSettings.SecretKey,
                IsUserConfigured = _mcpSettings.LocalUserId.HasValue,
                IsEnabled        = _mcpSettings.IsEnabled,
                Url              = url,
                ClaudeDesktopConfig = clients[0].Config,
                Clients          = clients
            };
        }

        // Clients that only speak stdio go through mcp-remote; the rest connect to the URL directly.
        // Backticks in Instructions mark inline code for the frontend.
        private static List<McpClientConfig> BuildClientConfigs(string url) =>
        [
            new()
            {
                Id = "claude-desktop",
                Name = "Claude Desktop",
                Instructions = @"Paste into `%APPDATA%\Claude\claude_desktop_config.json`, then fully restart Claude Desktop (quit from the system tray). Requires Node.js for `npx`.",
                Config = $$"""
                    {
                      "mcpServers": {
                        "graphnotelm": {
                          "command": "npx",
                          "args": ["mcp-remote", "{{url}}"]
                        }
                      }
                    }
                    """
            },
            new()
            {
                Id = "claude-code",
                Name = "Claude Code",
                Instructions = "Run in a terminal. Add `--scope user` before the name to make it available in every project.",
                Config = $"claude mcp add --transport http graphnotelm \"{url}\""
            },
            new()
            {
                Id = "codex",
                Name = "Codex",
                Instructions = @"Add to `~/.codex/config.toml` (Windows: `%USERPROFILE%\.codex\config.toml`). Requires Node.js for `npx`.",
                Config = $$"""
                    [mcp_servers.graphnotelm]
                    command = "npx"
                    args = ["mcp-remote", "{{url}}"]
                    """
            },
            new()
            {
                Id = "cursor",
                Name = "Cursor",
                Instructions = "Paste into `~/.cursor/mcp.json` for all projects, or `.cursor/mcp.json` in a project folder.",
                Config = $$"""
                    {
                      "mcpServers": {
                        "graphnotelm": {
                          "url": "{{url}}"
                        }
                      }
                    }
                    """
            },
            new()
            {
                Id = "vscode",
                Name = "VS Code (Copilot)",
                Instructions = "Paste into `.vscode/mcp.json` in your workspace, or run MCP: Open User Configuration to add it for every workspace.",
                Config = $$"""
                    {
                      "servers": {
                        "graphnotelm": {
                          "type": "http",
                          "url": "{{url}}"
                        }
                      }
                    }
                    """
            },
            new()
            {
                Id = "other",
                Name = "Other MCP client",
                Instructions = "Clients that support Streamable HTTP can use this URL directly. For clients that only support stdio, run it through `npx mcp-remote <url>`. The server only accepts connections from this machine.",
                Config = url
            }
        ];
    }

    public class McpSettingsResponse
    {
        public string SecretKey { get; set; } = string.Empty;
        public bool IsUserConfigured { get; set; }
        public bool IsEnabled { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ClaudeDesktopConfig { get; set; } = string.Empty;
        public List<McpClientConfig> Clients { get; set; } = [];
    }

    public class McpClientConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Config { get; set; } = string.Empty;
    }

    public class McpToggleRequest
    {
        public bool IsEnabled { get; set; }
    }
}
