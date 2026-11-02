using System.Collections.Concurrent;
using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Line
{
    public interface ILineSessionManager
    {
        ChatbotConversationContext GetOrCreateContext(string lineUserId);
        void SaveContext(string lineUserId, ChatbotConversationContext context);
    }

    public class LineSessionManager : ILineSessionManager
    {
        // จำ context ของแต่ละ LINE User ไว้ใน Memory (SessionId = line_{userId})
        private readonly ConcurrentDictionary<string, ChatbotConversationContext> _sessions = new();

        public ChatbotConversationContext GetOrCreateContext(string lineUserId)
        {
            return _sessions.GetOrAdd(lineUserId, id => new ChatbotConversationContext
            {
                SessionId = $"line_{id}",
                ProspectName = "ลูกค้า LINE",
                Turns = new List<ChatbotTurn>()
            });
        }

        public void SaveContext(string lineUserId, ChatbotConversationContext context)
        {
            _sessions[lineUserId] = context;
        }
    }
}
