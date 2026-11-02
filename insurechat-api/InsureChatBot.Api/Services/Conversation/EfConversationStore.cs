using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Chatbot;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>IConversationStore บน EF Core (Singleton + IDbContextFactory) — ADR 0016 Phase 2.</summary>
    public class EfConversationStore : IConversationStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfConversationStore(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public void Upsert(ChatbotConversationContext c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.SessionId)) return;
            using var db = _factory.CreateDbContext();
            var now = DateTime.UtcNow;

            var conv = db.Conversations
                .Include(x => x.Turns).Include(x => x.Needs)
                .FirstOrDefault(x => x.SessionKey == c.SessionId);
            if (conv == null)
            {
                conv = new ConversationRow { SessionKey = c.SessionId, OutcomeStatus = "open", CreatedAt = now };
                db.Conversations.Add(conv);
            }

            conv.ProductType = c.ProductType;
            conv.CarBrand = c.CarBrand;
            conv.CarModel = c.CarModel;
            conv.CarYear = c.CarYear;
            conv.InsuranceClass = c.InsuranceClass;
            conv.SumInsure = c.SumInsure;
            conv.CurrentStep = c.CurrentStep;
            conv.SpinReason = c.SpinReason;
            conv.ProspectName = c.ProspectName;
            conv.ProspectPhone = c.ProspectPhone;
            conv.ProspectEmail = c.ProspectEmail;
            conv.ProspectIdCard = c.ProspectIdCard;
            conv.ProspectLicensePlate = c.ProspectLicensePlate;
            conv.LastUpdatedAt = now;

            // แทนที่ turns/needs ด้วยชุดล่าสุด (context พก history เต็มทุกเทิร์น)
            // ลบผ่าน DbSet โดยตรงแทนการ reassign navigation เพื่อให้ EF tracking ถูกต้อง
            if (conv.Turns.Count > 0) db.ConversationTurns.RemoveRange(conv.Turns);
            if (conv.Needs.Count > 0) db.ConversationNeeds.RemoveRange(conv.Needs);

            var newTurns = (c.Turns ?? new()).Select((t, i) => new ConversationTurnRow
            {
                Seq = i, Role = t.Role, Text = t.Text,
                Timestamp = t.Timestamp == default ? now : t.Timestamp
            }).ToList();
            var newNeeds = (c.DiscoveredNeeds ?? new()).Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => new ConversationNeedRow { Need = n }).ToList();

            foreach (var t in newTurns) conv.Turns.Add(t);
            foreach (var n in newNeeds) conv.Needs.Add(n);

            db.SaveChanges();
        }

        public void MarkOrdered(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            using var db = _factory.CreateDbContext();
            var conv = db.Conversations.FirstOrDefault(x => x.SessionKey == sessionId);
            if (conv == null) return;
            conv.OutcomeStatus = "ordered";
            conv.LastUpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        public long? FindConversationId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            using var db = _factory.CreateDbContext();
            return db.Conversations.Where(x => x.SessionKey == sessionId)
                     .Select(x => (long?)x.Id).FirstOrDefault();
        }
    }
}
