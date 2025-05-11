using Microsoft.EntityFrameworkCore;
using RulesEngineEditor.Models;

namespace RulesEngineEditorServer.Data
{
    public class RulesDbContext : DbContext
    {
        public RulesDbContext(DbContextOptions<RulesDbContext> options) : base(options) { }

        public DbSet<RulesWorkflow> RulesWorkflows { get; set; }
    }
}
