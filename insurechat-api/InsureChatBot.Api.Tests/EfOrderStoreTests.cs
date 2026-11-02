using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Orders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfOrderStoreTests
    {
        private static ChatbotProposalSummary Pkg() => new()
        {
            PackageId = 123, Brand = "HONDA", Model = "CIVIC", Year = 2020,
            InsuranceClass = "1", SumInsure = 500000m, InsurerName = "วิริยะ",
            Premium = 14614m, Total = 15640m
        };

        [Fact]
        public void CreateOrder_inserts_with_formatted_orderno_and_links_conversation()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "sess-1", ProspectName = "สมชาย", ProspectPhone = "0812345678"
            });
            var store = new EfOrderStore(f);

            var ctx = new ChatbotConversationContext
            {
                SessionId = "sess-1", ProspectName = "สมชาย", ProspectPhone = "081-234-5678",
                ProspectEmail = "a@b.c"
            };
            var orderNo = store.CreateOrder(ctx, Pkg(), "sent");

            Assert.StartsWith($"ORD-{DateTime.UtcNow.Year}-", orderNo);   // ค.ศ. เสมอ (กัน พ.ศ. จาก culture ไทย)
            using var db = f.CreateDbContext();
            var o = db.Orders.Single();
            Assert.Equal(orderNo, o.OrderNo);
            Assert.Equal("0812345678", o.CustPhone);   // strip non-digit
            Assert.Equal(123, o.PackageId);
            Assert.Equal(15640m, o.Total);
            Assert.Equal("sent", o.EmailStatus);
            Assert.NotNull(o.ConversationId);
            Assert.Equal("ordered", db.Conversations.Single().OutcomeStatus);
        }

        [Fact]
        public void CreateOrder_without_session_has_null_conversation()
        {
            using var f = new TestDbContextFactory();
            var store = new EfOrderStore(f);

            var orderNo = store.CreateOrder(new ChatbotConversationContext { ProspectName = "x", ProspectPhone = "0800000000" }, Pkg(), "sent");

            using var db = f.CreateDbContext();
            Assert.Null(db.Orders.Single().ConversationId);
            Assert.False(string.IsNullOrWhiteSpace(orderNo));
        }
    }
}
