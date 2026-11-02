using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Email;
using InsureChatBot.Api.Services.Interfaces;
using InsureChatBot.Api.Services.Orders;
using InsureChatBot.Api.Services.Settings;
using InsureChatBot.Api.Services.Line;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Implementations
{
    /// <summary>
    /// Router: เลือก engine บทสนทนา — LLM เป็นหลัก, ตกไป state machine เมื่อ LLM ไม่พร้อม/ผิดพลาด (ADR 0003).
    /// SubmitProposal/History เป็น deterministic (ไม่ขึ้นกับ engine).
    /// </summary>
    public class ChatbotService : IChatbotService
    {
        private readonly LlmConversationEngine _llm;
        private readonly StateMachineConversationEngine _stateMachine;
        private readonly IOrderEmailService _orderEmail;
        private readonly LlmSettings _llmSettings;
        private readonly IConversationStore _conversations;
        private readonly IOrderStore _orders;
        private readonly ILineService _lineService;
        private readonly LineSettings _lineSettings;
        private readonly ILogger<ChatbotService> _logger;

        public ChatbotService(
            LlmConversationEngine llm,
            StateMachineConversationEngine stateMachine,
            IOrderEmailService orderEmail,
            IAppSettingsStore settings,
            IConversationStore conversations,
            IOrderStore orders,
            ILineService lineService,
            IOptions<LineSettings> lineOptions,
            ILogger<ChatbotService> logger)
        {
            _llm = llm;
            _stateMachine = stateMachine;
            _orderEmail = orderEmail;
            _llmSettings = settings.GetEffectiveLlm();
            _conversations = conversations;
            _orders = orders;
            _lineService = lineService;
            _lineSettings = lineOptions.Value;
            _logger = logger;
        }

        public async Task<ChatbotMessageResponse> ProcessMessageAsync(
            string userCode, string userName, string userEmail, string role,
            string message, ChatbotConversationContext context)
        {
            ChatbotMessageResponse response;
            if (_llmSettings.IsConfigured)
            {
                try { response = await _llm.ProcessMessageAsync(userCode, userName, userEmail, role, message, context); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM engine ล้มเหลว — fallback ไป state machine");
                    response = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
                }
            }
            else
            {
                response = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
            }
            PersistConversation(context, response.UpdatedContext);
            return response;
        }

        public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
            string userCode, string userName, string userEmail, string role,
            string message, ChatbotConversationContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // LLM mode → สตรีมจริง (error ระหว่างทาง ปล่อยให้ controller จับ → ส่ง event error)
            if (_llmSettings.IsConfigured)
            {
                await foreach (var ev in _llm.ProcessMessageStreamAsync(userCode, userName, userEmail, role, message, context, ct))
                {
                    if (ev.Type == "done" && ev.Context != null)
                        PersistConversation(context, ev.Context);
                    yield return ev;
                }
                yield break;
            }

            // ไม่ตั้งค่า LLM → state machine one-shot: ปล่อยทั้งคำตอบเป็น delta เดียว + suggestions + done (UI เดิน path เดียว)
            var resp = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
            var text = (resp.Replies != null && resp.Replies.Count > 0) ? string.Join("\n\n", resp.Replies) : resp.Reply;
            if (!string.IsNullOrWhiteSpace(text)) yield return ChatStreamEvent.Delta(text);
            if (resp.Suggestions is { Count: > 0 }) yield return ChatStreamEvent.Suggest(resp.Suggestions);
            PersistConversation(context, resp.UpdatedContext);
            yield return ChatStreamEvent.Done(resp.UpdatedContext, resp.IsReadyToPropose);
        }

        public async Task<ChatbotProposalResponse> SubmitProposalAsync(
            string userCode, string userName, string userEmail, string role,
            ChatbotConversationContext context)
        {
            try
            {
                if (context?.Proposals == null || context.Proposals.Count == 0)
                    return new ChatbotProposalResponse { Success = false, Message = "ไม่มีแพคเกจที่เลือก โปรดทำการค้นหาแพคเกจใหม่" };

                // บังคับ identity: ชื่อ + เบอร์โทร ต้องครบก่อนเสนอแพคเกจ (ADR-aligned, CONTEXT "Required identity")
                var prospectName = (context.ProspectName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(prospectName))
                    return new ChatbotProposalResponse { Success = false, Message = "ขอชื่อผู้ติดต่อในแชตก่อนส่งคำสั่งซื้อนะคะ" };

                var prospectPhone = new string((context.ProspectPhone ?? "").Where(char.IsDigit).ToArray());
                if (prospectPhone.Length < 9 || prospectPhone.Length > 10)
                    return new ChatbotProposalResponse { Success = false, Message = "ขอเบอร์โทร (9–10 หลัก) ในแชตก่อนส่งคำสั่งซื้อนะคะ" };

                var selected = context.Proposals[0];

                // ส่งคำสั่งซื้อทางอีเมลถึงโบรคเกอร์ (ADR 0012) — แทน partner POST เดิม
                var order = new OrderEmailModel
                {
                    CustomerName = prospectName,
                    CustomerPhone = prospectPhone,
                    CustomerEmail = !string.IsNullOrWhiteSpace(context.ProspectEmail) ? context.ProspectEmail : userEmail,
                    IdCard = context.ProspectIdCard,
                    LicensePlate = context.ProspectLicensePlate,
                    Brand = selected.Brand,
                    Model = selected.Model,
                    Year = selected.Year,
                    InsuranceClass = selected.InsuranceClass,
                    SumInsure = selected.SumInsure,
                    PremiumNet = selected.Premium,
                    PremiumTotal = selected.Total > 0 ? selected.Total : selected.Premium,
                    InsurerName = selected.InsurerName
                };

                var sent = await _orderEmail.SendAsync(order);
                if (!sent.Success)
                    return new ChatbotProposalResponse { Success = false, Message = "ส่งคำสั่งซื้อไม่สำเร็จ กรุณาลองใหม่อีกครั้งนะคะ" };

                // บันทึกคำสั่งซื้อลง DB + mark conversation = ordered (ADR 0016 Phase 2) — ไม่ให้พังถ้า DB ล่ม
                string orderNo = null;
                try { orderNo = _orders.CreateOrder(context, selected, "sent"); }
                catch (Exception ex) { _logger.LogWarning(ex, "บันทึก order ลง DB ล้มเหลว (อีเมลส่งแล้ว)"); }

                // แจ้งเตือนเข้า LINE อัตโนมัติ:
                // 1) หากลูกค้าคุยผ่าน LINE -> แจ้งลูกค้า
                // 2) หากลูกค้าสั่งซื้อผ่านหน้าเว็บ -> ส่งแจ้งเตือนหา LINE Admin ทันที!
                try
                {
                    var targetLineUserId = context.SessionId?.StartsWith("line_") == true 
                        ? context.SessionId.Substring(5) 
                        : _lineSettings.AdminUserId;

                    if (!string.IsNullOrWhiteSpace(targetLineUserId))
                    {
                        var flexCard = LineMessageBuilder.CreateInsuranceProposalFlex(selected, context);
                        var isWebOrder = !context.SessionId?.StartsWith("line_") == true;
                        
                        var messageHeader = isWebOrder 
                            ? $"🔔 [มีคำสั่งซื้อใหม่จากหน้าเว็บ!]\nเลขที่: {orderNo ?? "IC-ORDER"}\nผู้ติดต่อ: คุณ{prospectName}\nเบอร์โทร: {prospectPhone}\nแผน: {selected.InsurerName} ({selected.Brand} {selected.Model})"
                            : $"🎉 ขอบคุณคุณ {prospectName} ครับ!\nระบบได้บันทึกคำสั่งซื้อเรียบร้อยแล้ว (เลขที่: {orderNo ?? "IC-ORDER"})\nเจ้าหน้าที่ผู้เชี่ยวชาญจะติดต่อกลับที่เบอร์ {prospectPhone} ในเร็วๆ นี้นะครับ 😊";

                        var confirmNotice = new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = messageHeader
                        };

                        _ = _lineService.PushMessageAsync(targetLineUserId, new List<object> { flexCard, confirmNotice });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ส่งแจ้งเตือนเข้า LINE ไม่สำเร็จ");
                }

                return new ChatbotProposalResponse
                {
                    Success = true,
                    TransactionId = orderNo,
                    Message = "ระบบจะส่งคำสั่งซื้อไปยังโบรคเกอร์ เพื่อให้เจ้าหน้าที่ติดต่อกลับ โปรดดำเนินการตามเงื่อนไขที่เจ้าหน้าที่แจ้ง เพื่อดำเนินการออกกรมธรรม์ต่อไป"
                };
            }
            catch (Exception ex)
            {
                return new ChatbotProposalResponse { Success = false, Message = $"Error submitting proposal: {ex.Message}" };
            }
        }

        /// <summary>ตั้ง SessionId (ถ้ายังไม่มี) บน context ขาออก แล้ว persist — ห่อ try/catch ไม่ให้กระทบแชต</summary>
        private void PersistConversation(ChatbotConversationContext incoming, ChatbotConversationContext updated)
        {
            if (updated == null) return;
            var sid = !string.IsNullOrWhiteSpace(updated.SessionId) ? updated.SessionId
                    : !string.IsNullOrWhiteSpace(incoming?.SessionId) ? incoming.SessionId
                    : Guid.NewGuid().ToString("N");
            updated.SessionId = sid;
            try { _conversations.Upsert(updated); }
            catch (Exception ex) { _logger.LogWarning(ex, "persist conversation ล้มเหลว (ไม่กระทบแชต)"); }
        }

        public async Task<List<string>> GenerateSuggestionsAsync(ChatbotConversationContext context)
        {
            if (_llmSettings.IsConfigured)
            {
                try { return await _llm.SuggestAsync(context); }
                catch (Exception ex) { _logger.LogWarning(ex, "SuggestAsync ล้มเหลว — ใช้ปุ่ม fallback"); }
            }
            return FallbackSuggestions(context);
        }

        /// <summary>ปุ่ม fallback แบบ deterministic เมื่อ LLM ไม่พร้อม/ล่ม — อิงว่ามี candidate ไหม (ADR 0008).</summary>
        private static List<string> FallbackSuggestions(ChatbotConversationContext context)
        {
            if ((context?.Candidates?.Count ?? 0) == 0) return new List<string>();
            return new List<string> { "ขอแผนถูกสุด", "เพิ่มทุนประกัน", "ดูทั้งหมด" };
        }

        public Task<ChatbotConversationHistory> GetConversationHistoryAsync(string userCode)
        {
            return Task.FromResult(new ChatbotConversationHistory
            {
                UserCode = userCode,
                CreatedDate = DateTime.Now,
                LastUpdatedDate = DateTime.Now,
                Context = new ChatbotConversationContext(),
                CompletedProposals = new List<ChatbotProposalSummary>()
            });
        }
    }
}
