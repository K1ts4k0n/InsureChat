using System.Collections.Generic;

namespace InsureChatBot.Api.Models.Document
{
    /// <summary>ผลดิบจาก vision LLM (classify + extract คอลเดียว) — ADR 0018</summary>
    public class VisionExtractionResult
    {
        public bool IsVehicleDocument { get; set; }
        public string DocType { get; set; }                 // policy_schedule | registration | tax | other
        public double Confidence { get; set; }
        public ExtractedVehicleFields Fields { get; set; } = new();
        public string TableMarkdown { get; set; }
    }

    public class ExtractedVehicleFields
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public int? Year { get; set; }
        public string Plate { get; set; }
        public string Chassis { get; set; }
        public string Insurer { get; set; }
    }

    /// <summary>คำตอบ endpoint /Motor/ExtractDocument → frontend</summary>
    public class DocumentExtractResponse
    {
        public bool IsVehicleDocument { get; set; }
        public string DocType { get; set; }
        public string Table { get; set; }                   // markdown สำหรับโชว์ในแชต
        public ResolvedVehicle Resolved { get; set; } = new();
        public List<string> Unresolved { get; set; } = new();
        public string Message { get; set; }
    }

    public class ResolvedVehicle
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public int? Year { get; set; }
        public string Cc { get; set; } = "";
        public bool BrandOk { get; set; }
        public bool ModelOk { get; set; }
        public bool YearOk { get; set; }
    }
}
