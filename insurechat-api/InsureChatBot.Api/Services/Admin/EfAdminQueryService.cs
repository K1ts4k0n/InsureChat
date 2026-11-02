using InsureChatBot.Api.Data;
using InsureChatBot.Api.Models.Admin;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Admin
{
    /// <summary>IAdminQueryService บน EF Core (Singleton + IDbContextFactory).</summary>
    public class EfAdminQueryService : IAdminQueryService
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfAdminQueryService(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public List<ConversationSummaryDto> ListConversations(int take, int skip, string search)
        {
            using var db = _factory.CreateDbContext();
            var query = db.Conversations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c => (c.ProspectName != null && c.ProspectName.Contains(s))
                                      || (c.ProspectPhone != null && c.ProspectPhone.Contains(s))
                                      || (c.CarBrand != null && c.CarBrand.Contains(s)));
            }
            return query.OrderByDescending(c => c.LastUpdatedAt)
                .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
                .Select(c => new ConversationSummaryDto
                {
                    Id = c.Id, SessionKey = c.SessionKey, CarBrand = c.CarBrand, CarModel = c.CarModel, CarYear = c.CarYear,
                    ProspectName = c.ProspectName, ProspectPhone = c.ProspectPhone, OutcomeStatus = c.OutcomeStatus,
                    TurnCount = c.Turns.Count, LastUpdatedAt = c.LastUpdatedAt
                }).ToList();
        }

        public ConversationDetailDto GetConversation(long id)
        {
            using var db = _factory.CreateDbContext();
            var c = db.Conversations.AsNoTracking().Include(x => x.Turns).Include(x => x.Needs)
                      .FirstOrDefault(x => x.Id == id);
            if (c == null) return null;
            return new ConversationDetailDto
            {
                Id = c.Id, SessionKey = c.SessionKey, CarBrand = c.CarBrand, CarModel = c.CarModel, CarYear = c.CarYear,
                ProspectName = c.ProspectName, ProspectPhone = c.ProspectPhone, ProspectEmail = c.ProspectEmail,
                OutcomeStatus = c.OutcomeStatus, ProductType = c.ProductType, InsuranceClass = c.InsuranceClass,
                SumInsure = c.SumInsure, CreatedAt = c.CreatedAt, LastUpdatedAt = c.LastUpdatedAt,
                TurnCount = c.Turns.Count,
                Needs = c.Needs.Select(n => n.Need).ToList(),
                Turns = c.Turns.OrderBy(t => t.Seq).Select(t => new ConversationTurnDto
                { Seq = t.Seq, Role = t.Role, Text = t.Text, Timestamp = t.Timestamp }).ToList()
            };
        }

        public List<OrderListDto> ListOrders(int take, int skip)
        {
            using var db = _factory.CreateDbContext();
            return db.Orders.AsNoTracking().OrderByDescending(o => o.Id)
                .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
                .Select(o => new OrderListDto
                {
                    Id = o.Id, OrderNo = o.OrderNo, CustName = o.CustName, CustPhone = o.CustPhone,
                    Brand = o.Brand, Model = o.Model, InsurerName = o.InsurerName, Total = o.Total,
                    Status = o.Status, EmailStatus = o.EmailStatus, CreatedAt = o.CreatedAt
                }).ToList();
        }
    }
}
