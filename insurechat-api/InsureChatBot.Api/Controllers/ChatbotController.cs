using System.Text.Encodings.Web;
using System.Text.Json;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    // chatbot เป็น public — ไม่ต้อง login (ADR 0011). identity (ชื่อ/เบอร์) มาจาก PII intake ในแชต ไม่ใช่ JWT
    [AllowAnonymous]
    [Route("api/Chatbot")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        // identity จาก JWT (Program ตั้ง claim names: custCode / name / email / role)
        private string UserCode => User.FindFirst("custCode")?.Value ?? "";
        private string UserName => User.FindFirst("name")?.Value ?? "";
        private string UserEmail => User.FindFirst("email")?.Value ?? "";
        private string UserRole => User.FindFirst("role")?.Value ?? UserRoles.Customer;

        [HttpPost("Message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatbotMessageRequest request)
        {
            try
            {
                var response = await _chatbotService.ProcessMessageAsync(
                    userCode: UserCode,
                    userName: UserName,
                    userEmail: UserEmail,
                    role: UserRole,
                    message: request.Message,
                    conversationContext: request.ConversationContext ?? new ChatbotConversationContext());
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // NDJSON serialize: camelCase ให้ frontend อ่าน, ไม่ escape ไทยเป็น \uXXXX (ADR 0009)
        private static readonly JsonSerializerOptions StreamJson = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        [HttpPost("MessageStream")]
        public async Task SendMessageStream([FromBody] ChatbotMessageRequest request, CancellationToken ct)
        {
            Response.ContentType = "application/x-ndjson; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // กัน proxy buffer สตรีม

            async Task Write(ChatStreamEvent ev)
            {
                await Response.WriteAsync(JsonSerializer.Serialize(ev, StreamJson) + "\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            try
            {
                await foreach (var ev in _chatbotService.StreamMessageAsync(
                    UserCode, UserName, UserEmail, UserRole,
                    request.Message, request.ConversationContext ?? new ChatbotConversationContext(), ct))
                {
                    await Write(ev);
                }
            }
            catch (OperationCanceledException) { /* client ปิด/ยกเลิก — เงียบ */ }
            catch (Exception ex)
            {
                try { await Write(ChatStreamEvent.Error(ex.Message)); } catch { /* stream ปิดแล้ว */ }
            }
        }

        [HttpPost("Propose")]
        public async Task<IActionResult> SubmitProposal([FromBody] ChatbotProposalRequest request)
        {
            try
            {
                var result = await _chatbotService.SubmitProposalAsync(
                    userCode: UserCode,
                    userName: UserName,
                    userEmail: UserEmail,
                    role: UserRole,
                    conversationContext: request.ConversationContext);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("Suggest")]
        public async Task<IActionResult> Suggest([FromBody] ChatbotSuggestRequest request)
        {
            try
            {
                var suggestions = await _chatbotService.GenerateSuggestionsAsync(
                    request?.ConversationContext ?? new ChatbotConversationContext());
                return Ok(new ChatbotSuggestResponse { Suggestions = suggestions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("History")]
        public async Task<IActionResult> GetConversationHistory()
        {
            try
            {
                var history = await _chatbotService.GetConversationHistoryAsync(UserCode);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
