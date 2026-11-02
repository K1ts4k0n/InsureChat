using InsureChatBot.Api.Models.Admin;

namespace InsureChatBot.Api.Services.Admin
{
    /// <summary>read-only query สำหรับหน้า admin (ADR 0016 Phase 3) — conversations/orders.</summary>
    public interface IAdminQueryService
    {
        List<ConversationSummaryDto> ListConversations(int take, int skip, string search);
        ConversationDetailDto GetConversation(long id);
        List<OrderListDto> ListOrders(int take, int skip);
    }
}
