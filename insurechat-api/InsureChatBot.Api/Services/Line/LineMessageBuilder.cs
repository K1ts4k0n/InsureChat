using System.Text.Json;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Line
{
    public static class LineMessageBuilder
    {
        public static List<object> BuildReplyMessages(List<string> replies, List<string>? suggestions, ChatbotConversationContext? context = null)
        {
            var messages = new List<object>();

            var selectedProposal = context?.Proposals?.FirstOrDefault();
            if (context?.CurrentStep == "ready_to_propose" && selectedProposal != null)
            {
                var flexCard = CreateInsuranceProposalFlex(selectedProposal, context);
                messages.Add(flexCard);
            }
            else
            {
                var combinedText = string.Join("\n\n", (replies ?? new List<string>()).Where(r => !string.IsNullOrWhiteSpace(r)));
                if (string.IsNullOrWhiteSpace(combinedText))
                {
                    combinedText = "ยินดีให้คำปรึกษาประกันภัยรถยนต์ครับ สอบถามข้อมูลหรือปรึกษาเบี้ยได้เลยนะครับ 😊";
                }

                var textMsg = new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = combinedText
                };

                if (suggestions != null && suggestions.Count > 0)
                {
                    textMsg["quickReply"] = BuildQuickReply(suggestions);
                }

                messages.Add(textMsg);
            }

            if (messages.Count > 0 && context?.CurrentStep == "ready_to_propose" && selectedProposal != null)
            {
                var followUp = new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = "คุณลูกค้าต้องการยืนยันให้เจ้าหน้าที่ติดต่อกลับเพื่อรับสิทธิพิเศษนี้เลยไหมครับ? เพียงพิมพ์ 'ชื่อ และเบอร์โทร' ไว้ได้เลยครับ"
                };

                if (suggestions != null && suggestions.Count > 0)
                {
                    followUp["quickReply"] = BuildQuickReply(suggestions);
                }
                messages.Add(followUp);
            }

            return messages;
        }

        public static object BuildQuickReply(List<string> suggestions)
        {
            var items = new List<object>();
            foreach (var s in suggestions.Take(10))
            {
                var label = s.Length > 20 ? s.Substring(0, 17) + "..." : s;
                items.Add(new
                {
                    type = "action",
                    action = new
                    {
                        type = "message",
                        label = label,
                        text = s
                    }
                });
            }

            return new { items };
        }

        public static object CreateInsuranceProposalFlex(ChatbotProposalSummary proposal, ChatbotConversationContext? context = null)
        {
            var company = string.IsNullOrWhiteSpace(proposal.InsurerName) ? "บริษัทประกันภัยชั้นนำ" : proposal.InsurerName;
            var premium = proposal.Premium > 0 ? $"{proposal.Premium:N0} บาท/ปี" : "ติดต่อสอบถาม";
            var sumInsure = proposal.SumInsure > 0 ? $"{proposal.SumInsure:N0} บาท" : "ตามเงื่อนไข";
            
            var brand = !string.IsNullOrWhiteSpace(proposal.Brand) ? proposal.Brand : context?.CarBrand ?? "";
            var model = !string.IsNullOrWhiteSpace(proposal.Model) ? proposal.Model : context?.CarModel ?? "";
            var year = proposal.Year > 0 ? proposal.Year.ToString() : (context?.CarYear?.ToString() ?? "");
            var vehicle = $"{brand} {model} {year}".Trim();
            if (string.IsNullOrWhiteSpace(vehicle)) vehicle = "รถยนต์ของคุณ";

            var insuranceClass = !string.IsNullOrWhiteSpace(proposal.InsuranceClass) 
                ? proposal.InsuranceClass 
                : context?.InsuranceClass ?? "1";

            return new
            {
                type = "flex",
                altText = $"ข้อเสนอประกันภัย {vehicle} - {company}",
                contents = new
                {
                    type = "bubble",
                    size = "mega",
                    header = new
                    {
                        type = "box",
                        layout = "vertical",
                        backgroundColor = "#0F172A",
                        paddingAll = "20px",
                        contents = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = "RECOMMENDED PLAN",
                                color = "#38BDF8",
                                weight = "bold",
                                size = "xs"
                            },
                            new
                            {
                                type = "text",
                                text = vehicle,
                                color = "#FFFFFF",
                                weight = "bold",
                                size = "xl",
                                margin = "sm"
                            },
                            new
                            {
                                type = "text",
                                text = $"ประกันชั้น {insuranceClass} • {company}",
                                color = "#94A3B8",
                                size = "sm",
                                margin = "xs"
                            }
                        }
                    },
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        spacing = "md",
                        paddingAll = "20px",
                        contents = new object[]
                        {
                            new
                            {
                                type = "box",
                                layout = "horizontal",
                                contents = new object[]
                                {
                                    new { type = "text", text = "ทุนประกันภัย", size = "sm", color = "#64748B" },
                                    new { type = "text", text = sumInsure, size = "sm", color = "#0F172A", weight = "bold", align = "end" }
                                }
                            },
                            new
                            {
                                type = "box",
                                layout = "horizontal",
                                contents = new object[]
                                {
                                    new { type = "text", text = "เบี้ยประกันสุทธิ", size = "sm", color = "#64748B" },
                                    new { type = "text", text = premium, size = "lg", color = "#0284C7", weight = "bold", align = "end" }
                                }
                            },
                            new { type = "separator", margin = "lg" },
                            new
                            {
                                type = "box",
                                layout = "vertical",
                                margin = "md",
                                spacing = "xs",
                                contents = new object[]
                                {
                                    new { type = "text", text = "✓ ซ่อมศูนย์ / อู่มาตรฐานทั่วประเทศ", size = "xs", color = "#10B981" },
                                    new { type = "text", text = "✓ คุ้มครองอุบัติเหตุและบุคคลภายนอก", size = "xs", color = "#10B981" },
                                    new { type = "text", text = "✓ บริการช่วยเหลือฉุกเฉินตลอด 24 ชม.", size = "xs", color = "#10B981" }
                                }
                            }
                        }
                    },
                    footer = new
                    {
                        type = "box",
                        layout = "vertical",
                        spacing = "sm",
                        paddingAll = "16px",
                        contents = new object[]
                        {
                            new
                            {
                                type = "button",
                                style = "primary",
                                color = "#0284C7",
                                height = "sm",
                                action = new
                                {
                                    type = "message",
                                    label = "สนใจสมัครแผนนี้",
                                    text = $"สนใจสมัครแผนนี้ ({vehicle} ชั้น {insuranceClass})"
                                }
                            },
                            new
                            {
                                type = "button",
                                style = "link",
                                height = "sm",
                                action = new
                                {
                                    type = "message",
                                    label = "เปรียบเทียบแผนอื่น",
                                    text = "มีแผนอื่นให้เปรียบเทียบไหม"
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>การ์ดที่ส่งให้เจ้าหน้าที่หลังรับคำสั่งซื้อ: แสดงเฉพาะค่าที่มีในแผนที่เลือกจริง.</summary>
        public static object CreateOrderConfirmationFlex(ChatbotProposalSummary proposal, MotorPackageDetail detail, string orderNo)
        {
            var vehicle = $"{proposal.Brand} {proposal.Model} {proposal.Year}".Trim();
            var sumInsure = detail?.SumInsure > 0 ? detail.SumInsure : proposal.SumInsure;
            var premium = detail?.PremTotal > 0 ? detail.PremTotal : (proposal.Total > 0 ? proposal.Total : proposal.Premium);
            var repair = string.IsNullOrWhiteSpace(detail?.RenovType) ? "ไม่มีข้อมูล" : detail.RenovType;
            var fire = detail == null ? "ไม่มีข้อมูล" : $"{detail.FireTheft:N0}";
            var flood = detail == null ? "ไม่มีข้อมูล" : $"{detail.Flood:N0}";
            var deductible = detail == null ? "ไม่มีข้อมูล" : $"{detail.Deductible:N0}";

            object Row(string label, string value) => new
            {
                type = "box", layout = "horizontal", spacing = "sm", contents = new object[]
                {
                    new { type = "text", text = label, size = "xs", color = "#64748B", flex = 3 },
                    new { type = "text", text = value, size = "xs", color = "#0F172A", align = "end", flex = 4, wrap = true }
                }
            };

            return new
            {
                type = "flex",
                altText = $"คำสั่งซื้อ {orderNo}: {proposal.InsurerName}",
                contents = new
                {
                    type = "bubble", size = "mega",
                    header = new
                    {
                        type = "box", layout = "vertical", backgroundColor = "#0F172A", paddingAll = "18px",
                        contents = new object[]
                        {
                            new { type = "text", text = "คำสั่งซื้อใหม่", color = "#38BDF8", weight = "bold", size = "xs" },
                            new { type = "text", text = proposal.InsurerName ?? "ไม่มีข้อมูล", color = "#FFFFFF", weight = "bold", size = "xl", margin = "sm", wrap = true },
                            new { type = "text", text = $"{vehicle} · เลขที่ {orderNo}", color = "#CBD5E1", size = "xs", margin = "sm", wrap = true }
                        }
                    },
                    body = new
                    {
                        type = "box", layout = "vertical", spacing = "sm", paddingAll = "18px",
                        contents = new object[]
                        {
                            Row("ชั้น", proposal.InsuranceClass ?? "ไม่มีข้อมูล"), Row("ซ่อม", repair),
                            Row("ทุนประกัน", sumInsure > 0 ? $"{sumInsure:N0} บาท" : "ไม่มีข้อมูล"),
                            Row("เบี้ย/ปี", premium > 0 ? $"{premium:N0} บาท" : "ไม่มีข้อมูล"),
                            Row("ไฟไหม้", fire), Row("Deduct", deductible),
                            Row("อุปกรณ์ตกแต่ง", "ไม่มีข้อมูล"), Row("น้ำท่วม", flood)
                        }
                    },
                    footer = new
                    {
                        type = "box", layout = "vertical", paddingAll = "14px",
                        contents = new object[]
                        {
                            new { type = "button", style = "primary", color = "#0284C7", height = "sm", action = new { type = "message", label = "ยืนยันรับคำสั่งซื้อ", text = $"ยืนยันรับคำสั่งซื้อ {orderNo}" } }
                        }
                    }
                }
            };
        }
    }
}
