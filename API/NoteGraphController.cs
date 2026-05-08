using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace graphnotelm.API
{
    [Authorize]
    [ApiController]
    [Route("NoteGraph")]
    public class NoteGraphController : ControllerBase
    {
        private readonly ILogger<NoteGraphController> _logger;
        private readonly INoteGraphService _noteGraphService;

        public NoteGraphController(
            ILogger<NoteGraphController> logger,
            INoteGraphService noteGraphService)
        {
            _logger = logger;
            _noteGraphService = noteGraphService;
        }

        [HttpGet("id/{noteGraphId:guid}", Name = "GetNoteGraphById")]
        public async Task<ActionResult<Result<GetGraphResponse>>> GetGraphById(Guid noteGraphId, CancellationToken ct)
        {
            var graphResponse = await _noteGraphService.GetNoteGraphById(noteGraphId, ct);
            if (!graphResponse.Success || graphResponse.Value == null)
            {
                return Result<GetGraphResponse>.Fail("Could not find note graph of given id.");
            }

            return Result<GetGraphResponse>.Ok(graphResponse.Value);
        }

        [HttpGet("list", Name = "GetNoteGraphList")]
        public async Task<ActionResult<Result<GetGraphListResponse>>> GetGraphList(CancellationToken ct)
        {
            var graphListResponse = await _noteGraphService.GetNoteGraphList(ct);
            if (!graphListResponse.Success || graphListResponse.Value == null)
            {
                return Result<GetGraphListResponse>.Fail("Could not find any graphs for given user.");
            }

            return Result<GetGraphListResponse>.Ok(graphListResponse.Value);
        }

        [HttpPost("create", Name = "CreateNoteGraph")]
        public async Task<ActionResult<Result<CreateGraphResponse>>> CreateGraph([FromBody] CreateGraphRequest createGraphRequest, CancellationToken ct)
        {
            var createGraphResponse = await _noteGraphService.CreateNoteGraph(createGraphRequest, ct);
            if (!createGraphResponse.Success || createGraphResponse.Value == null)
            {
                return Result<CreateGraphResponse>.Fail("Failed to create given graph note.");
            }

            return Result<CreateGraphResponse>.Ok(createGraphResponse.Value);
        }

        [HttpDelete("delete/{noteGraphId:guid}", Name = "DeleteNoteGraph")]
        public async Task<ActionResult<Result<DeleteGraphResponse>>> DeleteGraph(Guid noteGraphId, CancellationToken ct)
        {
            var deleteGraphResponse = await _noteGraphService.DeleteNoteGraphById(noteGraphId, ct);
            if (!deleteGraphResponse.Success || deleteGraphResponse.Value == null)
            {
                return Result<DeleteGraphResponse>.Fail("Could not delete graph of given id.");
            }

            return Result<DeleteGraphResponse>.Ok(deleteGraphResponse.Value);
        }

        [HttpPatch("edit/{noteGraphId:guid}/metadata", Name = "EditNoteGraphMetadata")]
        public async Task<ActionResult<Result<EditGraphMetadataResponse>>> EditGraphMetadata([FromBody] EditGraphMetadataRequest editGraphMetadataRequest, Guid noteGraphId, CancellationToken ct)
        {
            var editGraphMetadataResponse = await _noteGraphService.EditGraphMetadataById(editGraphMetadataRequest, noteGraphId, ct);
            if (!editGraphMetadataResponse.Success || editGraphMetadataResponse.Value == null)
            {
                return Result<EditGraphMetadataResponse>.Fail("Failed to edit graph metadata of given ID.");
            }

            return Result<EditGraphMetadataResponse>.Ok(editGraphMetadataResponse.Value);
        }

        [HttpPost("create/import")]
        public async Task<ActionResult<Result<CreateGraphResponse>>> ImportNoteGraph([FromBody] NoteGraphDocumentREADONLY document, CancellationToken ct)
        {
            var importNoteGraphResponse = await _noteGraphService.ImportNoteGraphFromJSON(document, ct);
            if (!importNoteGraphResponse.Success || importNoteGraphResponse.Value == null)
            {
                return Result<CreateGraphResponse>.Fail("Failed to import graph from JSON.");
            }

            return Result<CreateGraphResponse>.Ok(importNoteGraphResponse.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/export")]
        public async Task<IActionResult> ExportNoteGraphAsJSON(Guid noteGraphId, CancellationToken ct)
        {
            var exportResult = await _noteGraphService.ExportNoteGraphAsJSON(noteGraphId, ct);
            if (!exportResult.Success || exportResult.Value == null)
            {
                return BadRequest(Result<object>.Fail("Failed to export graph as JSON."));
            }

            var json = JsonSerializer.Serialize(exportResult.Value, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"notegraph-{exportResult.Value.Name}.json");
        }
    }
}
