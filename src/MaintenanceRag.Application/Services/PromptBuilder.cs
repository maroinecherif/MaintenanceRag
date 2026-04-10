using System.Text;
using MaintenanceRag.Application.DTOs;

namespace MaintenanceRag.Application.Services;

public sealed class PromptBuilder
{
    public string Build(string question, IReadOnlyList<IncidentMatchDto> incidents)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Tu es un expert en maintenance industrielle.");
        sb.AppendLine();
        sb.AppendLine("Analyse les incidents suivants :");
        sb.AppendLine();

        for (int i = 0; i < incidents.Count; i++)
            sb.AppendLine($"Incident {i + 1}: {incidents[i].SearchText}");

        sb.AppendLine();
        sb.AppendLine("Réponds clairement en :");
        sb.AppendLine("- listant les incidents fréquents");
        sb.AppendLine("- expliquant brièvement les causes");
        sb.AppendLine("- suggérant des actions correctives");
        sb.AppendLine();
        sb.AppendLine("Réponds uniquement à partir des données fournies.");
        sb.AppendLine();
        sb.AppendLine("Question :");
        sb.AppendLine(question);

        return sb.ToString();
    }
}
