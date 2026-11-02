using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Admin;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Orders;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfAdminQueryServiceTests
    {
        [Fact]
        public void ListConversations_returns_summary_with_turncount()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "s1", CarBrand = "HONDA", ProspectName = "ก",
                Turns = new() { new ChatbotTurn { Role = "user", Text = "hi" }, new ChatbotTurn { Role = "assistant", Text = "ค่ะ" } }
            });
            var q = new EfAdminQueryService(f);

            var list = q.ListConversations(50, 0, null);
            Assert.Single(list);
            Assert.Equal(2, list[0].TurnCount);
            Assert.Equal("HONDA", list[0].CarBrand);
        }

        [Fact]
        public void GetConversation_returns_turns_ordered()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "s1",
                Turns = new() { new ChatbotTurn { Role = "user", Text = "a" }, new ChatbotTurn { Role = "assistant", Text = "b" } }
            });
            var q = new EfAdminQueryService(f);
            var id = q.ListConversations(50, 0, null)[0].Id;

            var detail = q.GetConversation(id);
            Assert.NotNull(detail);
            Assert.Equal(2, detail.Turns.Count);
            Assert.Equal(0, detail.Turns[0].Seq);
        }

        [Fact]
        public void ListOrders_returns_orders_newest_first()
        {
            using var f = new TestDbContextFactory();
            var os = new EfOrderStore(f);
            os.CreateOrder(new ChatbotConversationContext { ProspectName = "ก", ProspectPhone = "0800000000" },
                new ChatbotProposalSummary { PackageId = 1, Brand = "HONDA", Model = "CIVIC", Year = 2020, InsurerName = "x", Premium = 1, Total = 1 }, "sent");
            var q = new EfAdminQueryService(f);

            var orders = q.ListOrders(50, 0);
            Assert.Single(orders);
            Assert.StartsWith("ORD-", orders[0].OrderNo);
        }
    }
}
