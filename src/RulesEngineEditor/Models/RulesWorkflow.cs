using System;
using System.ComponentModel.DataAnnotations;
namespace RulesEngineEditor.Models
{
    public class RulesWorkflow
    {
        [Key]
        public int Id { get; set; }

        public string WorkflowName { get; set; } = string.Empty;


        public string RulesJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
