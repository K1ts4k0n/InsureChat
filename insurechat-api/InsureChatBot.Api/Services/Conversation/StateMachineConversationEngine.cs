using System.Text.RegularExpressions;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Interfaces;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>
    /// Engine แบบ state machine (deterministic) — ใช้เป็น fallback เมื่อ LLM ไม่พร้อม (ดู ADR 0003).
    /// motor: product→brand→model→year→class→sum→SPIN→[ask_customer]→ask_customer_detail→propose.
    /// property/pa: เก็บความต้องการ + handoff ส่งต่อตัวแทน.
    /// </summary>
    public class StateMachineConversationEngine : IConversationEngine
    {
        private readonly IMotorQueryService _motorQueryService;

        public StateMachineConversationEngine(IMotorQueryService motorQueryService)
        {
            _motorQueryService = motorQueryService;
        }

        public async Task<ChatbotMessageResponse> ProcessMessageAsync(
            string userCode, string userName, string userEmail, string role,
            string message, ChatbotConversationContext context)
        {
            context ??= new ChatbotConversationContext();
            bool isAgent = string.Equals(role, UserRoles.Agent, StringComparison.OrdinalIgnoreCase);

            context.Turns ??= new List<ChatbotTurn>();
            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "user", Text = message });

            // เช็คก่อนว่าคำถามเป็นคำถามทั่วไป / ความรู้ประกันภัย (FAQ Knowledge Base) หรือไม่
            var knowledgeAnswer = TryMatchKnowledge(message);
            if (!string.IsNullOrEmpty(knowledgeAnswer))
            {
                context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "assistant", Text = knowledgeAnswer });
                return new ChatbotMessageResponse
                {
                    Reply = knowledgeAnswer,
                    UpdatedContext = context,
                    IsReadyToPropose = false,
                    ProposalSummary = null
                };
            }

            if (string.IsNullOrEmpty(context.CurrentStep) || context.CurrentStep is "llm" or "Initial")
            {
                // ถ้ามาจากหน้า quote (มี candidate set) หรือเป็น step llm ที่ไม่มี LLM Key
                // ให้ปรับเข้าสู่การสโคปหรือเลือกแผนทันที
                if (context.Candidates != null && context.Candidates.Count > 0)
                {
                    context.CurrentStep = "scope_from_candidates";
                }
                else
                {
                    context.CurrentStep = "ask_product";
                }
            }

            string reply = context.CurrentStep switch
            {
                "scope_from_candidates" => HandleScopeFromCandidates(message, context),
                "ask_product"         => HandleProductType(message, context),
                "ask_brand"           => HandleBrand(message, context),
                "ask_model"           => HandleModel(message, context),
                "ask_year"            => HandleYear(message, context),
                "ask_class"           => HandleClass(message, context),
                "ask_sum"             => HandleSumInsure(message, context),
                "spin_deep"           => await HandleSpinDeepAsync(message, context, isAgent, userName, userEmail),
                "ask_customer"        => HandleAskCustomer(message, context),
                "ask_customer_detail" => HandleAskCustomerDetail(message, context),
                "ready_to_propose"    => HandleReadyToPropose(message, context),
                "prop_type"           => HandlePropType(message, context),
                "prop_sum"            => HandlePropSum(message, context),
                "prop_spin"           => HandlePropSpin(message, context),
                "pa_occupation"       => HandlePaOccupation(message, context),
                "pa_sum"              => HandlePaSum(message, context),
                "pa_spin"             => HandlePaSpin(message, context),
                "handoff_contact"     => HandleHandoffContact(message, context),
                "done"                => HandleDone(message, context),
                _                     => "ขออภัย ฉันไม่เข้าใจขั้นตอนปัจจุบัน พิมพ์ \"เริ่มใหม่\" เพื่อเริ่มใหม่ค่ะ"
            };

            context.Turns.Add(new ChatbotTurn { Timestamp = DateTime.Now, Role = "assistant", Text = reply });

            return new ChatbotMessageResponse
            {
                Reply = reply,
                UpdatedContext = context,
                IsReadyToPropose = context.CurrentStep == "ready_to_propose",
                ProposalSummary = null
            };
        }

        private string HandleScopeFromCandidates(string userMessage, ChatbotConversationContext context)
        {
            var candidates = context.Candidates ?? new List<PackageRow>();
            if (candidates.Count == 0)
            {
                context.CurrentStep = "ask_brand";
                return "ขออภัยค่ะ ไม่พบข้อมูลแพ็กเกจเดิม รบกวนแจ้งยี่ห้อและรุ่นรถใหม่อีกครั้งนะคะ";
            }

            var msg = userMessage.Trim();
            var lower = msg.ToLower();

            // 1. ตรวจสอบการกรองตามชั้นประกันภัย (Class)
            var pool = new List<PackageRow>(candidates);
            if (lower.Contains("ชั้น 1") || lower.Contains("ชั้น1") || lower.Contains("ประเภท 1") || lower.Contains("ประเภทหนึ่ง") || lower.Contains("ชั้นหนึ่ง") || lower.Contains("ป.1") || lower.Contains("ป1"))
            {
                var f = pool.Where(c => c.PolTypeName.Contains("1") && !c.PolTypeName.Contains("2+") && !c.PolTypeName.Contains("3+")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("2+") || lower.Contains("2 พลัส") || lower.Contains("สองพลัส") || lower.Contains("ประเภท 2+") || lower.Contains("ประเภทสองพลัส"))
            {
                var f = pool.Where(c => c.PolTypeName.Contains("2+")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("3+") || lower.Contains("3 พลัส") || lower.Contains("สามพลัส") || lower.Contains("ประเภท 3+") || lower.Contains("ประเภทสามพลัส"))
            {
                var f = pool.Where(c => c.PolTypeName.Contains("3+")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("ชั้น 3") || lower.Contains("ประเภท 3") || lower.Contains("ชั้นสาม"))
            {
                var f = pool.Where(c => c.PolTypeName.Contains("3") && !c.PolTypeName.Contains("+")).ToList();
                if (f.Count > 0) pool = f;
            }

            // 2. ตรวจสอบการกรองประเภทการซ่อม (อู่ / ห้าง)
            if (lower.Contains("ซ่อมห้าง") || lower.Contains("ห้าง") || lower.Contains("ศูนย์"))
            {
                var f = pool.Where(c => c.RenovType.Contains("ห้าง")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("ซ่อมอู่") || lower.Contains("อู่"))
            {
                var f = pool.Where(c => c.RenovType.Contains("อู่")).ToList();
                if (f.Count > 0) pool = f;
            }

            // 3. ตรวจสอบการกรองตามบริษัทประกันภัย
            if (lower.Contains("วิริยะ"))
            {
                var f = pool.Where(c => c.InsurerName.Contains("วิริยะ")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("ธนชาต"))
            {
                var f = pool.Where(c => c.InsurerName.Contains("ธนชาต")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("ทิพย"))
            {
                var f = pool.Where(c => c.InsurerName.Contains("ทิพย")).ToList();
                if (f.Count > 0) pool = f;
            }
            else if (lower.Contains("คุ้มภัย") || lower.Contains("โตเกียว"))
            {
                var f = pool.Where(c => c.InsurerName.Contains("คุ้มภัย") || c.InsurerName.Contains("โตเกียว")).ToList();
                if (f.Count > 0) pool = f;
            }

            // 4. ถ้าลูกค้าขอ "ดูทั้งหมด" หรือ "เทียบ" หรือ "มีกี่แผน" หรือ "มีอะไรบ้าง"
            if (lower.Contains("ทั้งหมด") || lower.Contains("ทุกแผน") || lower.Contains("เทียบ") || lower.Contains("มีอะไรบ้าง") || lower.Contains("ดูเพิ่ม") || lower.Contains("กี่แผน"))
            {
                var tableReply = $"พบ {pool.Count} แผนที่ตรงกับรถ **{context.CarBrand} {context.CarModel} ปี {context.CarYear}** ค่ะ:\n\n" +
                                 "| # | บริษัท | ชั้น | ซ่อม | ทุนประกัน | เบี้ย/ปี | ไฟไหม้ | Deduct | อุปกรณ์ตกแต่ง | น้ำท่วม |\n" +
                                 "|---:|---|---|---|---:|---:|---:|---:|---|---:|\n";
                for (int i = 0; i < pool.Count; i++)
                {
                    var p = pool[i];
                    tableReply += $"| {i + 1} | {p.InsurerName} | {p.PolTypeName} | {p.RenovType} | {p.SumInsureMax:N0} | {p.PremStm:N0} | {p.FireSuminsure:N0} | {p.Deductible:N0} | ไม่มีข้อมูล | {p.FloodSuminsure:N0} |\n";
                }
                tableReply += "\nสนใจแผนไหน พิมพ์หมายเลขแผน (เช่น **\"เอาแผน 1\"** หรือ **\"ขอแผนถูกสุด\"**) ได้เลยนะคะ";
                return tableReply;
            }

            // 5. ตรวจสอบการเลือกลำดับแผน เช่น "แผน 1", "แผนที่ 2", "เอาอันที่ 3"
            var numMatch = Regex.Match(msg, @"(แผน|อัน|ที่|เบอร์|ข้อ)\s*([0-9]+)");
            if (numMatch.Success && int.TryParse(numMatch.Groups[2].Value, out var choiceIdx) && choiceIdx >= 1 && choiceIdx <= pool.Count)
            {
                var selected = pool[choiceIdx - 1];
                return SelectProposalAndAskContact(selected, context);
            }

            // 6. การเรียงลำดับ (ถูกสุด / ทุนสูง)
            var sorted = pool.OrderBy(c => c.PremNet).ToList();
            if (lower.Contains("แพง") || lower.Contains("ทุนสูง") || lower.Contains("ทุนเยอะ") || lower.Contains("คุ้มครองเยอะ") || lower.Contains("ครบสุด"))
            {
                sorted = pool.OrderByDescending(c => c.SumInsureMax).ThenBy(c => c.PremNet).ToList();
            }

            var chosen = sorted.First();
            return SelectProposalAndAskContact(chosen, context);
        }

        private static string SelectProposalAndAskContact(PackageRow chosen, ChatbotConversationContext context)
        {
            context.Proposals = new List<ChatbotProposalSummary>
            {
                new()
                {
                    PackageId = chosen.PackageId,
                    Brand = context.CarBrand ?? "HONDA",
                    Model = context.CarModel ?? "CIVIC",
                    Year = context.CarYear ?? 2022,
                    InsuranceClass = chosen.PolTypeName?.Contains("2+") == true ? "2+" : (chosen.PolTypeName?.Contains("3+") == true ? "3+" : "1"),
                    SumInsure = chosen.SumInsureMax,
                    InsurerName = chosen.InsurerName,
                    Premium = chosen.PremNet,
                    Total = chosen.PremTotal
                }
            };

            context.CurrentStep = "ask_customer";
            return $"ได้เลยค่ะ 😊 สำหรับรถ **{context.CarBrand} {context.CarModel} ปี {context.CarYear}** ขอแนะนำแผนนี้ค่ะ:\n\n" +
                   $"⭐ **{chosen.InsurerName} ({chosen.PackageName})**\n" +
                   $"• **ความคุ้มครอง:** {chosen.PolTypeName} (ซ่อม{chosen.RenovType})\n" +
                   $"• **ทุนประกัน:** {chosen.SumInsureMax:N0} บาท\n" +
                   $"• **เบี้ยสุทธิ:** **{chosen.PremNet:N0} บาท/ปี** (รวมอากร: {chosen.PremTotal:N0} บาท)\n" +
                   $"• **คุ้มครองไฟไหม้:** {chosen.FireSuminsure:N0} บาท | **น้ำท่วม:** {chosen.FloodSuminsure:N0} บาท\n\n" +
                   $"ก่อนยืนยันส่งคำสั่งซื้อ ขอทราบ **ชื่อ และเบอร์โทรศัพท์** ของคุณลูกค้าด้วยนะคะ (เช่น: สมชาย 0812345678)";
        }

        // ===== product router =====
        private string HandleProductType(string userMessage, ChatbotConversationContext context)
        {
            var m = userMessage.ToLower();
            if (m.Contains("รถ") || m.Contains("motor") || m.Contains("ยานพาหนะ"))
            {
                context.ProductType = "motor"; context.CurrentStep = "ask_brand";
                return "ดีค่ะ ลูกค้าต้องการประกันรถยนต์ ขอทราบว่ารถยี่ห้ออะไรคะ? (เช่น Honda, Toyota, Nissan)";
            }
            if (m.Contains("บ้าน") || m.Contains("อัคคี") || m.Contains("property") || m.Contains("ไฟไหม้"))
            {
                context.ProductType = "property"; context.CurrentStep = "prop_type";
                return "ดีค่ะ ประกันบ้าน/อัคคีภัย ลูกค้าต้องการแบบไหนคะ?\n" +
                       "1) อัคคีภัยมาตรฐาน (มักบังคับตอนผ่อนบ้านกับธนาคาร)\n" +
                       "2) ประกันบ้านแบบครบ (ไฟไหม้ + ภัยธรรมชาติ + โจรกรรม + ทรัพย์สินภายใน)";
            }
            if (m.Contains("pa") || m.Contains("อุบัติเหตุ") || m.Contains("accident"))
            {
                context.ProductType = "pa"; context.CurrentStep = "pa_occupation";
                return "ดีค่ะ ประกันอุบัติเหตุส่วนบุคคล (PA) ขอทราบอาชีพของผู้เอาประกันคะ? (เช่น พนักงานออฟฟิศ, ช่าง, ขับรถ) — มีผลต่อชั้นอาชีพและเบี้ย";
            }
            return "ลูกค้าต้องการประกันอะไรคะ? พิมพ์ได้เลย: \"รถยนต์\", \"บ้าน/อัคคีภัย\" หรือ \"อุบัติเหตุ (PA)\"";
        }

        // ===== motor =====
        private string HandleBrand(string userMessage, ChatbotConversationContext context)
        {
            context.CarBrand = userMessage.Trim();
            context.CurrentStep = "ask_model";
            string modelExamples = context.CarBrand.ToLower() switch
            {
                "honda" => "Civic, Accord, City, CR-V",
                "toyota" => "Altis, Yaris, Fortuner, Hilux",
                "nissan" => "March, Almera, X-Trail, Navara",
                "isuzu" => "D-Max, MU-X, Rodeo",
                "mitsubishi" => "Mirage, Attrage, Outlander, Pajero",
                "mazda" => "2, 3, CX-5, CX-9",
                "ford" => "Ranger, Focus, EcoSport",
                "hyundai" => "i10, Accent, Tucson, Santa Fe",
                "kia" => "Morning, Picanto, Sorento",
                _ => "รุ่นต่างๆ"
            };
            return $"ขอบคุณค่ะ ยี่ห้อ {context.CarBrand} รุ่นอะไรคะ? (เช่น {modelExamples})";
        }

        private string HandleModel(string userMessage, ChatbotConversationContext context)
        {
            context.CarModel = userMessage.Trim();
            context.CurrentStep = "ask_year";
            return $"ได้ค่ะ {context.CarBrand} {context.CarModel} ปีไหนคะ? (เช่น 2023, 2024, 2025, 2026)";
        }

        private string HandleYear(string userMessage, ChatbotConversationContext context)
        {
            if (int.TryParse(userMessage.Trim(), out int year))
            {
                context.CarYear = year; context.CurrentStep = "ask_class";
                return $"ขอบคุณค่ะ ปี {year} ลูกค้าต้องการชั้นประกันไหนคะ? ชั้น 1 (ครอบคลุมเต็ม) ชั้น 2+ หรือชั้น 3+?";
            }
            return "โปรดบอกปีเป็นตัวเลขค่ะ (เช่น 2020)";
        }

        private string HandleClass(string userMessage, ChatbotConversationContext context)
        {
            var m = userMessage.ToLower();
            if (m.Contains("1") && !m.Contains("2") && !m.Contains("3")) context.InsuranceClass = "1";
            else if (m.Contains("2+")) context.InsuranceClass = "2+";
            else if (m.Contains("3+")) context.InsuranceClass = "3+";
            else return "โปรดเลือกชั้นประกัน: ชั้น 1, 2+, หรือ 3+";
            context.CurrentStep = "ask_sum";
            return "ดีค่ะ ลูกค้าต้องการทุนประกันเท่าไหร่คะ? (เช่น 200000, 300000, 500000 บาท)";
        }

        private string HandleSumInsure(string userMessage, ChatbotConversationContext context)
        {
            if (decimal.TryParse(userMessage.Trim().Replace(",", ""), out decimal sum))
            {
                context.SumInsure = sum; context.CurrentStep = "spin_deep";
                return $"ขอทำความเข้าใจนะคะ ลูกค้าเลือกชั้น {context.InsuranceClass} ทุน {sum:N0} บาท เพราะเหตุใดคะ? มีกังวลเรื่องอะไร?";
            }
            return "โปรดบอกจำนวนเงินเป็นตัวเลขค่ะ (เช่น 200000)";
        }

        private async Task<string> HandleSpinDeepAsync(string userMessage, ChatbotConversationContext context, bool isAgent, string userName, string userEmail)
        {
            if (string.IsNullOrEmpty(context.SpinReason))
            {
                context.DiscoveredNeeds ??= new List<string>();
                context.DiscoveredNeeds.Add(userMessage);
                context.SpinReason = userMessage;
            }

            var packages = await _motorQueryService.SearchPackagesAsync(
                brand: context.CarBrand, model: context.CarModel,
                year: context.CarYear ?? DateTime.Now.Year,
                insuranceClass: context.InsuranceClass,
                sumInsure: context.SumInsure ?? 300000m, limit: 5);

            if (packages == null || packages.Count == 0)
                return "ขออภัยค่ะ ไม่พบแพคเกจที่ตรงกับเงื่อนไขของคุณ โปรดลองปรับเปลี่ยนชั้นประกันหรือทุนประกัน";

            context.Proposals.Clear();
            foreach (var pkg in packages)
                context.Proposals.Add(new ChatbotProposalSummary
                {
                    Brand = pkg.CarBrand, Model = pkg.CarModel, Year = pkg.Year,
                    InsuranceClass = pkg.InsuranceClass, SumInsure = pkg.SumInsure,
                    InsurerId = pkg.InsurerId, InsurerName = pkg.InsurerName,
                    Premium = pkg.Premium, Total = pkg.Total
                });

            var reply = "ขอบคุณที่อธิบายค่ะ ฉันเข้าใจความกังวลของคุณแล้ว ได้ค้นหาแพคเกจที่เหมาะสมที่สุด 3 อันดับ (เรียงตามเบี้ยประหยัดสุด):\n\n";
            for (int i = 0; i < packages.Count && i < 3; i++)
            {
                var pkg = packages[i];
                reply += $"{i + 1}. {pkg.InsurerName}\n";
                reply += $"   รถ: {pkg.CarBrand} {pkg.CarModel} ปี {pkg.Year} | ชั้น {pkg.InsuranceClass}\n";
                reply += $"   ทุนประกัน: {pkg.SumInsure:N0} บาท\n";
                reply += $"   เบี้ยสุทธิ: {pkg.Premium:N0} บาท/ปี (รวมอากร: {pkg.Total:N0} บาท)\n\n";
            }
            reply += "⚠️ เบี้ยข้างต้นเป็นเรตแพคเกจ ต้องยืนยันกับตัวแทนก่อนออกกรมธรรม์จริงค่ะ\n\n";

            if (isAgent)
            {
                context.CurrentStep = "ask_customer";
                reply += "ก่อนเสนอ ขอชื่อและอีเมลของลูกค้าด้วยค่ะ (เช่น: สมชาย ใจดี, somchai@email.com)";
            }
            else
            {
                // public: ไม่มี identity จาก login → ต้องถามชื่อเองก่อน (ชื่อ+เบอร์ บังคับ ก่อนเสนอแพคเกจ)
                context.CurrentStep = "ask_customer";
                reply += "ก่อนส่งคำสั่งซื้อ ขอชื่อผู้ติดต่อด้วยค่ะ (พร้อมอีเมลถ้ามี เช่น: สมชาย ใจดี, somchai@email.com)";
            }
            return reply;
        }

        private string HandleAskCustomer(string userMessage, ChatbotConversationContext context)
        {
            var emailMatch = Regex.Match(userMessage, @"[^\s,;]+@[^\s,;]+\.[^\s,;]+");
            if (emailMatch.Success) context.ProspectEmail = emailMatch.Value;
            var textWithoutEmail = emailMatch.Success ? userMessage.Replace(emailMatch.Value, " ") : userMessage;

            // ตรวจจับเบอร์โทรศัพท์ (ถ้าพิมพ์ชื่อพร้อมเบอร์โทรมาด้วยกันในรอบเดียว เช่น "สมชาย 0812345678")
            var phoneMatch = Regex.Match(textWithoutEmail, @"0\d[\d\s-]{7,9}");
            if (phoneMatch.Success)
            {
                context.ProspectPhone = Regex.Replace(phoneMatch.Value, @"\D", "");
                textWithoutEmail = textWithoutEmail.Replace(phoneMatch.Value, " ");
            }

            var name = textWithoutEmail.Trim().Trim(',', ';', ' ', '-');
            if (!string.IsNullOrWhiteSpace(name)) context.ProspectName = name;

            if (string.IsNullOrWhiteSpace(context.ProspectName))
                return "ขอชื่อลูกค้าด้วยค่ะ (พร้อมเบอร์โทรหรืออีเมลถ้ามี เช่น: สมชาย 0812345678)";

            // ถ้ามีเบอร์โทรครบแล้ว เข้าสู่ ready_to_propose ทันที
            if (!string.IsNullOrWhiteSpace(context.ProspectPhone) && context.ProspectPhone.Length >= 9)
            {
                context.CurrentStep = "ready_to_propose";
                return $"ขอบคุณค่ะคุณ **{context.ProspectName}** (เบอร์ติดต่อ: {context.ProspectPhone})\n\n" +
                       $"ระบบได้เตรียมข้อเสนอให้เรียบร้อยแล้วค่ะ\n" +
                       $"กดปุ่ม **\"ส่งคำสั่งซื้อ\"** ด้านล่างเพื่อยืนยันให้เจ้าหน้าที่ติดต่อกลับและส่งแจ้งเตือนเข้า LINE ได้เลยค่ะ 😊";
            }

            context.CurrentStep = "ask_customer_detail";
            return $"ขอบคุณค่ะ ลูกค้า {context.ProspectName}\n" + CustomerDetailPrompt();
        }

        private static string CustomerDetailPrompt() =>
            "ขอข้อมูลเพิ่มเพื่อจัดทำกรมธรรม์ค่ะ: เบอร์โทร (จำเป็น), เลขบัตรประชาชน 13 หลัก, ทะเบียนรถ\n" +
            "(เช่น: 0812345678, 1101700200111, กก1234) — บัตร/ทะเบียน หากยังไม่มีพิมพ์ \"ข้าม\" ได้ แต่เบอร์โทรต้องระบุค่ะ";

        private string HandleAskCustomerDetail(string userMessage, ChatbotConversationContext context)
        {
            if (!userMessage.Contains("ข้าม"))
            {
                var idMatch = Regex.Match(userMessage, @"\d[\d\s-]{11,}\d");
                string idDigits = idMatch.Success ? Regex.Replace(idMatch.Value, @"\D", "") : "";
                if (idDigits.Length == 13) context.ProspectIdCard = idDigits;
                var rest = idDigits.Length == 13 ? userMessage.Replace(idMatch.Value, " ") : userMessage;
                var phoneMatch = Regex.Match(rest, @"0\d[\d\s-]{7,9}");
                if (phoneMatch.Success) context.ProspectPhone = Regex.Replace(phoneMatch.Value, @"\D", "");
                var plateMatch = Regex.Match(userMessage, @"[ก-ฮa-zA-Z]{1,3}\s?\d{1,4}");
                if (plateMatch.Success) context.ProspectLicensePlate = plateMatch.Value.Trim();
            }
            // เบอร์โทร = บังคับ ก่อนเสนอแพคเกจ (บัตร/ทะเบียน ข้ามได้)
            var phoneDigits = new string((context.ProspectPhone ?? "").Where(char.IsDigit).ToArray());
            if (phoneDigits.Length < 9 || phoneDigits.Length > 10)
                return "ขอเบอร์โทร (9–10 หลัก) ก่อนส่งคำสั่งซื้อค่ะ (เช่น 0812345678)";
            context.ProspectPhone = phoneDigits;
            context.CurrentStep = "ready_to_propose";
            var have = new List<string>();
            if (!string.IsNullOrWhiteSpace(context.ProspectPhone)) have.Add($"โทร {context.ProspectPhone}");
            if (!string.IsNullOrWhiteSpace(context.ProspectIdCard)) have.Add("บัตร ปชช. ✓");
            if (!string.IsNullOrWhiteSpace(context.ProspectLicensePlate)) have.Add($"ทะเบียน {context.ProspectLicensePlate}");
            var summary = have.Count > 0 ? $" (บันทึก: {string.Join(", ", have)})" : "";
            return $"รับข้อมูลเรียบร้อยค่ะ{summary}\n" +
                   "กด \"ส่งคำสั่งซื้อ\" เพื่อยืนยัน หรือพิมพ์ \"เริ่มใหม่\" เพื่อค้นหารถคันอื่นค่ะ";
        }

        private string HandleReadyToPropose(string userMessage, ChatbotConversationContext context)
        {
            if (IsRestart(userMessage)) return Restart(context);
            return "แพคเกจพร้อมแล้วค่ะ กดปุ่ม \"ส่งคำสั่งซื้อ\" เพื่อยืนยัน หรือพิมพ์ \"เริ่มใหม่\" เพื่อค้นหารถคันใหม่ค่ะ";
        }

        // ===== property =====
        private string HandlePropType(string userMessage, ChatbotConversationContext context)
        {
            var m = userMessage.ToLower();
            string choice = (m.Contains("2") || m.Contains("ครบ") || m.Contains("บ้านแบบครบ")) ? "ประกันบ้านแบบครบ (IAR)" : "อัคคีภัยมาตรฐาน";
            context.DiscoveredNeeds.Add("แบบที่สนใจ: " + choice);
            context.CurrentStep = "prop_sum";
            return $"รับทราบค่ะ — {choice} ขอทราบมูลค่าสิ่งปลูกสร้าง/ทุนประกันโดยประมาณคะ? (เช่น 2,000,000 บาท)\n" +
                   "💡 การประเมินทุนให้พอสำคัญมาก ถ้าต่ำไปเคลมได้ไม่เต็ม (under-insurance)";
        }

        private string HandlePropSum(string userMessage, ChatbotConversationContext context)
        {
            context.DiscoveredNeeds.Add("ทุน/มูลค่า: " + userMessage.Trim());
            context.CurrentStep = "prop_spin";
            return "ขอบคุณค่ะ ลูกค้ากังวลภัยอะไรเป็นพิเศษคะ? (เช่น น้ำท่วม, โจรกรรม, ทรัพย์สินภายใน) — จะได้จับคู่ความคุ้มครองให้ตรง";
        }

        private string HandlePropSpin(string userMessage, ChatbotConversationContext context)
        {
            context.DiscoveredNeeds.Add("ความกังวลหลัก: " + userMessage.Trim());
            context.CurrentStep = "handoff_contact";
            return "เข้าใจค่ะ 🙏 หมายเหตุ: ภัยน้ำท่วมมักมีวงเงินย่อย (sub-limit) และทรัพย์สินมีค่า (เงินสด/เครื่องประดับ) มักมีข้อยกเว้น ต้องดูเงื่อนไขกรมธรรม์จริง\n\n" +
                   ContactPrompt("ประกันบ้าน/อัคคีภัย");
        }

        // ===== PA =====
        private string HandlePaOccupation(string userMessage, ChatbotConversationContext context)
        {
            context.DiscoveredNeeds.Add("อาชีพ: " + userMessage.Trim());
            context.CurrentStep = "pa_sum";
            return "ขอบคุณค่ะ ลูกค้าต้องการทุนประกัน (เงินก้อนกรณีเสียชีวิต/ทุพพลภาพจากอุบัติเหตุ) ประมาณเท่าไหร่คะ? (เช่น 100,000 / 500,000 / 1,000,000 บาท)";
        }

        private string HandlePaSum(string userMessage, ChatbotConversationContext context)
        {
            context.DiscoveredNeeds.Add("ทุนเอาประกัน: " + userMessage.Trim());
            context.CurrentStep = "pa_spin";
            return "รับทราบค่ะ ต้องการความคุ้มครองเสริมไหมคะ? (เช่น ค่ารักษาพยาบาลต่อครั้ง, ชดเชยรายได้, คุ้มครองขับขี่/โดยสารมอเตอร์ไซค์)";
        }

        private string HandlePaSpin(string userMessage, ChatbotConversationContext context)
        {
            context.DiscoveredNeeds.Add("ความคุ้มครองเสริมที่สนใจ: " + userMessage.Trim());
            context.CurrentStep = "handoff_contact";
            return "เยี่ยมค่ะ 🙏 หมายเหตุ: PA คุ้มครองเฉพาะ \"อุบัติเหตุ\" ไม่รวมเจ็บป่วย และชั้นอาชีพมีผลต่อเบี้ย/การรับประกัน\n\n" +
                   ContactPrompt("ประกันอุบัติเหตุ (PA)");
        }

        // ===== shared handoff =====
        private static string ContactPrompt(string productLabel) =>
            $"สำหรับ{productLabel} ยังไม่มีระบบออกราคาอัตโนมัติในแชตนี้ — ขอชื่อและเบอร์โทรเพื่อให้ตัวแทนติดต่อกลับพร้อมใบเสนอราคาค่ะ\n" +
            "(เช่น: สมชาย ใจดี, 0812345678)";

        private string HandleHandoffContact(string userMessage, ChatbotConversationContext context)
        {
            var phoneMatch = Regex.Match(userMessage, @"0\d[\d\s-]{7,9}");
            if (phoneMatch.Success) context.ProspectPhone = Regex.Replace(phoneMatch.Value, @"\D", "");
            var name = phoneMatch.Success ? userMessage.Replace(phoneMatch.Value, "") : userMessage;
            name = name.Trim().Trim(',', ';', ' ');
            if (!string.IsNullOrWhiteSpace(name)) context.ProspectName = name;
            if (string.IsNullOrWhiteSpace(context.ProspectName) && string.IsNullOrWhiteSpace(context.ProspectPhone))
                return "ขอชื่อและเบอร์โทรเพื่อให้ตัวแทนติดต่อกลับค่ะ (เช่น: สมชาย ใจดี, 0812345678)";
            context.CurrentStep = "done";
            var product = context.ProductType == "pa" ? "ประกันอุบัติเหตุ (PA)"
                        : context.ProductType == "property" ? "ประกันบ้าน/อัคคีภัย" : "ประกัน";
            var needs = (context.DiscoveredNeeds != null && context.DiscoveredNeeds.Count > 0)
                ? "\n- " + string.Join("\n- ", context.DiscoveredNeeds) : "";
            return $"ขอบคุณค่ะ คุณ{context.ProspectName} 🙏\n" +
                   $"สรุปความต้องการ {product}:{needs}\n\n" +
                   $"ตัวแทนจะติดต่อกลับที่เบอร์ {context.ProspectPhone} พร้อมใบเสนอราคาที่เหมาะสมค่ะ\n" +
                   "พิมพ์ \"เริ่มใหม่\" เพื่อสอบถามผลิตภัณฑ์อื่นได้ค่ะ";
        }

        private string HandleDone(string userMessage, ChatbotConversationContext context)
        {
            if (IsRestart(userMessage)) return Restart(context);
            return "ขอบคุณค่ะ ตัวแทนจะติดต่อกลับเร็ว ๆ นี้ — พิมพ์ \"เริ่มใหม่\" เพื่อสอบถามผลิตภัณฑ์อื่นได้ค่ะ";
        }

        // ===== helpers =====
        private static bool IsRestart(string msg) => msg.Contains("เริ่มใหม่") || msg.Contains("ใหม่");

        private string Restart(ChatbotConversationContext context)
        {
            context.CurrentStep = "ask_product";
            context.ProductType = "";
            context.CarBrand = context.CarModel = context.InsuranceClass = "";
            context.CarYear = null; context.SumInsure = null;
            context.SpinReason = "";
            context.ProspectName = context.ProspectEmail = "";
            context.ProspectPhone = context.ProspectIdCard = context.ProspectLicensePlate = "";
            context.DiscoveredNeeds?.Clear();
            context.Proposals?.Clear();
            return "ได้ค่ะ เริ่มใหม่นะคะ ลูกค้าต้องการประกันอะไรคะ? (\"รถยนต์\", \"บ้าน/อัคคีภัย\" หรือ \"อุบัติเหตุ (PA)\")";
        }

        private static string TryMatchKnowledge(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return null;
            var lower = msg.ToLower();

            // 1. พ.ร.บ. คุ้มครองอะไร / วงเงิน / ค่าเสียหายเบื้องต้น / ชนแล้วหนี
            if ((lower.Contains("พรบ") || lower.Contains("พ.ร.บ") || lower.Contains("พ ร บ")) &&
                (lower.Contains("คุ้มครอง") || lower.Contains("วงเงิน") || lower.Contains("เท่าไหร่") || lower.Contains("เบิก") || lower.Contains("ชนแล้วหนี") || lower.Contains("จ่าย") || lower.Contains("อะไร")))
            {
                return "ความคุ้มครองของ **พ.ร.บ. (ภาคบังคับ)** มีดังนี้ค่ะ:\n\n" +
                       "1. **ค่าเสียหายเบื้องต้น (จ่ายทันทีภายใน 7 วัน ไม่ต้องรอพิสูจน์ถูกผิด):**\n" +
                       "   - ค่ารักษาพยาบาลตามจริง สูงสุด 30,000 บาท/คน\n" +
                       "   - ค่าปลงศพ / ชดเชยกรณีสูญเสียอวัยวะหรือทุพพลภาพ 35,000 บาท/คน (รวมกันไม่เกิน 65,000 บาท)\n\n" +
                       "2. **ค่าสินไหมทดแทนส่วนเกิน (เมื่อพิสูจน์แล้วว่าเป็นฝ่ายถูก หรือผู้โดยสาร/คนเดินถนน):**\n" +
                       "   - ค่ารักษาพยาบาลตามจริง รวมสูงสุด 80,000 บาท/คน\n" +
                       "   - เสียชีวิต หรือทุพพลภาพถาวรสิ้นเชิง รวมสูงสุด 500,000 บาท/คน\n" +
                       "   - เงินชดเชยรายวันพักรักษาตัวในโรงพยาบาล (IPD) วันละ 200 บาท สูงสุด 20 วัน (4,000 บาท)\n" +
                       "   - **วงเงินคุ้มครองสูงสุดรวม: 504,000 บาท/คน**\n\n" +
                       "3. **กรณีชนแล้วหนี:**\n" +
                       "   - คนบาดเจ็บสามารถยื่นขอรับเงินช่วยเหลือเบื้องต้นจาก **'กองทุนทดแทนผู้ประสบภัยจากรถ' (สนง.คปภ.)** ได้ภายใน 180 วันค่ะ\n\n" +
                       "*(หมายเหตุ: พ.ร.บ. คุ้มครองเฉพาะคน ไม่คุ้มครองค่าซ่อมรถเลยแม้แต่บาทเดียวนะคะ)*";
            }

            // 2. 2+ vs 3+ ต่างกันอย่างไร
            if ((lower.Contains("2+") || lower.Contains("2 พลัส") || lower.Contains("สองพลัส")) &&
                (lower.Contains("3+") || lower.Contains("3 พลัส") || lower.Contains("สามพลัส")) ||
                (lower.Contains("ต่างกัน") && (lower.Contains("2+") || lower.Contains("3+"))))
            {
                return "จุดต่างสำคัญระหว่าง **ประกันชั้น 2+** และ **ชั้น 3+** มีดังนี้ค่ะ:\n\n" +
                       "- **จุดที่เหมือนกัน:** ทั้งคู่คุ้มครอง **'ค่าซ่อมรถเราเมื่อชนกับยานพาหนะทางบกและมีคู่กรณี'** เหมือนกันเลยค่ะ\n" +
                       "- **จุดที่ต่างกัน:**\n" +
                       "  - **ชั้น 2+:** คุ้มครอง **รถหาย + ไฟไหม้ + น้ำท่วม** (ตามแพคเกจ) ด้วยค่ะ\n" +
                       "  - **ชั้น 3+:** **ไม่คุ้มครอง** รถหาย ไฟไหม้ และน้ำท่วมเลย\n\n" +
                       "💡 **คำแนะนำ:** หากรถจอดคอนโด จอดข้างนอกบ้าน หรือเสี่ยงน้ำท่วม/ไฟไหม้ แนะนำ **2+** สบายใจกว่าค่ะ แต่หากจอดในบ้านปลอดภัย เน้นคุ้มครองรถชนรถ แนะนำ **3+** จะประหยัดเบี้ยได้ดีที่สุดค่ะ";
            }

            // 2.1 ชั้น 1 คุ้มครองอะไร (รองรับปุ่มเริ่มต้นและคำถามภาษาพูด)
            if (lower.Contains("ชั้น 1") || lower.Contains("ชั้น1") || lower.Contains("ชั้นหนึ่ง") || lower.Contains("ประเภท 1"))
            {
                return "**ประกันรถยนต์ชั้น 1** เป็นความคุ้มครองที่กว้างที่สุดสำหรับตัวรถค่ะ โดยทั่วไปครอบคลุมความเสียหายรถเราแม้ชนเองหรือไม่มีคู่กรณี รวมถึงความรับผิดต่อบุคคลภายนอก และภัยรถหาย/ไฟไหม้/น้ำท่วมตามเงื่อนไขของแผน\n\nก่อนเลือกควรเทียบทุนประกัน ประเภทซ่อม (ห้าง/อู่) ค่าเสียหายส่วนแรก และข้อยกเว้นในกรมธรรม์จริง เพราะแต่ละแผนอาจมีรายละเอียดต่างกันค่ะ";
            }

            // 3. แอลกอฮอล์ / เมาแล้วขับ
            if (lower.Contains("แอลกอฮอล์") || lower.Contains("เมา") || lower.Contains("เหล้า") || lower.Contains("เบียร์"))
            {
                return "เกณฑ์ คปภ. เรื่อง **ปริมาณแอลกอฮอล์ในเลือด** มีดังนี้ค่ะ:\n\n" +
                       "1. หากตรวจพบแอลกอฮอล์เกิน **50 มิลลิกรัมเปอร์เซ็นต์ (mg%)** (หรือเกิน 20 mg% สำหรับผู้มีใบขับขี่ชั่วคราว/อายุไม่ถึง 20 ปี):\n" +
                       "   - **ตัวรถเราเอง:** บริษัทประกัน **'ไม่คุ้มครองทุกกรณี'** ต้องออกค่าซ่อมเอง 100% ค่ะ\n" +
                       "   - **คู่กรณี (บุคคลภายนอก):** ประกันจะจ่ายค่ารักษาและค่าซ่อมรถให้คู่กรณีก่อนตามกฎหมาย แต่หลังจากนั้น **บริษัทประกันจะมาฟ้องไล่เบี้ยเรียกเงินคืนทั้งหมดจากผู้ขับขี่** ค่ะ\n" +
                       "2. หากไม่เกินเกณฑ์ ประกันจะยังให้ความคุ้มครองตามเงื่อนไขปกติค่ะ";
            }

            // 4. ชนเสาไฟ / ชนต้นไม้ / ชนหมา / หินดีด
            if (lower.Contains("เสาไฟ") || lower.Contains("ต้นไม้") || lower.Contains("ชนหมา") || lower.Contains("หินดีด") || lower.Contains("ไม่มีคู่กรณี") || lower.Contains("ชนเอง"))
            {
                return "การชนที่ไม่มีคู่กรณีเป็นยานพาหนะทางบก เช่น ชนเสาไฟ ชนต้นไม้ ชนสุนัข คว่ำเอง หรือหินดีดกระจกหน้า:\n\n" +
                       "- **ประกันชั้น 1:** **คุ้มครองค่าซ่อมรถเราทุกกรณีค่ะ** (กรณีหินดีดหรือครูดฟุตบาทที่ไม่สามารถระบุคู่กรณีได้ อาจมีค่าเสียหายส่วนแรก Excess 1,000 บาทตามเกณฑ์ คปภ.)\n" +
                       "- **ประกันชั้น 2+ และ 3+:** **ไม่คุ้มครองค่ะ** เพราะเงื่อนไขของ 2+ และ 3+ ระบุชัดเจนว่าจะซ่อมรถเราเฉพาะกรณีชนกับยานพาหนะทางบกและระบุคู่กรณีได้เท่านั้นค่ะ";
            }

            // 5. น้ำท่วม / รถจมน้ำ
            if ((lower.Contains("น้ำท่วม") || lower.Contains("จมน้ำ")) &&
                !(lower.Contains("บ้าน") || lower.Contains("อาคาร") || lower.Contains("คอนโด") || lower.Contains("ทรัพย์สิน")))
            {
                return "กรณี **ภัยน้ำท่วมรถยนต์**:\n\n" +
                       "- **ประกันที่คุ้มครอง:** **ชั้น 1** และ **ชั้น 2+ (ที่มีระบุแพคเกจคุ้มครองน้ำท่วม)** ค่ะ\n" +
                       "- **การชดใช้ค่าสินไหม:**\n" +
                       "  - หากน้ำท่วมบางส่วน (ท่วมถึงเบาะ/คอนโซล แต่ซ่อมได้): บริษัทประกันจะรับผิดชอบซ่อมแซมให้อยู่ในสภาพเดิมค่ะ\n" +
                       "  - หากน้ำท่วมมิดคัน หรือความเสียหายเกิน 70% ของมูลค่ารถ: จะถือเป็น **Total Loss (เสียหายสิ้นเชิง)** บริษัทประกันจะจ่ายเงินเต็มทุนประกันภัย 100% ให้แก่ผู้เอาประกันค่ะ";
            }

            // 6. ระบุชื่อคนขับ / ให้เพื่อนยืมรถ
            if (lower.Contains("ระบุชื่อ") || (lower.Contains("ยืม") && lower.Contains("รถ")) || lower.Contains("เพื่อนขับ"))
            {
                return "กรณีเลือกแบบ **ระบุชื่อผู้ขับขี่** (ระบุได้สูงสุด 2 คน รับส่วนลด 5-20%):\n\n" +
                       "- หากให้เพื่อนหรือบุคคลที่ไม่มีชื่อในกรมธรรม์ขับไปเกิดอุบัติเหตุ:\n" +
                       "  - **หากเพื่อนเป็น 'ฝ่ายถูก':** เคลมได้เต็มจำนวน 100% ตามปกติ ไม่เสียค่าใช้จ่ายใดๆ ค่ะ\n" +
                       "  - **หากเพื่อนเป็น 'ฝ่ายผิด':** ประกันยังซ่อมให้ทั้งรถเราและคู่กรณี แต่ผู้เอาประกันต้องร่วมจ่าย **ค่าเสียหายส่วนแรกจากการผิดสัญญา** (รถเรา 6,000 บาท + บุคคลภายนอก 2,000 บาท รวมสูงสุด 8,000 บาท) ค่ะ";
            }

            // 7. ประกันบ้าน / อัคคีภัย
            if (lower.Contains("อัคคีภัย") || lower.Contains("ประกันบ้าน") || lower.Contains("ไฟไหม้บ้าน"))
            {
                return "การ **ประกันอัคคีภัยสำหรับที่อยู่อาศัย**:\n\n" +
                       "- ให้ความคุ้มครองพื้นฐาน: ไฟไหม้, ฟ้าผ่า, ระเบิด, ยานพาหนะชน, อากาศยานตกใส่, และภัยจากน้ำ (ท่อน้ำแตก)\n" +
                       "- มีการขยายความคุ้มครอง **4 ภัยธรรมชาติ** ให้อัตโนมัติ (ลมพายุ, น้ำท่วม, แผ่นดินไหว/สึนามิ, ลูกเห็บ) รวมกันไม่เกิน 20,000 บาทต่อปีค่ะ\n" +
                       "- 💡 **ข้อควรระวัง:** ควรตั้งทุนประกันภัยอย่างน้อย **80% ของมูลค่าก่อสร้างตัวบ้าน** เพื่อให้ได้รับการยกเว้นกฎการเฉลี่ยความเสียหาย (Average Clause) และได้รับค่าสินไหมเต็มจำนวนค่ะ";
            }

            // 8. PA / อุบัติเหตุส่วนบุคคล
            if (lower.Contains("pa") || lower.Contains("อุบัติเหตุส่วนบุคคล"))
            {
                return "การ **ประกันอุบัติเหตุส่วนบุคคล (PA)**:\n\n" +
                       "- **อบ.1:** คุ้มครองการเสียชีวิต, ทุพพลภาพถาวรสิ้นเชิง, และการสูญเสียมือ เท้า สายตา\n" +
                       "- **อบ.2:** เพิ่มเติมความคุ้มครองการสูญเสียนิ้ว, หูหนวก, เป็นใบ้, และทุพพลภาพถาวรบางส่วน\n" +
                       "- ⚠️ **ข้อยกเว้นสำคัญ:** การขับขี่หรือโดยสารรถจักรยานยนต์เป็นข้อยกเว้นมาตรฐาน หากใช้มอเตอร์ไซค์เป็นประจำ ต้องเลือกซื้อความคุ้มครองขยายมอเตอร์ไซค์เพิ่มเติมนะคะ";
            }

            // 9. ซ่อมห้าง / ซ่อมอู่
            if ((lower.Contains("ซ่อมห้าง") || lower.Contains("ซ่อมศูนย์") || lower.Contains("ซ่อมอู่")) &&
                (lower.Contains("ต่าง") || lower.Contains("เลือก") || lower.Contains("แบบไหน") || lower.Contains("ดีกว่า")))
            {
                return "**ซ่อมห้าง/ศูนย์** ใช้อะไหล่และขั้นตอนตามศูนย์บริการ เหมาะกับรถใหม่หรือผู้ที่ต้องการใช้ศูนย์ตามยี่ห้อ ส่วน **ซ่อมอู่** ใช้อู่ในเครือข่ายบริษัทประกัน เบี้ยมักประหยัดกว่าและควรตรวจรายชื่ออู่ใกล้บ้านก่อนเลือกค่ะ\n\nทั้งนี้สิทธิซ่อมขึ้นกับแผนและอายุรถที่บริษัทรับประกันได้ รบกวนดูคำว่า ‘ซ่อมห้าง’ หรือ ‘ซ่อมอู่’ ในตารางแผนก่อนตัดสินใจนะคะ";
            }

            // 10. ส่วนลดประวัติดี / Excess / Deductible
            if (lower.Contains("ncd") || lower.Contains("ประวัติดี") || lower.Contains("ส่วนลด") && lower.Contains("เคลม"))
            {
                return "**ส่วนลดประวัติดี (NCD)** คือส่วนลดเบี้ยต่ออายุสำหรับผู้ที่ไม่มีเคลมเป็นฝ่ายผิดตามหลักเกณฑ์ของบริษัทค่ะ อัตราและเงื่อนไขสะสมส่วนลดแตกต่างกัน จึงควรใช้กรมธรรม์เดิมตรวจสอบกับบริษัทก่อนเสนอราคา\n\nหากมีการเคลม ไม่ได้แปลว่าส่วนลดหายเสมอไป เพราะขึ้นกับประเภทเคลมและความรับผิดค่ะ";
            }
            if (lower.Contains("excess") || lower.Contains("deductible") || lower.Contains("ค่าเสียหายส่วนแรก"))
            {
                return "**Excess/Deductible (ค่าเสียหายส่วนแรก)** คือจำนวนเงินที่ผู้เอาประกันร่วมรับผิดชอบในบางเหตุการณ์หรือเมื่อเลือกแผนที่มีเงื่อนไขนี้ค่ะ\n\nจำนวนเงินและกรณีที่ต้องชำระต้องดูหน้าตารางกรมธรรม์/เอกสารแนบท้ายของแผนนั้นโดยตรง ไม่ควรสรุปจากชื่อชั้นประกันอย่างเดียวค่ะ";
            }

            // 11. ต่ออายุ / เคลม / เอกสาร
            if (lower.Contains("ต่ออายุ") || lower.Contains("ต่อประกัน") || lower.Contains("ประกันหมด"))
            {
                return "ช่วยดูเรื่อง **ต่ออายุประกัน** ได้ค่ะ เตรียมประเภทประกัน วันหมดอายุ เลขกรมธรรม์เดิม (ถ้ามี) และประวัติการเคลมไว้เพื่อให้เจ้าหน้าที่ตรวจสิทธิและเสนอทางเลือกที่ต่อเนื่องนะคะ\n\nอย่าปล่อยให้กรมธรรม์ขาดช่วง และตรวจความคุ้มครอง/ทุน/เงื่อนไขเดิมก่อนตัดสินใจต่ออายุค่ะ";
            }
            if (lower.Contains("เคลม") || lower.Contains("เรียกร้องค่าสินไหม") || lower.Contains("แจ้งเหตุ"))
            {
                return "เมื่อเกิดเหตุและต้องการ **เคลม**: 1) ดูแลความปลอดภัยและผู้บาดเจ็บก่อน 2) เก็บภาพ/วิดีโอ สถานที่ เวลา และข้อมูลคู่กรณีเท่าที่ทำได้ 3) แจ้งบริษัทประกันตามช่องทางในกรมธรรม์โดยเร็ว 4) เก็บเอกสารและเลขเคลมไว้ค่ะ\n\nผลการพิจารณาสินไหมขึ้นกับข้อเท็จจริง กรมธรรม์ และเงื่อนไขของบริษัท จึงไม่ควรซ่อมหรือรับผิดแทนคู่กรณีก่อนสอบถามบริษัทเมื่อยังไม่แน่ใจนะคะ";
            }

            // 12. ประกันกลุ่ม / สวัสดิการพนักงาน
            if (lower.Contains("ประกันกลุ่ม") || lower.Contains("สวัสดิการพนักงาน") || lower.Contains("employee benefit") || lower.Contains("ประกันสุขภาพกลุ่ม"))
            {
                return "**ประกันกลุ่ม/สวัสดิการพนักงาน** สามารถจัดความคุ้มครองชีวิต สุขภาพ และ PA ให้พนักงานได้ โดยแบบและเงื่อนไขขึ้นกับขนาดกลุ่มและนโยบายบริษัทค่ะ\n\nหากต้องการใบเสนอราคา เตรียมจำนวนพนักงาน ช่วงอายุ ลักษณะงาน สถานที่ทำงาน และสวัสดิการเดิม เพื่อให้เจ้าหน้าที่ออกแบบความคุ้มครองที่เหมาะสมได้ค่ะ";
            }

            // 13. ประกันชีวิต — ระบบมีข้อมูลผลิตภัณฑ์จริงเฉพาะตัวอย่างตลอดชีพ
            if (lower.Contains("ประกันชีวิต") || lower.Contains("ตลอดชีพ") || lower.Contains("สะสมทรัพย์") || lower.Contains("บำนาญ") || lower.Contains("unit linked") || lower.Contains("ยูนิตลิงก์"))
            {
                return "ประกันชีวิตควรเลือกจากเป้าหมายก่อนค่ะ: **ตลอดชีพ** เพื่อหลักประกันครอบครัว, **ชั่วระยะเวลา** เพื่อคุ้มครองหนี้/ภาระช่วงหนึ่ง, **สะสมทรัพย์** เพื่อออมอย่างมีวินัย, และ **บำนาญ** เพื่อรายได้หลังเกษียณ\n\nสำหรับ **Unit Linked** ผลตอบแทนส่วนลงทุนไม่การันตีและมีความเสี่ยง ต้องพิจารณาค่าธรรมเนียมและเอกสารเสนอขายก่อนตัดสินใจ ระบบยังไม่มีราคาอัตโนมัติสำหรับประกันชีวิต จึงให้ผู้เชี่ยวชาญตรวจความต้องการและออกใบเสนอราคาจริงค่ะ";
            }

            // 14. ขนส่งสินค้า / ความรับผิด / วิศวกรรม
            if (lower.Contains("ขนส่งสินค้า") || lower.Contains("marine cargo") || lower.Contains("นำเข้า") || lower.Contains("ส่งออก") || lower.Contains("incoterms") || lower.Contains("cif") || lower.Contains("fob"))
            {
                return "ประกัน **ขนส่งสินค้า (Marine Cargo)** ต้องพิจารณาชนิดและมูลค่าสินค้า เส้นทาง วิธีขนส่ง และ Incoterms เช่น CIF/FOB ค่ะ ความเสี่ยงอาจโอนคนละจุดกับผู้ที่ชำระค่าเบี้ย จึงควรตรวจเงื่อนไขซื้อขายและเอกสารประกันทุกครั้ง\n\nหากต้องการขอราคา กรุณาเตรียมรายละเอียดสินค้า มูลค่า ต้นทาง-ปลายทาง และเที่ยว/ความถี่ในการส่งค่ะ";
            }
            if (lower.Contains("public liability") || lower.Contains("product liability") || lower.Contains("ความรับผิด") || lower.Contains("ลูกค้าลื่นล้ม"))
            {
                return "ประกัน **ความรับผิดต่อบุคคลภายนอก** เหมาะกับกิจการที่กังวลความเสียหายต่อผู้มาใช้บริการหรือทรัพย์สินของผู้อื่น เช่น ลูกค้าลื่นล้มในร้าน ส่วน Product Liability เน้นความเสียหายจากสินค้าที่ผลิต/จำหน่ายค่ะ\n\nเพื่อขอราคา ควรระบุประเภทธุรกิจ ขนาด/สถานที่ทำการ ประวัติการเคลม และวงเงินที่ต้องการ โดยความคุ้มครองจริงขึ้นกับเงื่อนไขกรมธรรม์ค่ะ";
            }
            if (lower.Contains("car insurance") || lower.Contains("car/ear") || lower.Contains("ear insurance") || lower.Contains("งานก่อสร้าง") || lower.Contains("เครื่องจักร") || lower.Contains("วิศวกรรม"))
            {
                return "ประกัน **วิศวกรรม** เช่น CAR สำหรับงานก่อสร้าง, EAR สำหรับงานติดตั้ง และประกันเครื่องจักร/อุปกรณ์ ต้องกำหนดตามลักษณะโครงการและความเสี่ยงหน้างานค่ะ\n\nขอข้อมูลประเภทงาน มูลค่า ระยะเวลา สถานที่ ผู้รับเหมา และความคุ้มครองความรับผิดต่อบุคคลภายนอกที่ต้องการ เพื่อให้เจ้าหน้าที่ตรวจเงื่อนไขและออกใบเสนอราคาได้ค่ะ";
            }

            return null;
        }
    }
}
