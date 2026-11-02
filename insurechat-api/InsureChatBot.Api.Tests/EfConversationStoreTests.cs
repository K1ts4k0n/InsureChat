using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Conversation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfConversationStoreTests
    {
        private static ChatbotConversationContext Ctx(string sid) => new()
        {
            SessionId = sid,
            ProductType = "motor", CarBrand = "HONDA", CarModel = "CIVIC", CarYear = 2020,
            CurrentStep = "llm",
            DiscoveredNeeds = new() { "กลัวน้ำท่วม" },
            ProspectName = "สมชาย", ProspectPhone = "0812345678",
            Turns = new()
            {
                new ChatbotTurn { Role = "assistant", Text = "สวัสดีค่ะ", Timestamp = DateTime.UtcNow },
                new ChatbotTurn { Role = "user", Text = "ฮอนดา ซีวิค", Timestamp = DateTime.UtcNow }
            }
        };

        [Fact]
        public void Upsert_creates_conversation_with_turns_and_needs()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);

            store.Upsert(Ctx("sess-1"));

            using var db = f.CreateDbContext();
            var conv = db.Conversations.Include(c => c.Turns).Include(c => c.Needs)
                        .Single(c => c.SessionKey == "sess-1");
            Assert.Equal("HONDA", conv.CarBrand);
            Assert.Equal("open", conv.OutcomeStatus);
            Assert.Equal(2, conv.Turns.Count);
            Assert.Single(conv.Needs);
            Assert.Equal(0, conv.Turns.OrderBy(t => t.Seq).First().Seq);
        }

        [Fact]
        public void Upsert_same_session_updates_and_replaces_turns_no_duplicate()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);

            store.Upsert(Ctx("sess-1"));
            var ctx2 = Ctx("sess-1");
            ctx2.Turns.Add(new ChatbotTurn { Role = "assistant", Text = "ทุนเท่าไหร่คะ", Timestamp = DateTime.UtcNow });
            ctx2.CarModel = "CITY";
            store.Upsert(ctx2);

            using var db = f.CreateDbContext();
            Assert.Equal(1, db.Conversations.Count(c => c.SessionKey == "sess-1"));
            var conv = db.Conversations.Include(c => c.Turns).Single(c => c.SessionKey == "sess-1");
            Assert.Equal("CITY", conv.CarModel);
            Assert.Equal(3, conv.Turns.Count);
            Assert.Equal(3, db.ConversationTurns.Count());   // no orphan turns left
        }

        [Fact]
        public void MarkOrdered_sets_outcome_status()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);
            store.Upsert(Ctx("sess-1"));

            store.MarkOrdered("sess-1");

            using var db = f.CreateDbContext();
            Assert.Equal("ordered", db.Conversations.Single(c => c.SessionKey == "sess-1").OutcomeStatus);
        }

        [Fact]
        public void FindConversationId_returns_id_or_null()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);
            store.Upsert(Ctx("sess-1"));

            Assert.NotNull(store.FindConversationId("sess-1"));
            Assert.Null(store.FindConversationId("nope"));
        }
    }
}
