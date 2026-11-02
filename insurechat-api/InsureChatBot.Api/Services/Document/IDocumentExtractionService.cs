using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;

namespace InsureChatBot.Api.Services.Document
{
    public interface IDocumentExtractionService
    {
        /// <summary>ส่งรูป (PNG/JPEG bytes) ให้ vision LLM → classify + extract คอลเดียว</summary>
        Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default);
    }
}
