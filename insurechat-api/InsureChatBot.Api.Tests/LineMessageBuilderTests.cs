using System.Text.Json;
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Line;
using Xunit;

namespace InsureChatBot.Api.Tests;

public class LineMessageBuilderTests
{
    [Fact]
    public void CreateOrderConfirmationFlex_uses_selected_plan_facts_and_acknowledgement_button()
    {
        var proposal = new ChatbotProposalSummary
        {
            PackageId = 7, Brand = "ISUZU", Model = "D-MAX", Year = 2024,
            InsurerName = "บริษัททดสอบ", InsuranceClass = "1", SumInsure = 550000,
            Premium = 17000, Total = 17500
        };
        var detail = new MotorPackageDetail
        {
            RenovType = "ห้าง", SumInsure = 550000, PremTotal = 17500,
            FireTheft = 550000, Flood = 100000, Deductible = 2000
        };

        var json = JsonSerializer.Serialize(LineMessageBuilder.CreateOrderConfirmationFlex(proposal, detail, "ORD-2026-000004"),
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.Contains("Deduct", json);
        Assert.Contains("2,000", json);
        Assert.Contains("อุปกรณ์ตกแต่ง", json);
        Assert.Contains("ไม่มีข้อมูล", json);
        Assert.Contains("ยืนยันรับคำสั่งซื้อ", json);
        Assert.Contains("ORD-2026-000004", json);
        Assert.DoesNotContain("ช่วยเหลือฉุกเฉิน", json);
    }
}
