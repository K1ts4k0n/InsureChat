using System.Text.Json;
using InsureChatBot.Api.Models.Chatbot;

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
    }
}
