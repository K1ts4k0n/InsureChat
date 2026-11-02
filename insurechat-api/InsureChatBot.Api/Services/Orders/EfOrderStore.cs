using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Chatbot;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Orders
{
    /// <summary>IOrderStore บน EF Core (Singleton + IDbContextFactory) — ADR 0016 Phase 2.</summary>
    public class EfOrderStore : IOrderStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfOrderStore(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public string CreateOrder(ChatbotConversationContext ctx, ChatbotProposalSummary p, string emailStatus)
        {
            using var db = _factory.CreateDbContext();
            var now = DateTime.UtcNow;

            long? convId = null;
            if (!string.IsNullOrWhiteSpace(ctx?.SessionId))
                convId = db.Conversations.Where(c => c.SessionKey == ctx.SessionId)
                           .Select(c => (long?)c.Id).FirstOrDefault();

            var phone = new string((ctx?.ProspectPhone ?? "").Where(char.IsDigit).ToArray());
            var order = new OrderRow
            {
                OrderNo = "",
                ConversationId = convId,
                CustName = ctx?.ProspectName,
                CustPhone = phone,
                CustEmail = ctx?.ProspectEmail,
                CustIdCard = ctx?.ProspectIdCard,
                LicensePlate = ctx?.ProspectLicensePlate,
                Brand = p.Brand, Model = p.Model, Year = p.Year,
                InsuranceClass = p.InsuranceClass, PackageId = p.PackageId, InsurerName = p.InsurerName,
                SumInsure = p.SumInsure, Premium = p.Premium, Total = p.Total > 0 ? p.Total : p.Premium,
                Status = "ordered", EmailStatus = emailStatus,
                EmailSentAt = emailStatus == "sent" ? now : (DateTime?)null,
                CreatedAt = now, UpdatedAt = now
            };
            db.Orders.Add(order);
            db.SaveChanges();                         // ได้ Id

            // ใช้ now.Year (ค.ศ. เสมอ) ไม่ใช่ {now:yyyy} ที่กลายเป็น พ.ศ. เมื่อ server culture เป็นไทย — ให้ OrderNo deterministic ข้าม environment
            order.OrderNo = $"ORD-{now.Year}-{order.Id:D6}";
            if (convId.HasValue)
            {
                var conv = db.Conversations.FirstOrDefault(c => c.Id == convId.Value);
                if (conv != null) { conv.OutcomeStatus = "ordered"; conv.LastUpdatedAt = now; }
            }
            db.SaveChanges();                         // เซฟ OrderNo + status
            return order.OrderNo;
        }
    }
}
