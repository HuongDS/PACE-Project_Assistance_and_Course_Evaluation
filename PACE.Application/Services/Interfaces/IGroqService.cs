using System.Threading.Tasks;

namespace PACE.Application.Services.Interfaces
{
    public interface IGroqService
    {
        Task<string> EvaluateWithGroqAsync(string systemPrompt, string userPrompt, string? model = null);
        bool IsConfigured { get; }
    }
}
