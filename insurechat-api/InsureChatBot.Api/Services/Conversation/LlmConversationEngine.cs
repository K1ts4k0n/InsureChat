using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Llm;
using InsureChatBot.Api.Services.Settings;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>
    /// Engine ขับด้วย LLM — สโคปความคุ้มครอง/งบจาก candidate set (จากฟอร์ม 5 ฟิลด์) แล้วกรองเหลือ shortlist.
    /// ข้อมูลจริงผ่าน tool filter_packages เท่านั้น (grounding). ดู ADR 0003, 0005.
    /// </summary>
    public class LlmConversationEngine : IConversationEngine
    {
        private readonly IChatCompletionClient _client;
        private readonly SystemPromptProvider _prompt;
        private readonly LlmSettings _settings;
        private readonly ILogger<LlmConversationEngine> _logger;

        private const int DefaultLimit = 3;
        private const int HardCap = 10; // เพดานแสดงสูงสุดต่อครั้ง (ADR 0007)
        private static readonly JsonSerializerOptions J = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public LlmConversationEngine(
            IChatCompletionClient client, SystemPromptProvider prompt,
            IAppSettingsStore settings, ILogger<LlmConversationEngine> logger)
        {
            _client = client; _prompt = prompt; _settings = settings.GetEffectiveLlm(); _logger = logger;
        }

        public async Task<ChatbotMessageResponse> ProcessMessageAsync(
            string userCode, string userName, string userEmail, string role,
            string message, ChatbotConversationContext context)
        {
            context ??= new ChatbotConversationContext();
            context.Turns ??= new List<ChatbotTurn>();
            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "user", Text = message });
            if (string.IsNullOrEmpty(context.CurrentStep)) context.CurrentStep = "llm";

            // system = persona/knowledge + สถานการณ์ปัจจุบัน (สรุป candidate set)
            var system = _prompt.Get() + "\n\n# สถานการณ์ปัจจุบัน\n" + CandidateSummary(context);
            var messages = new List<LlmMessage> { new() { Role = "system", Content = system } };
            foreach (var t in context.Turns)
                messages.Add(new LlmMessage { Role = t.Role == "user" ? "user" : "assistant", Content = t.Text });

            var tools = BuildTools();
            string finalText = null;

            for (int i = 0; i < _settings.MaxToolIterations; i++)
            {
                var assistant = await _client.CompleteAsync(messages, tools);
                if (assistant.ToolCalls is { Count: > 0 })
                {
                    messages.Add(assistant);
                    foreach (var call in assistant.ToolCalls)
                    {
                        string result = ExecuteTool(call.Function?.Name, call.Function?.Arguments, context);
                        messages.Add(new LlmMessage { Role = "tool", ToolCallId = call.Id, Content = result });
                    }
                    continue;
                }
                finalText = assistant.Content;
                break;
            }

            // ถ้าวน tool จนครบรอบแล้วยังไม่ได้ข้อความ → บังคับสรุปเป็นข้อความ (ไม่มี tool)
            if (string.IsNullOrWhiteSpace(finalText))
            {
                try { finalText = (await _client.CompleteAsync(messages, null))?.Content; }
                catch (Exception ex) { _logger.LogWarning(ex, "forced final completion ล้มเหลว"); }
            }
            if (string.IsNullOrWhiteSpace(finalText))
                finalText = "ขออภัยค่ะ ระบบประมวลผลนานเกินไป รบกวนพิมพ์อีกครั้งนะคะ";

            // ดึงปุ่มถามด่วน [SUGGEST] ออก → strip เฉพาะตอน "แสดง"; history คง sentinel ไว้เป็น few-shot. ดู ADR 0008/0006
            var (cleanText, suggestions) = ExtractSuggestions(finalText);

            // เก็บเป็น 1 turn เดียว (พร้อม sentinel [BUBBLE]+[SUGGEST]) — presentation ล้วน ไม่รั่วเข้า state. ดู ADR 0006
            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "assistant", Text = finalText });
            var replies = SplitBubbles(cleanText);
            return new ChatbotMessageResponse
            {
                Replies = replies,
                Reply = string.Join("\n\n", replies),
                Suggestions = suggestions,
                UpdatedContext = context,
                IsReadyToPropose = context.CurrentStep == "ready_to_propose",
                ProposalSummary = null
            };
        }

        // แสดงได้หลายคำถามพอให้ลูกค้าเลือกเรื่องที่กังวลจริง ๆ โดย UI จะตัดบรรทัดให้อัตโนมัติ
        private const int MaxSuggestions = 6;

        // ===== streaming (ADR 0009) =====

        /// <summary>เหมือน ProcessMessageAsync แต่สตรีม: status ระหว่าง tool loop → delta token คำตอบ → suggestions → done.
        /// sentinel ([BUBBLE]/[SUGGEST]) ถูก parse กลางสตรีมที่ backend (single source of truth). ดู ADR 0009</summary>
        public async IAsyncEnumerable<ChatStreamEvent> ProcessMessageStreamAsync(
            string userCode, string userName, string userEmail, string role,
            string message, ChatbotConversationContext context,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            context ??= new ChatbotConversationContext();
            context.Turns ??= new List<ChatbotTurn>();
            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "user", Text = message });
            if (string.IsNullOrEmpty(context.CurrentStep)) context.CurrentStep = "llm";

            var system = _prompt.Get() + "\n\n# สถานการณ์ปัจจุบัน\n" + CandidateSummary(context);
            var messages = new List<LlmMessage> { new() { Role = "system", Content = system } };
            foreach (var t in context.Turns)
                messages.Add(new LlmMessage { Role = t.Role == "user" ? "user" : "assistant", Content = t.Text });

            var tools = BuildTools();
            string finalText = null;

            for (int i = 0; i < _settings.MaxToolIterations; i++)
            {
                var roundContent = new StringBuilder();
                int emitted = 0;
                var toolAcc = new SortedDictionary<int, (string id, string name, StringBuilder args)>();

                await foreach (var chunk in _client.CompleteStreamAsync(messages, tools, ct))
                {
                    if (!string.IsNullOrEmpty(chunk.ContentDelta))
                    {
                        roundContent.Append(chunk.ContentDelta);
                        var emittable = ComputeEmittable(roundContent.ToString(), final: false);
                        if (emittable.Length > emitted)
                        {
                            yield return ChatStreamEvent.Delta(emittable.Substring(emitted));
                            emitted = emittable.Length;
                        }
                    }
                    if (chunk.ToolCallDeltas != null)
                        foreach (var d in chunk.ToolCallDeltas)
                        {
                            if (!toolAcc.TryGetValue(d.Index, out var e))
                                toolAcc[d.Index] = e = (null, null, new StringBuilder());
                            if (!string.IsNullOrEmpty(d.Id)) e.id = d.Id;
                            if (!string.IsNullOrEmpty(d.Name)) e.name = d.Name;
                            if (!string.IsNullOrEmpty(d.ArgumentsDelta)) e.args.Append(d.ArgumentsDelta);
                            toolAcc[d.Index] = e;
                        }
                }

                if (toolAcc.Count > 0)
                {
                    var toolCalls = toolAcc.Values.Select(v => new LlmToolCall
                    {
                        Id = v.id,
                        Function = new LlmFunctionCall { Name = v.name, Arguments = v.args.ToString() }
                    }).ToList();
                    messages.Add(new LlmMessage { Role = "assistant", ToolCalls = toolCalls });
                    foreach (var tc in toolCalls)
                    {
                        yield return ChatStreamEvent.Status(StatusForTool(tc.Function?.Name));
                        string result = ExecuteTool(tc.Function?.Name, tc.Function?.Arguments, context);
                        messages.Add(new LlmMessage { Role = "tool", ToolCallId = tc.Id, Content = result });
                    }
                    continue;
                }

                // รอบนี้เป็นคำตอบ (ไม่มี tool) → flush ส่วนท้ายที่กั๊กไว้ (sentinel ที่ยังก่อตัวไม่เสร็จ)
                finalText = roundContent.ToString();
                var flush = ComputeEmittable(finalText, final: true);
                if (flush.Length > emitted) yield return ChatStreamEvent.Delta(flush.Substring(emitted));
                break;
            }

            // วน tool ครบรอบยังไม่ได้ข้อความ → บังคับสรุป (ไม่สตรีม) แล้วปล่อยทีเดียว
            if (string.IsNullOrWhiteSpace(finalText))
            {
                finalText = (await _client.CompleteAsync(messages, null, ct))?.Content;
                if (!string.IsNullOrWhiteSpace(finalText))
                    yield return ChatStreamEvent.Delta(ComputeEmittable(finalText, final: true));
            }
            if (string.IsNullOrWhiteSpace(finalText))
            {
                finalText = "ขออภัยค่ะ ระบบประมวลผลนานเกินไป รบกวนพิมพ์อีกครั้งนะคะ";
                yield return ChatStreamEvent.Delta(finalText);
            }

            // chips จาก [SUGGEST] + เก็บ turn เต็ม (พร้อม sentinel) เป็น 1 turn เหมือน non-stream (ADR 0006/0009)
            var (_, suggestions) = ExtractSuggestions(finalText);
            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "assistant", Text = finalText });
            if (suggestions.Count > 0) yield return ChatStreamEvent.Suggest(suggestions);
            yield return ChatStreamEvent.Done(context, context.CurrentStep == "ready_to_propose");
        }

        private static string StatusForTool(string name) => name switch
        {
            "filter_packages" => "กำลังค้นหาและกรองแผนประกัน",
            "prepare_proposal" => "กำลังจัดเตรียมใบเสนอราคา",
            _ => "กำลังประมวลผล"
        };

        /// <summary>แปลง raw content (กำลังไหล) → ข้อความที่ "ปล่อยได้": ตัดจาก [SUGGEST] ทิ้ง,
        /// คง [BUBBLE] ไว้ให้ client แตกกล่องเอง, และตอนยังไม่จบให้กั๊ก sentinel ที่กำลังก่อตัว
        /// ที่ปลายไว้ (กัน "[BUB" โผล่แวบระหว่างสตรีม).
        ///
        /// เดิม (ADR 0009) แทน [BUBBLE] ด้วยค่าว่าง → คำตอบไหลลงกล่องเดียว. ตอนนี้คงตัวคั่นไว้
        /// เพื่อให้คำถามปิดท้ายแยกเป็นกล่องของตัวเองตามเจตนาเดิมของ [[ADR 0006]] — ตัว sentinel เอง
        /// client เป็นคนตัดออกก่อนเรนเดอร์ ไม่โผล่ให้ผู้ใช้เห็น</summary>
        private static string ComputeEmittable(string full, bool final)
        {
            int s = full.IndexOf("[SUGGEST]", StringComparison.Ordinal);
            string body = s >= 0 ? full.Substring(0, s) : full;
            if (!final)
            {
                int hb = PartialSentinelStart(body);
                if (hb >= 0) body = body.Substring(0, hb);
            }
            return body;
        }

        /// <summary>คืน index เริ่มต้นของ sentinel ที่ "กำลังก่อตัว" ที่ปลายข้อความ (prefix ยังไม่ครบของ [BUBBLE]/[SUGGEST]) — ไม่งั้น -1.</summary>
        private static int PartialSentinelStart(string body)
        {
            int nl = body.LastIndexOf('\n');
            int lineStart = nl + 1;
            string lastLine = body.Substring(lineStart);
            string t = lastLine.TrimStart(' ', '\t');
            if (t.Length == 0 || t[0] != '[') return -1;
            bool incomplete =
                (t.Length < "[BUBBLE]".Length && "[BUBBLE]".StartsWith(t, StringComparison.Ordinal)) ||
                (t.Length < "[SUGGEST]".Length && "[SUGGEST]".StartsWith(t, StringComparison.Ordinal));
            return incomplete ? lineStart + (lastLine.Length - t.Length) : -1;
        }

        /// <summary>ขอเฉพาะปุ่มถามด่วนชุดแรก (ตอนเปิดแชต greeting เป็น template instant). ไม่เข้า tool loop — เบา/เร็ว. ดู ADR 0008</summary>
        public async Task<List<string>> SuggestAsync(ChatbotConversationContext context)
        {
            context ??= new ChatbotConversationContext();
            // หน้าแชตอาจยังไม่มีรถ/candidate: ให้มีคำถามความรู้ที่กดได้เสมอ
            // แทนการส่ง prompt ที่บังคับให้ LLM อ้างอิง candidate set ที่ยังไม่มี
            if ((context.Candidates?.Count ?? 0) == 0) return StarterSuggestions();

            var system = _prompt.Get() + "\n\n# สถานการณ์ปัจจุบัน\n" + CandidateSummary(context) +
                "\n\n# คำสั่งเฉพาะรอบนี้\nลูกค้าเพิ่งเปิดแชต (ทักทายเรียบร้อยแล้ว). ขอ**เฉพาะ**ปุ่มถามด่วน — อย่าทักทาย อย่าอธิบาย อย่าใส่ตาราง.\n" +
                "ตอบกลับเป็น JSON array ของสตริงไทยสั้น ๆ 4-6 อัน (มุมมองลูกค้า, กด=ถามต่อได้, ≤24 ตัวอักษร, อิง candidate set จริง) เท่านั้น เช่น [\"ขอแผนถูกสุด\",\"เพิ่มทุนประกัน\",\"ดูน้ำท่วม\",\"เทียบ 2+ กับ 3+\"]";
            var messages = new List<LlmMessage>
            {
                new() { Role = "system", Content = system },
                new() { Role = "user", Content = "ขอปุ่มถามด่วนสำหรับเริ่มคุย" }
            };

            var resp = await _client.CompleteAsync(messages, null);
            return ParseChipsJson(resp?.Content).Take(MaxSuggestions).ToList();
        }

        /// <summary>คำถามเริ่มต้นเมื่อยังไม่ได้ค้นรถ — ทุกข้อความรองรับใน fallback ด้วย.</summary>
        public static List<string> StarterSuggestions() => new()
        {
            "ชั้น 1 คุ้มครองอะไร",
            "2+ ต่างจาก 3+ อย่างไร",
            "รถน้ำท่วมเคลมได้ไหม",
            "พ.ร.บ. คุ้มครองอะไร",
            "ประกันบ้านคุ้มครองอะไร",
            "ประกัน PA คืออะไร"
        };

        // ===== suggestion chips parsing =====
        private static readonly System.Text.RegularExpressions.Regex SuggestLine =
            new(@"^[ \t]*\[SUGGEST\][ \t]*(.*)$", System.Text.RegularExpressions.RegexOptions.Multiline);

        /// <summary>ดึงบรรทัด [SUGGEST] a | b | c ออกจากข้อความ → คืน (ข้อความที่ strip แล้ว, รายการ chips).</summary>
        private static (string text, List<string> chips) ExtractSuggestions(string text)
        {
            var chips = new List<string>();
            if (string.IsNullOrEmpty(text)) return (text ?? "", chips);
            var m = SuggestLine.Match(text);
            if (m.Success)
            {
                chips = m.Groups[1].Value.Split('|')
                    .Select(s => s.Trim()).Where(s => s.Length > 0)
                    .Take(MaxSuggestions).ToList();
                text = SuggestLine.Replace(text, "").Trim();
            }
            return (text, chips);
        }

        /// <summary>parse คำตอบ /Suggest เป็น chips — รองรับทั้ง JSON array และ fallback (บรรทัด/`|`).</summary>
        private static List<string> ParseChipsJson(string content)
        {
            content = content?.Trim() ?? "";
            if (content.Length == 0) return new List<string>();
            // ตัด code fence ```json ... ``` ถ้ามี
            int lb = content.IndexOf('['), rb = content.LastIndexOf(']');
            if (lb >= 0 && rb > lb)
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<List<string>>(content.Substring(lb, rb - lb + 1));
                    if (arr != null)
                        return arr.Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
                catch { /* ตกไป fallback */ }
            }
            // fallback: แยกบรรทัด/pipe แล้วล้าง bullet/เลขนำหน้า
            return content.Split(new[] { '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().TrimStart('-', '*', '•', '"').TrimEnd('"').Trim())
                .Where(s => s.Length is > 0 and <= 40).ToList();
        }

        private const int MaxBubbles = 4; // safety cap — ส่วนเกินยุบรวมเข้ากล่องสุดท้าย (ADR 0006)

        /// <summary>แตกข้อความตาม sentinel [BUBBLE] → trim + กรองชิ้นว่าง + cap ที่ MaxBubbles (ยุบส่วนเกิน).</summary>
        private static List<string> SplitBubbles(string text)
        {
            var parts = System.Text.RegularExpressions.Regex
                .Split(text ?? "", @"^[ \t]*\[BUBBLE\][ \t]*$", System.Text.RegularExpressions.RegexOptions.Multiline)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (parts.Count == 0) return new List<string> { text?.Trim() ?? "" };
            if (parts.Count <= MaxBubbles) return parts;

            // ยุบส่วนเกินเข้ากล่องสุดท้าย ไม่ทิ้งเนื้อหา
            var capped = parts.Take(MaxBubbles - 1).ToList();
            capped.Add(string.Join("\n\n", parts.Skip(MaxBubbles - 1)));
            return capped;
        }

        // ===== candidate summary (ฉีดให้ LLM รู้ว่ามีอะไรให้เลือก โดยไม่ดัมพ์ทุกแถว) =====
        private static string CandidateSummary(ChatbotConversationContext c)
        {
            var cands = c.Candidates ?? new List<PackageRow>();
            if (cands.Count == 0)
                return "ยังไม่มี candidate (ลูกค้ายังไม่ได้เลือกรถจากหน้าค้นหา) — แนะนำให้กลับไปกรอกข้อมูลรถที่หน้า 'คำนวณเบี้ย' ก่อนค่ะ";

            var insurers = cands.Select(p => p.InsurerName).Distinct().Count();
            // ชั้นประกันที่ "มีจริง" — normalize เป็น 1/2/2+/3/3+ + นับจำนวน (ตัวยึดความจริง กัน LLM มโนชั้นที่ไม่มี)
            var classes = ClassCountLabels(cands);
            var sumMin = cands.Min(p => p.SumInsureMax); var sumMax = cands.Max(p => p.SumInsureMax);
            var premMin = cands.Min(p => p.PremStm); var premMax = cands.Max(p => p.PremStm);
            bool hasFire = cands.Any(p => p.FireSuminsure > 0);
            bool hasFlood = cands.Any(p => p.FloodSuminsure > 0);
            var car = $"{c.CarBrand} {c.CarModel} ปี {c.CarYear}".Trim();

            return
                $"รถ: {car}\n" +
                $"พบ **{cands.Count} แผน** จาก **{insurers} บริษัท**\n" +
                $"ชั้นประกันที่มีจริงในชุดนี้ (เสนอ/อ้างได้เฉพาะรายการนี้ — ห้ามเอ่ยถึงชั้นอื่น): {string.Join(", ", classes)}\n" +
                $"ทุนประกัน: {sumMin:N0}–{sumMax:N0} บาท | เบี้ยรวม: {premMin:N0}–{premMax:N0} บาท/ปี\n" +
                $"คุ้มครองไฟไหม้/สูญหาย: {(hasFire ? "มีให้เลือก" : "—")} | น้ำท่วม: {(hasFlood ? "มีให้เลือก" : "—")}\n" +
                "→ เปิดบทสนทนาด้วยการสรุปสั้นๆ ว่าพบกี่แผน แล้วเริ่มถามทุนประกัน/งบเบี้ย/ไฟไหม้/น้ำท่วม ทีละข้อ";
        }

        // ===== tools =====
        private List<ToolDef> BuildTools() => new()
        {
            new ToolDef { Function = new ToolFunction
            {
                Name = "filter_packages",
                Description = "กรอง+เรียง candidate set ตามความต้องการลูกค้า → คืน shortlist แผนจริง. เรียกได้แม้ไม่มีเกณฑ์ (โชว์แผนเด่น). รองรับ limit (จำนวนที่แสดง) และ sort_by (เกณฑ์เรียง). ผลลัพธ์มี coverageInSet บอกว่าทั้งชุดมีไฟไหม้/น้ำท่วมกี่แผน.",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["sum_min"] = Num("ทุนประกันขั้นต่ำที่ต้องการ (บาท)"),
                        ["sum_max"] = Num("ทุนประกันสูงสุดที่ต้องการ (บาท)"),
                        ["prem_max"] = Num("งบเบี้ยสูงสุดที่จ่ายไหว (บาท/ปี)"),
                        ["fire_min"] = Num("ทุนไฟไหม้/สูญหายขั้นต่ำที่ต้องการ (บาท); 0 = ไม่เน้น"),
                        ["flood_min"] = Num("ทุนน้ำท่วมขั้นต่ำที่ต้องการ (บาท); >0 = ต้องมีคุ้มครองน้ำท่วม"),
                        ["insurance_class"] = Str("ชั้นประกันที่ต้องการ: 1, 2+, 3+ (เว้นได้)"),
                        ["limit"] = Num($"จำนวนแผนที่จะแสดง (ดีฟอลต์ {DefaultLimit}). 'ขอทั้งหมด/ทุกแผน' → ส่งเลขสูง เช่น 99 (ระบบจะตัดเหลือสูงสุด {HardCap})"),
                        ["sort_by"] = Str("เกณฑ์เรียง: premium_asc (เบี้ยถูกสุด=ดีฟอลต์) | sum_desc (ทุนมากสุด) | coverage_desc (คุ้มครองครบสุด)")
                    },
                    ["required"] = Array.Empty<string>()
                }
            }},
            new ToolDef { Function = new ToolFunction
            {
                Name = "prepare_proposal",
                Description = "ร่างใบเสนอจากแผนใน shortlist ที่ลูกค้าเลือก (ต้องเรียก filter_packages ก่อน). หลังเรียก บอกลูกค้ากดปุ่ม 'ส่งคำสั่งซื้อ'.",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["choice"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "หมายเลขแผนใน shortlist (1 = อันดับแรก)" },
                        ["customer_name"] = Str("ชื่อลูกค้า (ถ้ามี)"),
                        ["customer_phone"] = Str("เบอร์โทร (ถ้ามี)"),
                        ["customer_email"] = Str("อีเมล (ถ้ามี)"),
                        ["license_plate"] = Str("ทะเบียนรถ (ถ้ามี)"),
                        ["id_card"] = Str("เลขบัตรประชาชน (ถ้ามี)")
                    },
                    ["required"] = new[] { "choice" }
                }
            }}
        };

        private static Dictionary<string, object> Str(string d) => new() { ["type"] = "string", ["description"] = d };
        private static Dictionary<string, object> Num(string d) => new() { ["type"] = "number", ["description"] = d };

        private string ExecuteTool(string name, string argsJson, ChatbotConversationContext context)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
                var args = doc.RootElement;
                return name switch
                {
                    "filter_packages" => FilterPackages(args, context),
                    "prepare_proposal" => PrepareProposal(args, context),
                    _ => Err($"unknown tool: {name}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "tool {Tool} ผิดพลาด", name);
                return Err(ex.Message);
            }
        }

        private string FilterPackages(JsonElement args, ChatbotConversationContext context)
        {
            var cands = context.Candidates ?? new List<PackageRow>();
            if (cands.Count == 0)
                return Err("ยังไม่มี candidate — ลูกค้าต้องเลือกรถจากหน้าค้นหาก่อน");

            decimal? sumMin = GetDec(args, "sum_min"), sumMax = GetDec(args, "sum_max");
            decimal? premMax = GetDec(args, "prem_max"), fireMin = GetDec(args, "fire_min"), floodMin = GetDec(args, "flood_min");
            string klass = NormalizeClass(GetStr(args, "insurance_class"));

            IEnumerable<PackageRow> q = cands;
            if (sumMin.HasValue) q = q.Where(p => p.SumInsureMax >= sumMin.Value);
            if (sumMax.HasValue) q = q.Where(p => p.SumInsureMax <= sumMax.Value);
            if (premMax.HasValue) q = q.Where(p => p.PremStm <= premMax.Value);
            if (fireMin is > 0) q = q.Where(p => p.FireSuminsure >= fireMin.Value);
            if (floodMin is > 0) q = q.Where(p => p.FloodSuminsure >= floodMin.Value);
            if (!string.IsNullOrEmpty(klass)) q = q.Where(p => ClassOf(p.PolTypeName) == klass);

            // เรียงตาม sort_by — ทุกแบบ tie-break ด้วยเบี้ยถูกสุด (ADR 0007)
            var matched = (GetStr(args, "sort_by") switch
            {
                "sum_desc" => q.OrderByDescending(p => p.SumInsureMax).ThenBy(p => p.PremStm),
                "coverage_desc" => q.OrderByDescending(p => (p.FireSuminsure > 0 ? 1 : 0) + (p.FloodSuminsure > 0 ? 1 : 0)).ThenBy(p => p.PremStm),
                _ => q.OrderBy(p => p.PremStm)
            }).ToList();

            // limit: ดีฟอลต์ 3, "ขอทั้งหมด" (เลขสูง) → clamp = min(limit, matched, HardCap)
            int limit = Math.Clamp(GetInt(args, "limit") ?? DefaultLimit, 1, HardCap);
            var shortlist = matched.Take(limit).ToList();

            // re-shopping: shortlist กำลังถูกสร้างใหม่ → pin เดิม (prepare_proposal) ใช้ไม่ได้แล้ว.
            // ถอดออกจาก ready_to_propose เพื่อไม่ให้การ์ด "แพคเกจที่เลือก" ค้างข้อมูลแผนเก่า —
            // ต้องเรียก prepare_proposal ใหม่เพื่อ pin แผนที่ลูกค้าเลือกตอนนี้ (กันการ์ดเพี้ยนตอนเปลี่ยนแผน)
            if (context.CurrentStep == "ready_to_propose") context.CurrentStep = "scoping";

            // เก็บ shortlist ลง Proposals (ให้ prepare_proposal/การ์ด/Propose ใช้)
            context.Proposals.Clear();
            var outList = new List<object>();
            for (int i = 0; i < shortlist.Count; i++)
            {
                var p = shortlist[i];
                context.Proposals.Add(new ChatbotProposalSummary
                {
                    PackageId = p.PackageId,
                    Brand = context.CarBrand, Model = context.CarModel, Year = context.CarYear ?? 0,
                    InsuranceClass = p.PolTypeName, SumInsure = p.SumInsureMax,
                    InsurerId = p.InsurerName, InsurerName = p.InsurerName,
                    Premium = p.PremNet, Total = p.PremStm
                });
                outList.Add(new
                {
                    choice = i + 1,
                    insurer = p.InsurerName, @class = p.PolTypeName, renov = p.RenovType,
                    sumInsure = p.SumInsureMax, premiumNet = p.PremNet, premiumTotal = p.PremStm,
                    fire = p.FireSuminsure, deductible = p.Deductible, accessories = "ไม่มีข้อมูล", flood = p.FloodSuminsure
                });
            }

            // coverageInSet: นับจาก candidate "ทั้งชุด" (กัน LLM อ้างเท็จว่าไม่มีน้ำท่วม/ไฟไหม้) — ADR 0007
            var coverageInSet = new
            {
                fire = cands.Count(p => p.FireSuminsure > 0),
                flood = cands.Count(p => p.FloodSuminsure > 0)
            };
            // classesInSet: ชั้นประกันที่ "มีจริง" ทั้งชุด + จำนวน (กัน LLM อ้าง/เสนอชั้นที่ไม่มี เช่น 2+/3+ ที่ไม่ได้อยู่ในชุด)
            var classesInSet = ClassCountLabels(cands);

            string note;
            if (matched.Count == 0)
            {
                note = !string.IsNullOrEmpty(klass)
                    ? $"ไม่มีแผน \"ชั้น {klass}\" ในชุดนี้ — ชั้นที่มีจริงคือ: {string.Join(", ", classesInSet)}. เสนอเฉพาะชั้นเหล่านี้เท่านั้น"
                    : "ไม่มีแผนตรงเกณฑ์ — ลองผ่อนเกณฑ์ (เช่น เพิ่มงบ/ลดทุน)";
            }
            else if (shortlist.Count < matched.Count) note = $"แสดง {shortlist.Count} จาก {matched.Count} แผน — บอกเกณฑ์ (ทุน/งบ/น้ำท่วม) เพื่อแคบลงค่ะ";
            else note = "เบี้ยเป็นเรตแพคเกจ ต้องยืนยันกับตัวแทนก่อนออกจริง";

            return JsonSerializer.Serialize(new
            {
                matched = matched.Count,
                showing = shortlist.Count,
                coverageInSet,
                classesInSet,
                note,
                shortlist = outList
            }, J);
        }

        private string PrepareProposal(JsonElement args, ChatbotConversationContext context)
        {
            if (context.Proposals == null || context.Proposals.Count == 0)
                return Err("ยังไม่มี shortlist — เรียก filter_packages ก่อน");

            int idx = (GetInt(args, "choice") ?? 1) - 1;
            if (idx < 0 || idx >= context.Proposals.Count)
                return Err($"choice ต้องอยู่ระหว่าง 1 ถึง {context.Proposals.Count}");

            var chosen = context.Proposals[idx];
            context.Proposals.RemoveAt(idx);
            context.Proposals.Insert(0, chosen);

            var nameA = GetStr(args, "customer_name"); var phone = GetStr(args, "customer_phone");
            var email = GetStr(args, "customer_email"); var plate = GetStr(args, "license_plate"); var idc = GetStr(args, "id_card");
            if (!string.IsNullOrWhiteSpace(nameA)) context.ProspectName = nameA;
            if (!string.IsNullOrWhiteSpace(phone)) context.ProspectPhone = phone;
            if (!string.IsNullOrWhiteSpace(email)) context.ProspectEmail = email;
            if (!string.IsNullOrWhiteSpace(plate)) context.ProspectLicensePlate = plate;
            if (!string.IsNullOrWhiteSpace(idc)) context.ProspectIdCard = idc;

            context.CurrentStep = "ready_to_propose";
            return JsonSerializer.Serialize(new
            {
                ok = true,
                selected = new { insurer = chosen.InsurerName, @class = chosen.InsuranceClass, sumInsure = chosen.SumInsure, premiumTotal = chosen.Total },
                next = "บอกลูกค้าให้กดปุ่ม 'ส่งคำสั่งซื้อ' เพื่อยืนยัน"
            }, J);
        }

        // ===== helpers =====
        private static string NormalizeClass(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return null;
            c = c.Trim();
            if (c.Contains("2+")) return "2+";
            if (c.Contains("3+")) return "3+";
            if (c.Contains("1")) return "1";
            if (c.Contains("2")) return "2";
            if (c.Contains("3")) return "3";
            return null;
        }
        private static string ClassOf(string polTypeName)
        {
            var t = polTypeName ?? "";
            if (t.Contains("2+")) return "2+";
            if (t.Contains("3+")) return "3+";
            if (t.Contains("1")) return "1";
            if (t.Contains("2")) return "2";
            if (t.Contains("3")) return "3";
            return "";
        }

        // ลำดับชั้นมาตรฐานสำหรับแสดงผล (1 → 2 → 2+ → 3 → 3+)
        private static readonly string[] ClassOrder = { "1", "2", "2+", "3", "3+" };
        private static int ClassRank(string c) { int i = Array.IndexOf(ClassOrder, c); return i < 0 ? 99 : i; }

        /// <summary>ชั้นประกันที่ "มีจริง" ใน candidate set → normalize + นับจำนวน + เรียงมาตรฐาน เช่น ["ประเภท 1 (12 แผน)", "ประเภท 3 (5 แผน)"].</summary>
        private static List<string> ClassCountLabels(IEnumerable<PackageRow> cands) =>
            cands.Select(p => ClassOf(p.PolTypeName))
                 .Where(s => !string.IsNullOrEmpty(s))
                 .GroupBy(s => s)
                 .OrderBy(g => ClassRank(g.Key))
                 .Select(g => $"ประเภท {g.Key} ({g.Count()} แผน)")
                 .ToList();

        private static string GetStr(JsonElement e, string k) =>
            e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        private static int? GetInt(JsonElement e, string k)
        {
            if (!e.TryGetProperty(k, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return (int)d; // เผื่อ LLM ส่ง 99.0
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
            return null;
        }
        private static decimal? GetDec(JsonElement e, string k)
        {
            if (!e.TryGetProperty(k, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n)) return n;
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var s)) return s;
            return null;
        }
        private static string Err(string msg) => JsonSerializer.Serialize(new { error = msg });
    }
}
