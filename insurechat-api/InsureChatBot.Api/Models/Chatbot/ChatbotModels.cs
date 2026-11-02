using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Models.Chatbot
{
    public class ChatbotMessageRequest
    {
        public string Message { get; set; }
        public ChatbotConversationContext ConversationContext { get; set; }
    }

    public class ChatbotMessageResponse
    {
        // คำตอบแตกเป็นหลายกล่อง (bubble split) — frontend ทยอยเผยทีละกล่อง. ดู ADR 0006
        public List<string> Replies { get; set; } = new List<string>();
        // คงไว้เป็น fallback (= string.Join ของ Replies) เผื่อ client เก่า
        public string Reply { get; set; }
        // ปุ่มถามด่วน (suggestion chips) สำหรับเทิร์นถัดไป — แต่ละชิ้นเป็นวลีที่กด=ส่งได้เลย. ดู ADR 0008
        public List<string> Suggestions { get; set; } = new List<string>();
        public ChatbotConversationContext UpdatedContext { get; set; }
        public bool IsReadyToPropose { get; set; }
        public ChatbotProposalSummary ProposalSummary { get; set; }
    }

    // /Chatbot/Suggest — ขอเฉพาะปุ่มถามด่วนชุดแรก (ตอนเปิดแชต greeting เป็น template instant). ดู ADR 0008
    public class ChatbotSuggestRequest
    {
        public ChatbotConversationContext ConversationContext { get; set; }
    }

    public class ChatbotSuggestResponse
    {
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    // /Chatbot/MessageStream — 1 บรรทัด NDJSON ต่อ 1 event (ADR 0009)
    // type: status (ระหว่าง tool loop) | delta (token คำตอบ) | suggestions | done | error
    public class ChatStreamEvent
    {
        public string Type { get; set; }
        public string Text { get; set; }                              // status / delta / error
        public List<string> Items { get; set; }                       // suggestions
        public ChatbotConversationContext Context { get; set; }       // done → state กลับ
        public bool IsReadyToPropose { get; set; }                    // done

        public static ChatStreamEvent Status(string t) => new() { Type = "status", Text = t };
        public static ChatStreamEvent Delta(string t) => new() { Type = "delta", Text = t };
        public static ChatStreamEvent Suggest(List<string> items) => new() { Type = "suggestions", Items = items };
        public static ChatStreamEvent Done(ChatbotConversationContext ctx, bool ready) =>
            new() { Type = "done", Context = ctx, IsReadyToPropose = ready };
        public static ChatStreamEvent Error(string t) => new() { Type = "error", Text = t };
    }

    public class ChatbotConversationContext
    {
        // รหัส session (ตั้งฝั่ง server เทิร์นแรก, round-trip กลับผ่าน context — ใช้ correlate การเก็บลง DB, ADR 0016 Phase 2)
        public string SessionId { get; set; }
        public List<ChatbotTurn> Turns { get; set; } = new List<ChatbotTurn>();

        // "ask_product", "ask_brand", "ask_model", "ask_year", "ask_class",
        // "ask_sum", "spin_deep", "ask_customer", "ready_to_propose"
        public string CurrentStep { get; set; }

        // Product info collected
        public string ProductType { get; set; } // "motor", "property", "pa", "group"
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int? CarYear { get; set; }
        public string InsuranceClass { get; set; } // "1", "2+", "3+", "2", "3"
        public decimal? SumInsure { get; set; }

        // จังหวัดจดทะเบียน (ADR 0005 — เก็บลง proposal, ไม่กรองผลค้น).
        // เพิ่มใน ADR 0019: เดิม quote ส่งมาแต่ context ไม่มีฟิลด์ → ค่าหายตอน round-trip
        public string Province { get; set; }
        public string ProvinceLabel { get; set; }

        // Needs discovered (SPIN)
        public List<string> DiscoveredNeeds { get; set; } = new List<string>();
        public string SpinReason { get; set; } // why customer wants a certain class

        // Prospect identity (ผู้มุ่งหวัง = เจ้าของ proposal).
        // role=agent → เก็บจาก step ask_customer; role=customer → เติมจาก profile ที่ login
        public string ProspectName { get; set; }
        public string ProspectEmail { get; set; }

        // รายละเอียดเพิ่มสำหรับออกกรมธรรม์จริง (step ask_customer_detail) — จำเป็นเมื่อ T Broker = live
        public string ProspectPhone { get; set; }
        public string ProspectIdCard { get; set; }
        public string ProspectLicensePlate { get; set; }

        // Proposals (shortlist หลัง AI กรอง — Proposals[0] = ที่ลูกค้าเลือกหลัง prepare_proposal)
        public List<ChatbotProposalSummary> Proposals { get; set; } = new List<ChatbotProposalSummary>();

        // Candidate set จากการค้น 5 ฟิลด์ (quote form) — AI สโคป/กรองชุดนี้ (ADR 0005)
        public List<PackageRow> Candidates { get; set; } = new List<PackageRow>();
    }

    public class ChatbotTurn
    {
        public DateTime Timestamp { get; set; }
        public string Role { get; set; } // "user" or "assistant"
        public string Text { get; set; }
    }

    public class ChatbotProposalRequest
    {
        public ChatbotConversationContext ConversationContext { get; set; }
    }

    public class ChatbotProposalResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TransactionId { get; set; }
    }

    public class ChatbotProposalSummary
    {
        public int PackageId { get; set; }   // ใช้ดึงรายละเอียดความคุ้มครองเต็ม (modal "ดูรายละเอียด")
        public string Brand { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public decimal SumInsure { get; set; }
        public string InsurerId { get; set; }
        public string InsurerName { get; set; }
        public decimal Premium { get; set; }
        public decimal Total { get; set; }
    }

    public class ChatbotConversationHistory
    {
        public string UserCode { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastUpdatedDate { get; set; }
        public ChatbotConversationContext Context { get; set; }
        public List<ChatbotProposalSummary> CompletedProposals { get; set; } = new List<ChatbotProposalSummary>();
    }
}
