using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Line;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Interfaces;
using InsureChatBot.Api.Services.Line;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LineWebhookController : ControllerBase
    {
        private readonly ILineService _lineService;
        private readonly IChatbotService _chatbotService;
        private readonly ILineSessionManager _sessionManager;
        private readonly IConversationStore _conversationStore;
        private readonly LineSettings _lineSettings;
        private readonly ILogger<LineWebhookController> _logger;

        public LineWebhookController(
            ILineService lineService,
            IChatbotService chatbotService,
            ILineSessionManager sessionManager,
            IConversationStore conversationStore,
            IOptions<LineSettings> lineSettings,
            ILogger<LineWebhookController> logger)
        {
            _lineService = lineService;
            _chatbotService = chatbotService;
            _sessionManager = sessionManager;
            _conversationStore = conversationStore;
            _lineSettings = lineSettings.Value;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var signature = Request.Headers["x-line-signature"].FirstOrDefault() ?? "";
            var isSignatureValid = _lineService.VerifySignature(body, _lineSettings.ChannelSecret, signature);
            if (!isSignatureValid)
            {
                _logger.LogWarning("LINE signature mismatch (Incoming: '{Signature}'). Bypassing for dev.", signature);
            }

            LineWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<LineWebhookPayload>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse LINE webhook payload");
                return BadRequest();
            }

            if (payload?.Events == null || payload.Events.Count == 0)
                return Ok();

            foreach (var evt in payload.Events)
            {
                if (evt.Type == "message" && evt.Message?.Type == "text" && !string.IsNullOrWhiteSpace(evt.ReplyToken))
                {
                    var userId = evt.Source?.UserId ?? "anonymous";
                    var userMsg = evt.Message.Text?.Trim() ?? "";

                    _logger.LogInformation("Received message from LINE user: {UserId}: {Text}", userId, userMsg);

                    try
                    {
                        var context = _sessionManager.GetOrCreateContext(userId);

                        // ส่งประมวลผลผ่านสมอง InsureChat (LLM / Consultative Engine)
                        var result = await _chatbotService.ProcessMessageAsync(
                            userCode: userId,
                            userName: context.ProspectName ?? "ลูกค้า LINE",
                            userEmail: "",
                            role: "customer",
                            message: userMsg,
                            conversationContext: context
                        );

                        // บันทึกสถานะบทสนทนา
                        _sessionManager.SaveContext(userId, context);
                        _conversationStore.Upsert(context);

                        // แปลงเป็น LINE Flex Messages / Quick Replies
                        var replyMessages = LineMessageBuilder.BuildReplyMessages(
                            result.Replies,
                            result.Suggestions,
                            context
                        );

                        // ส่งกลับหาลูกค้าผ่าน LINE API
                        await _lineService.ReplyMessageAsync(evt.ReplyToken, replyMessages);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing LINE message for user: {UserId}", userId);
                    }
                }
            }

            return Ok();
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                status = "online",
                hasToken = !string.IsNullOrWhiteSpace(_lineSettings.ChannelAccessToken),
                hasSecret = !string.IsNullOrWhiteSpace(_lineSettings.ChannelSecret),
                message = "LINE Webhook endpoint ready at /api/linewebhook"
            });
        }
    }
}
