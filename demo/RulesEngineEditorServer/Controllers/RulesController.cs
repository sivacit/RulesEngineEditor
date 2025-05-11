using System;
using System.Linq;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // ✅ Needed for ILogger
using RulesEngineEditorServer.Data;
using RulesEngineEditor.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace RulesEngineEditorServer.Controllers
{
    [Route("api/rules")]
    [ApiController]
    public class RulesController : ControllerBase
    {
        private readonly RulesDbContext _context;
        private readonly ILogger<RulesController> _logger; // ✅ Declare logger

        public RulesController(RulesDbContext context, ILogger<RulesController> logger)
        {
            _context = context;
            _logger = logger;
        }



        [HttpPost]
        public async Task<IActionResult> SaveRules([FromBody] RulesWorkflow model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                Console.WriteLine("❌ ModelState Errors: " + errors); // Server-side
                return BadRequest("ModelState Errors: " + errors);
            }


            if (string.IsNullOrWhiteSpace(model.WorkflowName) || string.IsNullOrWhiteSpace(model.RulesJson))
            {
                return BadRequest("Missing required fields.");
            }

            var existing = await _context.RulesWorkflows
                .FirstOrDefaultAsync(w => w.WorkflowName == model.WorkflowName);

            if (existing != null)
            {
                existing.RulesJson = model.RulesJson;
                existing.CreatedAt = DateTime.UtcNow;
                _context.Update(existing);
            }
            else
            {
                model.CreatedAt = DateTime.UtcNow;
                _context.Add(model);
            }

            await _context.SaveChangesAsync();
            return Ok("Saved");
        }


        [HttpGet("{workflowName}")]
        public async Task<IActionResult> GetRules(string workflowName)
        {
            _logger.LogInformation("Fetching workflow: {WorkflowName}", workflowName);

            var workflow = await _context.RulesWorkflows
                .FirstOrDefaultAsync(w => w.WorkflowName == workflowName);

            if (workflow == null)
            {
                _logger.LogWarning("Workflow not found: {WorkflowName}", workflowName);
                return NotFound();
            }

            return Ok(workflow);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRules()
        {
            var workflows = await _context.RulesWorkflows
                .Select(w => new { w.Id, w.WorkflowName })
                .ToListAsync();

            return Ok(workflows);
        }
    }
}
