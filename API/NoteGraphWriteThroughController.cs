using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class NoteGraphWriteThroughController
    {
        private readonly ILogger<NoteGraphWriteThroughController> _logger;
        private readonly INoteGraphWriteThroughService _noteGraphWriteThroughService;

        public NoteGraphWriteThroughController(
            ILogger<NoteGraphWriteThroughController> logger,
            INoteGraphWriteThroughService noteGraphWriteThroughService
            )
        {
            _logger = logger;
            _noteGraphWriteThroughService = noteGraphWriteThroughService;
        }

        [Authorize]
        [HttpPut("{noteGraphId:Guid}")]
        public async Task<bool> Save(Guid noteGraphId, [FromBody] NoteGraphDocumentREADONLY document)
        {
            bool result = await _noteGraphWriteThroughService.ReplaceAsync(document, noteGraphId);
            if (!result)
            {
                return false;
            }
            return true;
        }
    }
}
