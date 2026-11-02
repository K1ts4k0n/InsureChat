# Document Intake Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ให้ลูกค้าแนบรูป/PDF เอกสารรถ → vision LLM classify+extract → resolve เข้าค่า facet ใน DB → prefill ฟอร์มคำนวณเบี้ย/แชตอัตโนมัติ (ephemeral ไม่เก็บไฟล์)

**Architecture:** endpoint เดียว `POST /api/Motor/ExtractDocument` ที่หน้า quote และแชตเรียกร่วมกัน. รูป **ไม่เข้า** chat pipeline (`LlmMessage.Content` ยังเป็น string) — ทำผ่าน `DocumentExtractionService` ที่สร้าง OpenAI-compatible request มี image content-part เอง ด้วยโมเดล vision แยก (`VisionModel`). ค่าที่อ่านได้ถูก resolve เข้าค่า canonical ใน KPN facet DB ด้วย `VehicleResolver` (pure, unit-testable) ก่อน prefill. ดู spec `2026-07-06-document-intake-vision-extraction-design.md` + ADR 0018.

**Tech Stack:** ASP.NET Core net10 (Dapper/EF, xUnit) · Angular 21 (signals) · OpenRouter (google/gemini-2.5-flash) · NuGet `PDFtoImage` (PDFium)

**Repo layout (สำคัญ — โค้ดอยู่คนละ repo กับแผน):**
- `c:\CTD\ChatBot\insurechat-api\` — backend (.NET) — git repo แยก
- `c:\CTD\ChatBot\insurechat-web\` — frontend (Angular) — git repo แยก
- `c:\CTD\ChatBot\InsureChat\` — แผน/spec/ADR/CONTEXT — git repo แยก
- คำสั่ง `cd` ในแต่ละ step ชี้ repo ที่ commit; รันจาก `c:\CTD\ChatBot\`

---

## File Structure

**Backend (`insurechat-api/InsureChatBot.Api/`)**
- Create `Services/Motor/VehicleResolver.cs` — fuzzy match extracted→canonical (pure static)
- Create `Services/Document/IPdfRasterizer.cs` + `PdfRasterizer.cs` — PDF→PNG หน้าแรก
- Create `Services/Document/IDocumentExtractionService.cs` + `DocumentExtractionService.cs` — vision call + parse
- Create `Models/Document/DocumentModels.cs` — VisionExtractionResult / DocumentExtractResponse / ResolvedVehicle
- Modify `Config/AppConfig.cs` — `LlmSettings.VisionModel`
- Modify `Models/Admin/AppSettings.cs` — `LlmOverrides.VisionModel`
- Modify `Services/Settings/AppSettingsStore.cs` + `SqlAppSettingsStore.cs` — merge VisionModel
- Modify `Models/Motor/MotorFacetModels.cs` — `VehicleResolveResult`
- Modify `Services/Motor/IMotorFacetService.cs` + `MotorFacetSqlService.cs` — `ResolveVehicleAsync`
- Modify `Controllers/MotorController.cs` — `ExtractDocument` endpoint
- Modify `Program.cs` — DI สำหรับ IPdfRasterizer + IDocumentExtractionService
- Modify `InsureChatBot.Api.csproj` — เพิ่ม PDFtoImage

**Backend tests (`insurechat-api/InsureChatBot.Api.Tests/`)**
- Create `VehicleResolverTests.cs`, `DocumentExtractionParsingTests.cs`, `MotorControllerExtractDocumentTests.cs`

**Frontend (`insurechat-web/src/app/`)**
- Create `core/doc-upload.ts` — client guard + downscale
- Modify `core/motor-facet.ts` — `extractDocument` + interfaces
- Modify `features/quote/quote.ts` + `quote.html` — ปุ่มแนบ + prefill
- Modify `features/chatbot/chatbot.ts` + `chatbot.html` — ปุ่มแนบ + seed แชต

**Optional (admin editability of VisionModel)** — Task 11

---

## Task 1: เพิ่ม VisionModel เข้า LLM settings (base + override + merge)

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Config/AppConfig.cs:61-73`
- Modify: `insurechat-api/InsureChatBot.Api/Models/Admin/AppSettings.cs:58-67`
- Modify: `insurechat-api/InsureChatBot.Api/Services/Settings/AppSettingsStore.cs:74-84`
- Modify: `insurechat-api/InsureChatBot.Api/Services/Settings/SqlAppSettingsStore.cs:66-77`

- [ ] **Step 1: เพิ่มฟิลด์ VisionModel ใน LlmSettings**

ใน `Config/AppConfig.cs` คลาส `LlmSettings` เพิ่มหลังบรรทัด `public string Model ...`:

```csharp
        /// <summary>โมเดล vision แยกสำหรับ Document intake — โมเดลสนทนา (Model) อาจ text-only. ดู ADR 0018</summary>
        public string VisionModel { get; set; } = "google/gemini-2.5-flash";
```

- [ ] **Step 2: เพิ่ม VisionModel ใน LlmOverrides**

ใน `Models/Admin/AppSettings.cs` คลาส `LlmOverrides` เพิ่มหลัง `public string Model { get; set; }`:

```csharp
        public string VisionModel { get; set; }
```

- [ ] **Step 3: merge VisionModel ใน AppSettingsStore.GetEffectiveLlm**

ใน `Services/Settings/AppSettingsStore.cs` ใน `GetEffectiveLlm()` เพิ่มบรรทัดในการสร้าง `new LlmSettings { ... }` (หลัง `Model = ...`):

```csharp
                VisionModel = string.IsNullOrWhiteSpace(o.VisionModel) ? b.VisionModel : o.VisionModel,
```

- [ ] **Step 4: merge VisionModel ใน SqlAppSettingsStore.GetEffectiveLlm**

ใน `Services/Settings/SqlAppSettingsStore.cs` ใน `GetEffectiveLlm()` เพิ่มบรรทัดเดียวกัน (หลัง `Model = ...`):

```csharp
                VisionModel = string.IsNullOrWhiteSpace(o.VisionModel) ? b.VisionModel : o.VisionModel,
```

- [ ] **Step 5: Build ยืนยันคอมไพล์ผ่าน**

Run: `cd insurechat-api && dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj`
Expected: Build succeeded, 0 errors

> หมายเหตุ: `SqlAppSettingsStore` ยังไม่ persist VisionModel ลง DB (ไม่มีคอลัมน์) → `o.VisionModel` เป็น null เสมอ → fallback ค่า base (`google/gemini-2.5-flash`). แก้ผ่าน `appsettings.json` `Llm:VisionModel` ได้ทันที; admin-editable ทำใน Task 11.

- [ ] **Step 6: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(llm): add VisionModel setting (default google/gemini-2.5-flash)"
```

---

## Task 2: VehicleResolver — fuzzy match (pure, TDD)

**Files:**
- Create: `insurechat-api/InsureChatBot.Api/Services/Motor/VehicleResolver.cs`
- Test: `insurechat-api/InsureChatBot.Api.Tests/VehicleResolverTests.cs`

- [ ] **Step 1: เขียน test ที่ยังไม่ผ่าน**

สร้าง `InsureChatBot.Api.Tests/VehicleResolverTests.cs`:

```csharp
using System.Collections.Generic;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Motor;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class VehicleResolverTests
    {
        private static List<FacetItem> F(params string[] vals)
        {
            var list = new List<FacetItem>();
            foreach (var v in vals) list.Add(new FacetItem { Value = v, Label = v });
            return list;
        }

        [Fact]
        public void Brand_exact_normalized_match()
            => Assert.Equal("KIA", VehicleResolver.ResolveBrand("kia", F("KIA", "HONDA")));

        [Fact]
        public void Brand_contains_match()
            => Assert.Equal("KIA", VehicleResolver.ResolveBrand("KIA MOTORS", F("KIA", "HONDA")));

        [Fact]
        public void Brand_no_match_returns_null()
            => Assert.Null(VehicleResolver.ResolveBrand("FORD", F("TOYOTA", "HONDA")));

        [Fact]
        public void Model_maps_spaced_to_canonical()
            => Assert.Equal("K2700", VehicleResolver.ResolveModel("K 2700 M", F("K2700", "CERATO")));

        [Fact]
        public void Model_prefers_exact_over_contains()
            => Assert.Equal("CIVIC", VehicleResolver.ResolveModel("CIVIC", F("CIVIC FD", "CIVIC")));

        [Fact]
        public void Model_no_match_returns_null()
            => Assert.Null(VehicleResolver.ResolveModel("ZZZ", F("CIVIC")));

        [Fact]
        public void Year_present_returns_year()
            => Assert.Equal(2005, VehicleResolver.ResolveYear(2005, F("2004", "2005", "2006")));

        [Fact]
        public void Year_absent_returns_null()
            => Assert.Null(VehicleResolver.ResolveYear(2000, F("2004", "2005")));

        [Fact]
        public void Year_null_input_returns_null()
            => Assert.Null(VehicleResolver.ResolveYear(null, F("2005")));
    }
}
```

- [ ] **Step 2: รัน test ให้ fail**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~VehicleResolverTests`
Expected: FAIL — `VehicleResolver` ไม่มี (compile error)

- [ ] **Step 3: เขียน implementation ขั้นต่ำ**

สร้าง `InsureChatBot.Api/Services/Motor/VehicleResolver.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Motor
{
    /// <summary>
    /// แมตช์ค่าที่ vision อ่านได้ (free text) → ค่า canonical ใน facet DB ก่อน prefill.
    /// pure = unit-testable ไม่พึ่ง DB. ดู ADR 0018 (Facet resolve).
    /// </summary>
    public static class VehicleResolver
    {
        // เทียบแบบ normalize: ตัวพิมพ์ใหญ่ + เหลือเฉพาะตัวอักษร/ตัวเลข ("K 2700 M" → "K2700M")
        private static string Norm(string s)
            => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static string ResolveBrand(string extracted, IReadOnlyList<FacetItem> brands)
        {
            if (string.IsNullOrWhiteSpace(extracted) || brands == null) return null;
            var n = Norm(extracted);
            if (n.Length == 0) return null;

            var exact = brands.FirstOrDefault(b => Norm(b.Value) == n);
            if (exact != null) return exact.Value;

            return brands
                .Where(b => { var bn = Norm(b.Value); return bn.Length > 0 && (bn.Contains(n) || n.Contains(bn)); })
                .OrderBy(b => Norm(b.Value).Length)
                .FirstOrDefault()?.Value;
        }

        public static string ResolveModel(string extracted, IReadOnlyList<FacetItem> models)
        {
            if (string.IsNullOrWhiteSpace(extracted) || models == null) return null;
            var n = Norm(extracted);
            if (n.Length == 0) return null;

            var exact = models.FirstOrDefault(m => Norm(m.Value) == n);
            if (exact != null) return exact.Value;

            return models
                .Where(m => { var mn = Norm(m.Value); return mn.Length > 0 && (mn.Contains(n) || n.Contains(mn)); })
                .OrderBy(m => Norm(m.Value).Length)   // เลือก canonical ที่สั้น/ใกล้สุด ("K2700" ก่อน "K2700MEGA")
                .FirstOrDefault()?.Value;
        }

        public static int? ResolveYear(int? extracted, IReadOnlyList<FacetItem> years)
        {
            if (extracted == null || years == null) return null;
            var target = extracted.Value.ToString();
            return years.Any(y => y.Value == target) ? extracted : (int?)null;
        }
    }
}
```

- [ ] **Step 4: รัน test ให้ผ่าน**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~VehicleResolverTests`
Expected: PASS (9 tests)

- [ ] **Step 5: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(motor): VehicleResolver fuzzy-match extracted values to facet canon"
```

---

## Task 3: ResolveVehicleAsync ใน MotorFacet

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Models/Motor/MotorFacetModels.cs`
- Modify: `insurechat-api/InsureChatBot.Api/Services/Motor/IMotorFacetService.cs`
- Modify: `insurechat-api/InsureChatBot.Api/Services/Motor/MotorFacetSqlService.cs`

- [ ] **Step 1: เพิ่ม VehicleResolveResult**

ใน `Models/Motor/MotorFacetModels.cs` เพิ่มคลาสท้ายไฟล์ (ก่อน `}` ปิด namespace):

```csharp
    /// <summary>ผลการ resolve เอกสารรถ → ค่า facet canonical (ADR 0018)</summary>
    public class VehicleResolveResult
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public int? Year { get; set; }
        public bool BrandOk => Brand != null;
        public bool ModelOk => Model != null;
        public bool YearOk => Year != null;
    }
```

- [ ] **Step 2: เพิ่มเมธอดใน interface**

ใน `Services/Motor/IMotorFacetService.cs` เพิ่มก่อน `}` ปิด interface:

```csharp
        /// <summary>resolve ค่าที่ vision อ่านได้ → ค่า facet canonical (cascade brand→model→year). ADR 0018</summary>
        Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year);
```

- [ ] **Step 3: implement ใน MotorFacetSqlService**

ใน `Services/Motor/MotorFacetSqlService.cs` เพิ่มเมธอด (วางก่อน `}` ปิดคลาส) — orchestrate facet เดิม + `VehicleResolver`:

```csharp
        public async Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year)
        {
            var result = new VehicleResolveResult();

            var brands = await GetBrandsAsync();
            result.Brand = VehicleResolver.ResolveBrand(brand, brands);
            if (result.Brand == null) return result;

            var models = await GetModelsAsync(result.Brand);
            result.Model = VehicleResolver.ResolveModel(model, models);
            if (result.Model == null) return result;

            var years = await GetYearsAsync(result.Brand, result.Model, "");
            result.Year = VehicleResolver.ResolveYear(year, years);
            return result;
        }
```

ตรวจว่าไฟล์มี `using InsureChatBot.Api.Models.Motor;` แล้ว (มีอยู่แล้วจาก type เดิม); ถ้ายังไม่มี ให้เพิ่ม

- [ ] **Step 4: Build ยืนยันผ่าน**

Run: `cd insurechat-api && dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(motor): ResolveVehicleAsync cascade brand/model/year via facets"
```

---

## Task 4: PdfRasterizer — PDF หน้าแรก → PNG

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj`
- Create: `insurechat-api/InsureChatBot.Api/Services/Document/IPdfRasterizer.cs`
- Create: `insurechat-api/InsureChatBot.Api/Services/Document/PdfRasterizer.cs`
- Test: `insurechat-api/InsureChatBot.Api.Tests/PdfRasterizerTests.cs`

- [ ] **Step 1: เพิ่มแพ็กเกจ PDFtoImage**

Run: `cd insurechat-api && dotnet add InsureChatBot.Api/InsureChatBot.Api.csproj package PDFtoImage`
Expected: package + SkiaSharp/PDFium natives ถูกเพิ่ม, restore สำเร็จ

- [ ] **Step 2: เขียน test guard ที่ยังไม่ผ่าน**

สร้าง `InsureChatBot.Api.Tests/PdfRasterizerTests.cs`:

```csharp
using System;
using InsureChatBot.Api.Services.Document;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class PdfRasterizerTests
    {
        [Fact]
        public void Empty_pdf_throws()
        {
            var r = new PdfRasterizer();
            Assert.Throws<ArgumentException>(() => r.RenderFirstPagePng(Array.Empty<byte>()));
        }

        [Fact]
        public void Null_pdf_throws()
        {
            var r = new PdfRasterizer();
            Assert.Throws<ArgumentException>(() => r.RenderFirstPagePng(null));
        }
    }
}
```

- [ ] **Step 3: รัน test ให้ fail**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~PdfRasterizerTests`
Expected: FAIL — `PdfRasterizer` ไม่มี

- [ ] **Step 4: เขียน interface + implementation**

สร้าง `InsureChatBot.Api/Services/Document/IPdfRasterizer.cs`:

```csharp
namespace InsureChatBot.Api.Services.Document
{
    /// <summary>PDF → PNG หน้าแรก (สำหรับส่งให้ vision LLM). รับหน้าแรกเท่านั้น (ADR 0018)</summary>
    public interface IPdfRasterizer
    {
        byte[] RenderFirstPagePng(byte[] pdf);
    }
}
```

สร้าง `InsureChatBot.Api/Services/Document/PdfRasterizer.cs`:

```csharp
using System;
using PDFtoImage;
using SkiaSharp;

namespace InsureChatBot.Api.Services.Document
{
    public class PdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf)
        {
            if (pdf == null || pdf.Length == 0)
                throw new ArgumentException("PDF ว่าง", nameof(pdf));

            // render หน้าแรก (index 0) ที่ 150 DPI → SKBitmap → PNG
            // NOTE: overload ของ Conversion.ToImage ต่างตามเวอร์ชัน PDFtoImage — ปรับตาม README ของแพ็กเกจที่ติดตั้ง
            using var bitmap = Conversion.ToImage(pdf, page: 0, options: new RenderOptions(Dpi: 150));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }
    }
}
```

- [ ] **Step 5: รัน test ให้ผ่าน**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~PdfRasterizerTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Manual integration verify (render จริง)**

รันแอป (Task 10) แล้วอัปโหลด PDF 1 หน้าในหน้า quote → ต้องไม่ error 502 และได้ผล extract. (ไม่มี unit test เพราะต้องมี PDF fixture + PDFium native)

- [ ] **Step 7: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(document): PdfRasterizer first-page PDF->PNG via PDFtoImage"
```

---

## Task 5: DocumentExtractionService — vision call + JSON parse (TDD บน parser)

**Files:**
- Create: `insurechat-api/InsureChatBot.Api/Models/Document/DocumentModels.cs`
- Create: `insurechat-api/InsureChatBot.Api/Services/Document/IDocumentExtractionService.cs`
- Create: `insurechat-api/InsureChatBot.Api/Services/Document/DocumentExtractionService.cs`
- Test: `insurechat-api/InsureChatBot.Api.Tests/DocumentExtractionParsingTests.cs`

- [ ] **Step 1: สร้าง DTO models**

สร้าง `InsureChatBot.Api/Models/Document/DocumentModels.cs`:

```csharp
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
```

- [ ] **Step 2: เขียน test ของ parser ที่ยังไม่ผ่าน**

สร้าง `InsureChatBot.Api.Tests/DocumentExtractionParsingTests.cs`:

```csharp
using InsureChatBot.Api.Services.Document;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class DocumentExtractionParsingTests
    {
        [Fact]
        public void Parses_plain_json()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "{\"isVehicleDocument\":true,\"docType\":\"policy_schedule\",\"fields\":{\"brand\":\"KIA\",\"year\":2005}}");
            Assert.True(r.IsVehicleDocument);
            Assert.Equal("KIA", r.Fields.Brand);
            Assert.Equal(2005, r.Fields.Year);
        }

        [Fact]
        public void Parses_json_wrapped_in_code_fence()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "```json\n{\"isVehicleDocument\":false,\"docType\":\"other\"}\n```");
            Assert.False(r.IsVehicleDocument);
            Assert.Equal("other", r.DocType);
        }

        [Fact]
        public void Reads_year_when_returned_as_string()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "{\"isVehicleDocument\":true,\"fields\":{\"year\":\"2020\"}}");
            Assert.Equal(2020, r.Fields.Year);
        }
    }
}
```

- [ ] **Step 3: รัน test ให้ fail**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~DocumentExtractionParsingTests`
Expected: FAIL — `DocumentExtractionService` ไม่มี

- [ ] **Step 4: สร้าง interface**

สร้าง `InsureChatBot.Api/Services/Document/IDocumentExtractionService.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;

namespace InsureChatBot.Api.Services.Document
{
    public interface IDocumentExtractionService
    {
        /// <summary>ส่งรูป (PNG/JPEG bytes) ให้ vision LLM → classify + extract คอลเดียว</summary>
        Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default);
    }
}
```

- [ ] **Step 5: สร้าง implementation + static parser**

สร้าง `InsureChatBot.Api/Services/Document/DocumentExtractionService.cs`:

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Services.Llm;
using InsureChatBot.Api.Services.Settings;

namespace InsureChatBot.Api.Services.Document
{
    /// <summary>
    /// เรียก vision LLM (OpenAI-compatible) ด้วย image content-part + VisionModel แยก.
    /// ไม่ยุ่งกับ LlmMessage/IChatCompletionClient (chat pipeline). ephemeral — ไม่ log ภาพ. ADR 0018.
    /// </summary>
    public class DocumentExtractionService : IDocumentExtractionService
    {
        private readonly HttpClient _http;
        private readonly IAppSettingsStore _settings;

        private static readonly JsonSerializerOptions ParseOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private const string SystemPrompt =
            "คุณเป็นตัวช่วยอ่านเอกสารรถยนต์ของไทย (ตารางกรมธรรม์ประกันภัยรถยนต์ / เล่มทะเบียน / ป้ายภาษี). " +
            "ตอบกลับ 'เฉพาะ JSON' เท่านั้น (ห้ามมีข้อความอื่น) รูปแบบ: " +
            "{\"isVehicleDocument\": true/false, \"docType\": \"policy_schedule|registration|tax|other\", " +
            "\"confidence\": 0-1, \"fields\": {\"brand\": \"\", \"model\": \"\", \"year\": 0, \"plate\": \"\", " +
            "\"chassis\": \"\", \"insurer\": \"\"}, \"tableMarkdown\": \"ตาราง markdown สรุปทุกฟิลด์ที่อ่านได้\"}. " +
            "ถ้าไม่ใช่เอกสารเกี่ยวกับรถยนต์ ให้ isVehicleDocument=false. year เป็นปี ค.ศ. (แปลง พ.ศ.→ค.ศ. ให้ด้วย).";

        public DocumentExtractionService(HttpClient http, IAppSettingsStore settings)
        {
            _settings = settings;
            _http = http;
            var s = settings.GetEffectiveLlm();
            _http.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
        }

        public async Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default)
        {
            var s = _settings.GetEffectiveLlm();
            var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(image)}";

            var payload = new
            {
                model = s.VisionModel,
                temperature = 0.1,
                max_tokens = 1500,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = new object[]
                    {
                        new { type = "text", text = "อ่านเอกสารนี้แล้วตอบเป็น JSON ตามรูปแบบที่กำหนด" },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }}
                }
            };

            var baseUrl = s.BaseUrl.TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
            {
                Content = JsonContent.Create(payload, options: OpenAiCompatibleClient.Json)
            };
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"vision LLM {(int)resp.StatusCode}");

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenAiCompatibleClient.Json);
            var content = parsed?.Choices is { Count: > 0 } ? parsed.Choices[0].Message?.Content : null;
            return ParseVisionJson(content);
        }

        /// <summary>ตัด code fence + คว้าเฉพาะบล็อก JSON แรก แล้ว deserialize. static → unit-testable</summary>
        public static VisionExtractionResult ParseVisionJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("vision ตอบว่าง");

            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                int nl = json.IndexOf('\n');
                if (nl >= 0) json = json.Substring(nl + 1);
                int fence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) json = json.Substring(0, fence);
            }
            int a = json.IndexOf('{');
            int b = json.LastIndexOf('}');
            if (a >= 0 && b > a) json = json.Substring(a, b - a + 1);

            return JsonSerializer.Deserialize<VisionExtractionResult>(json, ParseOpts) ?? new VisionExtractionResult();
        }
    }
}
```

- [ ] **Step 6: รัน test ให้ผ่าน**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~DocumentExtractionParsingTests`
Expected: PASS (3 tests)

- [ ] **Step 7: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(document): DocumentExtractionService vision classify+extract + JSON parse"
```

---

## Task 6: MotorController.ExtractDocument endpoint (TDD)

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Controllers/MotorController.cs`
- Modify: `insurechat-api/InsureChatBot.Api/Program.cs:92-95`
- Test: `insurechat-api/InsureChatBot.Api.Tests/MotorControllerExtractDocumentTests.cs`

- [ ] **Step 0: ให้ test project อ้าง ASP.NET framework (สำหรับ FormFile/ControllerBase)**

เปิด `insurechat-api/InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj` — ถ้ายังไม่มี `<FrameworkReference Include="Microsoft.AspNetCore.App" />` ให้เพิ่มใน `<ItemGroup>`:

```xml
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
```

จำเป็นเพราะเทสต์ instantiate `MotorController` (ControllerBase) + ใช้ `IFormFile`/`FormFile`/`BadRequestObjectResult`

- [ ] **Step 1: ลงทะเบียน DI**

ใน `Program.cs` หลังบรรทัด `builder.Services.AddScoped<...MotorFacetSqlService>();` (บรรทัด ~95) เพิ่ม:

```csharp
builder.Services.AddScoped<InsureChatBot.Api.Services.Document.IPdfRasterizer, InsureChatBot.Api.Services.Document.PdfRasterizer>();
builder.Services.AddHttpClient<InsureChatBot.Api.Services.Document.IDocumentExtractionService, InsureChatBot.Api.Services.Document.DocumentExtractionService>();
```

- [ ] **Step 2: เขียน test controller ที่ยังไม่ผ่าน**

สร้าง `InsureChatBot.Api.Tests/MotorControllerExtractDocumentTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using InsureChatBot.Api.Controllers;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Document;
using InsureChatBot.Api.Services.Motor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class MotorControllerExtractDocumentTests
    {
        // ---- fakes ----
        private class FakeExtraction : IDocumentExtractionService
        {
            public VisionExtractionResult Result = new();
            public Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default)
                => Task.FromResult(Result);
        }
        private class FakePdf : IPdfRasterizer
        {
            public byte[] RenderFirstPagePng(byte[] pdf) => pdf;
        }
        private class FakeFacets : IMotorFacetService
        {
            public VehicleResolveResult Resolve = new();
            public Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year)
                => Task.FromResult(Resolve);
            // ไม่ใช้ในเทสต์นี้
            public Task<List<FacetItem>> GetBrandsAsync() => throw new NotImplementedException();
            public Task<List<FacetItem>> GetModelsAsync(string brand) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetCcAsync(string brand, string model) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetYearsAsync(string brand, string model, string cc) => throw new NotImplementedException();
            public Task<List<PolTypeItem>> GetPolTypesAsync(string brand, string model, string cc, int year) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetFireSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetFloodSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
            public Task<List<FacetItem>> GetProvincesAsync() => throw new NotImplementedException();
            public Task<List<PackageRow>> SearchPackagesAsync(PackageSearchRequest req) => throw new NotImplementedException();
            public Task<MotorPackageDetail> GetPackageDetailAsync(int packageId) => throw new NotImplementedException();
        }

        private static IFormFile MakeFile(string contentType, long length, byte[] content = null)
        {
            var bytes = content ?? new byte[] { 1, 2, 3 };
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, length, "file", "doc")
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        private static MotorController NewController(FakeExtraction ex, FakeFacets fc)
            => new MotorController(fc, ex, new FakePdf(), NullLogger<MotorController>.Instance);

        [Fact]
        public async Task Rejects_unsupported_type_400()
        {
            var c = NewController(new FakeExtraction(), new FakeFacets());
            var res = await c.ExtractDocument(MakeFile("text/plain", 3), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
        }

        [Fact]
        public async Task Rejects_oversize_400()
        {
            var c = NewController(new FakeExtraction(), new FakeFacets());
            var res = await c.ExtractDocument(MakeFile("image/png", 9L * 1024 * 1024), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
        }

        [Fact]
        public async Task Non_vehicle_returns_422()
        {
            var ex = new FakeExtraction { Result = new VisionExtractionResult { IsVehicleDocument = false, DocType = "other" } };
            var c = NewController(ex, new FakeFacets());
            var res = await c.ExtractDocument(MakeFile("image/png", 3), CancellationToken.None);
            var obj = Assert.IsType<ObjectResult>(res);
            Assert.Equal(422, obj.StatusCode);
        }

        [Fact]
        public async Task Vehicle_resolved_returns_200_no_unresolved()
        {
            var ex = new FakeExtraction { Result = new VisionExtractionResult
            {
                IsVehicleDocument = true, DocType = "policy_schedule",
                Fields = new ExtractedVehicleFields { Brand = "KIA", Model = "K 2700 M", Year = 2005 },
                TableMarkdown = "| a | b |"
            }};
            var fc = new FakeFacets { Resolve = new VehicleResolveResult { Brand = "KIA", Model = "K2700", Year = 2005 } };
            var c = NewController(ex, fc);
            var res = await c.ExtractDocument(MakeFile("image/png", 3), CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<DocumentExtractResponse>(ok.Value);
            Assert.True(dto.IsVehicleDocument);
            Assert.Equal("K2700", dto.Resolved.Model);
            Assert.Empty(dto.Unresolved);
        }

        [Fact]
        public async Task Vehicle_partial_lists_unresolved()
        {
            var ex = new FakeExtraction { Result = new VisionExtractionResult
            {
                IsVehicleDocument = true,
                Fields = new ExtractedVehicleFields { Brand = "KIA", Model = "???", Year = 2005 }
            }};
            var fc = new FakeFacets { Resolve = new VehicleResolveResult { Brand = "KIA", Model = null, Year = null } };
            var c = NewController(ex, fc);
            var res = await c.ExtractDocument(MakeFile("image/png", 3), CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<DocumentExtractResponse>(ok.Value);
            Assert.Contains("model", dto.Unresolved);
            Assert.Contains("year", dto.Unresolved);
        }
    }
}
```

- [ ] **Step 3: รัน test ให้ fail**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~MotorControllerExtractDocumentTests`
Expected: FAIL — controller ctor เดิมไม่มี dependency ใหม่ / ไม่มีเมธอด `ExtractDocument`

- [ ] **Step 4: แก้ controller — ctor + endpoint**

ใน `Controllers/MotorController.cs`:

(4a) เพิ่ม `using`:

```csharp
using System.IO;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Services.Document;
```

(4b) แก้ fields + ctor:

```csharp
        private readonly IMotorFacetService _facets;
        private readonly IDocumentExtractionService _extraction;
        private readonly IPdfRasterizer _pdf;
        private readonly ILogger<MotorController> _logger;

        public MotorController(
            IMotorFacetService facets,
            IDocumentExtractionService extraction,
            IPdfRasterizer pdf,
            ILogger<MotorController> logger)
        {
            _facets = facets;
            _extraction = extraction;
            _pdf = pdf;
            _logger = logger;
        }
```

(4c) เพิ่มค่าคงที่ + endpoint (วางก่อน `}` ปิดคลาส):

```csharp
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/jpg", "image/webp", "application/pdf" };
        private const long MaxBytes = 8L * 1024 * 1024;

        /// <summary>แนบรูป/PDF เอกสารรถ → vision classify+extract → resolve → prefill. ephemeral (ADR 0018)</summary>
        [HttpPost("ExtractDocument")]
        public async Task<IActionResult> ExtractDocument(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "ไม่พบไฟล์" });
            if (file.Length > MaxBytes)
                return BadRequest(new { message = "ไฟล์ใหญ่เกิน 8MB" });

            var ctype = (file.ContentType ?? "").Split(';')[0].Trim();
            if (!AllowedTypes.Contains(ctype))
                return BadRequest(new { message = "รองรับเฉพาะรูป JPG/PNG/WebP หรือ PDF" });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var mime = ctype;
            if (ctype == "application/pdf")
            {
                bytes = _pdf.RenderFirstPagePng(bytes);
                mime = "image/png";
            }

            VisionExtractionResult vision;
            try { vision = await _extraction.ExtractAsync(bytes, mime, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "vision extract failed (docType metadata only, no PII logged)");
                return StatusCode(502, new { message = "อ่านเอกสารไม่สำเร็จ ลองใหม่อีกครั้งค่ะ" });
            }

            if (!vision.IsVehicleDocument)
                return StatusCode(422, new { message = "ดูเหมือนไม่ใช่เอกสารรถ ลองแนบตารางกรมธรรม์หรือเล่มทะเบียนนะคะ" });

            var f = vision.Fields ?? new ExtractedVehicleFields();
            var r = await _facets.ResolveVehicleAsync(f.Brand, f.Model, f.Year);

            var unresolved = new List<string>();
            if (!r.BrandOk) unresolved.Add("brand");
            if (!r.ModelOk) unresolved.Add("model");
            if (!r.YearOk) unresolved.Add("year");

            return Ok(new DocumentExtractResponse
            {
                IsVehicleDocument = true,
                DocType = vision.DocType,
                Table = vision.TableMarkdown,
                Resolved = new ResolvedVehicle
                {
                    Brand = r.Brand, Model = r.Model, Year = r.Year, Cc = "",
                    BrandOk = r.BrandOk, ModelOk = r.ModelOk, YearOk = r.YearOk
                },
                Unresolved = unresolved,
                Message = unresolved.Count == 0
                    ? "อ่านข้อมูลรถได้ครบแล้วค่ะ"
                    : "อ่านได้บางส่วน ช่องที่เหลือเลือกเองได้เลยนะคะ"
            });
        }
```

- [ ] **Step 5: รัน test ให้ผ่าน**

Run: `cd insurechat-api && dotnet test --filter FullyQualifiedName~MotorControllerExtractDocumentTests`
Expected: PASS (5 tests)

- [ ] **Step 6: รัน test ทั้งชุด**

Run: `cd insurechat-api && dotnet test`
Expected: PASS ทั้งหมด (รวมเทสต์เดิม)

- [ ] **Step 7: Commit**

```bash
cd insurechat-api && git add -A && git commit -m "feat(motor): POST /Motor/ExtractDocument endpoint (guard->pdf->vision->resolve)"
```

---

## Task 7: Frontend service + client guard util

**Files:**
- Create: `insurechat-web/src/app/core/doc-upload.ts`
- Modify: `insurechat-web/src/app/core/motor-facet.ts`

- [ ] **Step 1: สร้าง client guard + downscale util**

สร้าง `insurechat-web/src/app/core/doc-upload.ts`:

```ts
export const ACCEPTED_DOC_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'application/pdf'];
export const MAX_DOC_BYTES = 8 * 1024 * 1024;

/** null = ผ่าน, string = ข้อความ error ภาษาไทย */
export function validateDocFile(file: File): string | null {
  if (!ACCEPTED_DOC_TYPES.includes(file.type)) return 'รองรับเฉพาะรูป JPG/PNG/WebP หรือ PDF';
  if (file.size > MAX_DOC_BYTES) return 'ไฟล์ใหญ่เกิน 8MB';
  return null;
}

/** ย่อรูป (ไม่ใช่ PDF) ให้ด้านยาวสุด ≤ max px เพื่อประหยัด token; คืนไฟล์เดิมถ้าไม่ต้องย่อ/ล้มเหลว */
export async function downscaleImage(file: File, max = 2000): Promise<Blob> {
  if (file.type === 'application/pdf') return file;
  try {
    const bmp = await createImageBitmap(file);
    if (Math.max(bmp.width, bmp.height) <= max) { bmp.close(); return file; }
    const scale = max / Math.max(bmp.width, bmp.height);
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bmp.width * scale);
    canvas.height = Math.round(bmp.height * scale);
    canvas.getContext('2d')!.drawImage(bmp, 0, 0, canvas.width, canvas.height);
    bmp.close();
    return await new Promise<Blob>((res) => canvas.toBlob((b) => res(b ?? file), 'image/jpeg', 0.9));
  } catch {
    return file;
  }
}
```

- [ ] **Step 2: เพิ่ม interfaces + extractDocument ใน MotorFacet**

ใน `core/motor-facet.ts` เพิ่ม interfaces (ใต้ `PackageSearchRequest`):

```ts
export interface ResolvedVehicle {
  brand: string | null; model: string | null; year: number | null; cc: string;
  brandOk: boolean; modelOk: boolean; yearOk: boolean;
}
export interface DocumentExtractResponse {
  isVehicleDocument: boolean; docType: string; table: string;
  resolved: ResolvedVehicle; unresolved: string[]; message: string;
}
```

เพิ่มเมธอดในคลาส `MotorFacet` (ใต้ `packageDetail`):

```ts
  /** แนบเอกสารรถ (Blob รูป/PDF) → vision สกัด → resolved ค่า facet (ADR 0018) */
  extractDocument(file: Blob, filename = 'document'): Observable<DocumentExtractResponse> {
    const form = new FormData();
    form.append('file', file, filename);
    return this.http.post<DocumentExtractResponse>(`${this.base}/ExtractDocument`, form);
  }
```

- [ ] **Step 3: ยืนยันคอมไพล์**

Run: `cd insurechat-web && npx ng build`
Expected: Build ผ่าน (ไม่มี TS error)

- [ ] **Step 4: Commit**

```bash
cd insurechat-web && git add -A && git commit -m "feat(web): doc-upload guard/downscale util + MotorFacet.extractDocument"
```

---

## Task 8: ปุ่มแนบเอกสารในหน้า quote → prefill

**Files:**
- Modify: `insurechat-web/src/app/features/quote/quote.ts`
- Modify: `insurechat-web/src/app/features/quote/quote.html:12`

- [ ] **Step 1: เพิ่ม import + state + handler ใน quote.ts**

(1a) เพิ่ม import ท้ายบล็อก import:

```ts
import { validateDocFile, downscaleImage } from '../../core/doc-upload';
import { DocumentExtractResponse } from '../../core/motor-facet';
```

(1b) เพิ่ม signals (ใกล้ `loading = signal(false);`):

```ts
  docLoading = signal(false);
  docNote = signal('');
```

(1c) เพิ่มเมธอด (วางก่อน `search()`):

```ts
  /** แนบเอกสารรถ → สกัด → prefill cascade (brand→model→year) ตามค่าที่ resolve ได้ */
  onDocSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';               // reset ให้เลือกไฟล์เดิมซ้ำได้
    if (!file) return;

    const err = validateDocFile(file);
    if (err) { this.error.set(err); return; }

    this.docLoading.set(true); this.error.set(''); this.docNote.set('');
    downscaleImage(file).then((blob) => {
      this.facet.extractDocument(blob, file.name).subscribe({
        next: (r) => { this.docLoading.set(false); this.applyExtraction(r); },
        error: (e) => { this.docLoading.set(false); this.error.set(e?.error?.message ?? 'อ่านเอกสารไม่สำเร็จ'); }
      });
    });
  }

  /** เติมฟอร์มจากผล resolve — โหลด cascade เป็นทอด ๆ, ช่องที่ resolve ไม่ได้เว้นไว้ */
  private applyExtraction(r: DocumentExtractResponse): void {
    const rv = r.resolved;
    const note = [rv.brand, rv.model, rv.year].filter(Boolean).join(' · ');
    this.docNote.set(note ? `อ่านจากเอกสาร: ${note}` : (r.message || ''));
    if (!rv.brand) return;

    this.brand = rv.brand;
    this.model = ''; this.year = '';
    this.models.set([]); this.years.set([]);
    this.facet.models(rv.brand).subscribe((m) => {
      this.models.set(m);
      if (rv.model && m.some((x) => x.value === rv.model)) {
        this.model = rv.model;
        this.facet.years(rv.brand!, rv.model!, '').subscribe((y) => {
          this.years.set(y);
          if (rv.year && y.some((x) => +x.value === rv.year)) this.year = String(rv.year);
        });
      }
    });
  }
```

- [ ] **Step 2: เพิ่มปุ่มใน quote.html**

ใน `features/quote/quote.html` แทรกหลังบรรทัด `<p class="sub">...</p>` (บรรทัด 12) ก่อน `@if (error())`:

```html
    <div class="doc-intake">
      <input #docInput type="file" accept="image/png,image/jpeg,image/webp,application/pdf" hidden (change)="onDocSelected($event)" />
      <button type="button" class="doc-btn" (click)="docInput.click()" [disabled]="docLoading()">
        {{ docLoading() ? 'กำลังอ่านเอกสาร…' : '📄 แนบเอกสารรถ (กรอกให้อัตโนมัติ)' }}
      </button>
      @if (docNote()) { <div class="doc-note">{{ docNote() }}</div> }
    </div>
```

- [ ] **Step 3: (สไตล์) เพิ่ม CSS ปุ่มใน quote.css**

ใน `features/quote/quote.css` เพิ่มท้ายไฟล์:

```css
.doc-intake { margin: 0 0 16px; }
.doc-btn {
  width: 100%; padding: 12px; border: 1px dashed var(--border, #cbd5e1);
  border-radius: 10px; background: transparent; color: var(--brand, #2563eb);
  font-weight: 600; cursor: pointer;
}
.doc-btn:disabled { opacity: .6; cursor: default; }
.doc-note { margin-top: 8px; font-size: 13px; color: var(--muted, #64748b); }
```

- [ ] **Step 4: ยืนยันคอมไพล์**

Run: `cd insurechat-web && npx ng build`
Expected: Build ผ่าน

- [ ] **Step 5: Manual verify**

รัน api+web (Task 10) → หน้า `/quote` กด "แนบเอกสารรถ" เลือกรูป `ตัวอย่างเอกสาร/ตารางกรมธรรม์ประกันภัยรถยนต์.png` → ฟอร์มต้องเติม KIA → K2700 → 2005 (หรือใกล้เคียง) + โชว์ "อ่านจากเอกสาร: …"

- [ ] **Step 6: Commit**

```bash
cd insurechat-web && git add -A && git commit -m "feat(quote): attach vehicle document -> auto prefill brand/model/year"
```

---

## Task 9: ปุ่มแนบเอกสารในแชต → โชว์ตาราง + seed scoping

**Files:**
- Modify: `insurechat-web/src/app/features/chatbot/chatbot.ts`
- Modify: `insurechat-web/src/app/features/chatbot/chatbot.html:81-88`

- [ ] **Step 1: เพิ่ม import + refactor seed ให้ reuse ได้**

(1a) เพิ่ม import:

```ts
import { validateDocFile, downscaleImage } from '../../core/doc-upload';
import { DocumentExtractResponse } from '../../core/motor-facet';
```

(1b) refactor `seedFromQuote` ให้เรียก helper กลาง `seedContext` (DRY). แทนที่เมธอด `seedFromQuote` เดิมด้วย:

```ts
  /** มาจากหน้า quote (5 ฟิลด์) → seed candidate set + เกริ่นเริ่มสโคป (ADR 0005) */
  private seedFromQuote(d: any): void {
    this.seedContext(d.brand, d.model, d.year, d.candidates ?? []);
  }

  /** seed context + greeting + opening suggestions จาก brand/model/year + candidate set (ใช้ทั้ง quote handoff และ document intake) */
  private seedContext(brand: string, model: string, year: number, cands: any[]): void {
    const n = cands.length;
    const insurers = new Set(cands.map((p: any) => p.insurerName)).size;
    const greeting =
      `เยี่ยมเลยค่ะ 😊 ดูให้รถ **${brand} ${model} ปี ${year}** แล้วนะคะ — ` +
      `เจอ **${n} แผน** จาก **${insurers} บริษัท** ที่เลือกได้เลยค่ะ\n\n` +
      `เดี๋ยวพิมพ์ช่วยกรองให้เหลือแผนที่ใช่ บอกงบเบี้ย/ทุนประกันที่ต้องการ หรือกดปุ่มด้านล่างได้เลยค่ะ`;

    this.conversationContext = {
      turns: [{ timestamp: new Date().toISOString(), role: 'assistant', text: greeting }],
      currentStep: 'llm',
      productType: 'motor',
      carBrand: brand, carModel: model, carYear: year,
      discoveredNeeds: [],
      proposals: [],
      candidates: cands
    };
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: greeting });
    this.scrollChatToBottom(true);
    this.isReadyToPropose.set(false);
    this.prevReady = false; this.prevProposalSig = '';
    this.fetchOpeningSuggestions();
  }
```

(1c) เพิ่ม handler เอกสาร (วางใกล้ `sendMessage`):

```ts
  /** แนบเอกสารรถในแชต → โชว์ตารางสกัด + (ถ้า resolve ครบ) ค้น candidates แล้วเข้า scoping */
  onChatDocSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.loading()) return;

    const err = validateDocFile(file);
    if (err) {
      this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + err });
      return;
    }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text: '📎 แนบเอกสารรถ: ' + file.name });
    this.loading.set(true);
    this.suggestions.set([]);
    this.statusText.set('กำลังอ่านเอกสาร');
    this.scrollChatToBottom(true);

    downscaleImage(file).then((blob) => {
      this.motor.extractDocument(blob, file.name).subscribe({
        next: (r) => { this.loading.set(false); this.statusText.set(''); this.applyDocToChat(r); },
        error: (e) => {
          this.loading.set(false); this.statusText.set('');
          this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + (e?.error?.message ?? 'อ่านเอกสารไม่สำเร็จ') });
          this.scrollChatToBottom();
        }
      });
    });
  }

  private applyDocToChat(r: DocumentExtractResponse): void {
    if (r.table) this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: r.table });
    const rv = r.resolved;
    if (rv.brand && rv.model && rv.year) {
      this.motor.search({ brand: rv.brand, model: rv.model, cc: '', year: rv.year, sort: 'prem' }).subscribe({
        next: (rows) => this.seedContext(rv.brand!, rv.model!, rv.year!, rows ?? []),
        error: () => this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: 'อ่านเอกสารได้แล้ว แต่ค้นแผนไม่สำเร็จ ลองที่หน้า "คำนวณเบี้ย" นะคะ' })
      });
    } else {
      this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: (r.message || '') + ' บอกยี่ห้อ/รุ่น/ปี เพิ่มได้เลยค่ะ' });
    }
    this.scrollChatToBottom();
  }
```

- [ ] **Step 2: เพิ่มปุ่ม 📎 ใน chatbot.html**

ใน `features/chatbot/chatbot.html` แก้ `<footer class="chat-input">` (บรรทัด 81-88) เพิ่มปุ่มแนบก่อน `<textarea>`:

```html
  <footer class="chat-input">
    <input #chatDoc type="file" accept="image/png,image/jpeg,image/webp,application/pdf" hidden (change)="onChatDocSelected($event)" />
    <button type="button" class="attach-btn" (click)="chatDoc.click()" [disabled]="loading()" title="แนบเอกสารรถ">📎</button>
    <textarea
      [(ngModel)]="userInput"
      (keydown)="onKeyPress($event)"
      placeholder="พิมพ์ข้อความ... (Enter เพื่อส่ง)"
      rows="1"></textarea>
    <button (click)="sendMessage()" [disabled]="loading() || !userInput.trim()">ส่ง</button>
  </footer>
```

- [ ] **Step 3: (สไตล์) เพิ่ม CSS ปุ่มแนบใน chatbot.css**

ใน `features/chatbot/chatbot.css` เพิ่มท้ายไฟล์:

```css
.attach-btn {
  flex: 0 0 auto; width: 40px; height: 40px; border: none; border-radius: 10px;
  background: transparent; font-size: 20px; cursor: pointer;
}
.attach-btn:disabled { opacity: .5; cursor: default; }
```

- [ ] **Step 4: ยืนยันคอมไพล์**

Run: `cd insurechat-web && npx ng build`
Expected: Build ผ่าน

- [ ] **Step 5: Manual verify**

รัน api+web → `/chat` กด 📎 แนบ `ตารางกรมธรรม์ประกันภัยรถยนต์.png` → ต้องเห็น (1) กล่องตารางสรุป (2) กล่อง greeting "เจอ N แผน…" + ปุ่มถามด่วน. แนบรูปที่ไม่ใช่เอกสารรถ → ต้องได้กล่อง "ดูเหมือนไม่ใช่เอกสารรถ…"

- [ ] **Step 6: Commit**

```bash
cd insurechat-web && git add -A && git commit -m "feat(chat): attach vehicle document -> show table + seed scoping flow"
```

---

## Task 10: Verify end-to-end (รันจริงกับรูปตัวอย่าง)

**Files:** ไม่มี (verification)

- [ ] **Step 1: ตั้ง env ให้ครบ**

ตรวจ `insurechat-api/.env` มี `OPENROUTER_API_KEY`, `KPN_DB_CONNECTION`, `EF_DB_CONNECTION` (ดู `.env.example`). VisionModel ใช้ default `google/gemini-2.5-flash` (ไม่ต้องตั้งเพิ่ม)

- [ ] **Step 2: รัน backend**

Run: `cd insurechat-api && dotnet run --project InsureChatBot.Api`
Expected: ฟังที่ `http://localhost:5228` (หรือพอร์ตใน launchSettings)

- [ ] **Step 3: รัน frontend**

Run: `cd insurechat-web && npm start`
Expected: Angular dev server ที่ `http://localhost:4200`

- [ ] **Step 4: ทดสอบ 3 เคส**

1. `/quote` แนบ `ตัวอย่างเอกสาร/ตารางกรมธรรม์ประกันภัยรถยนต์.png` → ฟอร์มเติมยี่ห้อ/รุ่น/ปี
2. `/chat` แนบ `ตัวอย่างเอกสาร/รายการจดทะเบียน.png` → เห็นตาราง + seed แผน
3. แนบรูปที่ไม่ใช่เอกสารรถ (รูปอะไรก็ได้) → ถูกปฏิเสธด้วยข้อความสุภาพ (422)

- [ ] **Step 5: ตรวจ privacy**

ยืนยันว่าไม่มีไฟล์รูป/เอกสารถูกเขียนลง disk หรือ DB, log ไม่มี base64/PII (มีแค่ metadata)

---

## Task 11 (Optional): ทำให้ admin แก้ Vision model ได้ผ่าน /admin

> ทำต่อได้ภายหลัง — ฟีเจอร์หลักทำงานด้วย default `google/gemini-2.5-flash` โดยไม่ต้องมี Task นี้. Task นี้เพิ่มการ override ผ่าน UI (persist ลง DB).

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Data/Entities/AppSettingsRow.cs` — เพิ่ม `LlmVisionModel`
- Migration ใหม่ (EF Core)
- Modify: `SqlAppSettingsStore.cs` — `ToPoco`/`WriteRow` map `LlmVisionModel`
- Modify: `Models/Admin/AdminDtos.cs` — `LlmConfigDto.VisionModel` + `LlmUpdateDto.VisionModel`
- Modify: `Controllers/AdminController.cs` — map GET/PUT `/Admin/Llm` (mirror `Model`)
- Modify: `insurechat-web/src/app/features/admin/admin.ts` — `LlmResp.visionModel` + field + save
- Modify: `insurechat-web/src/app/features/admin/admin.html` — ช่องกรอกในแท็บ LLM

- [ ] **Step 1: เพิ่มคอลัมน์ entity**

ใน `Data/Entities/AppSettingsRow.cs` หลัง `public string LlmModel { get; set; }` เพิ่ม:

```csharp
        public string LlmVisionModel { get; set; }
```

- [ ] **Step 2: สร้าง migration**

Run: `cd insurechat-api && dotnet ef migrations add AddLlmVisionModel --project InsureChatBot.Api`
Expected: migration ไฟล์ใหม่ที่ AddColumn `LlmVisionModel`

- [ ] **Step 3: map ใน SqlAppSettingsStore**

ใน `ToPoco` (บล็อก `Llm = new LlmOverrides { ... }`) เพิ่ม `VisionModel = r.LlmVisionModel,`
ใน `WriteRow` เพิ่ม `r.LlmVisionModel = s.Llm?.VisionModel;`

- [ ] **Step 4: เพิ่มใน DTOs**

ใน `Models/Admin/AdminDtos.cs`:
- `LlmConfigDto` เพิ่ม `public string VisionModel { get; set; }`
- `LlmUpdateDto` เพิ่ม `public string VisionModel { get; set; }`

- [ ] **Step 5: map ใน AdminController (GET/PUT Llm)**

หาเมธอด GET `/Admin/Llm`: เพิ่ม `VisionModel = eff.VisionModel` ใน DTO ที่คืน
หาเมธอด PUT: เพิ่มการเซ็ต `s.Llm.VisionModel = dto.VisionModel;` ในบล็อก `_store.Update(...)` (mirror `Model`)

> ตรวจโค้ดจริงของ `AdminController` ก่อน — ทำตามแพตเทิร์นของ `Model` ที่มีอยู่ทุกจุด

- [ ] **Step 6: frontend admin field**

ใน `admin.ts`: `LlmResp` เพิ่ม `visionModel: string;`; เพิ่มฟิลด์ `llmVisionModel = '';`; ใน `fillLlm` เพิ่ม `this.llmVisionModel = r.visionModel;`; ใน `saveLlm` body เพิ่ม `visionModel: this.llmVisionModel`
ใน `admin.html` แท็บ LLM เพิ่มช่องกรอก (mirror ช่อง model) ผูก `[(ngModel)]="llmVisionModel"`

- [ ] **Step 7: build + test**

Run: `cd insurechat-api && dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj` แล้ว `cd insurechat-web && npx ng build`
Expected: ผ่านทั้งคู่

- [ ] **Step 8: Commit (2 repo)**

```bash
cd insurechat-api && git add -A && git commit -m "feat(admin): make VisionModel editable + persist (migration)"
cd ../insurechat-web && git add -A && git commit -m "feat(admin): Vision model input in LLM settings tab"
```

---

## Notes for the implementer

- **Multi-repo commits:** โค้ด backend commit ที่ `insurechat-api`, frontend ที่ `insurechat-web`. แผน/ADR/CONTEXT อยู่ `InsureChat` (commit แล้ว)
- **Vision cost:** ทุกการอัปโหลด = 1 vision call (gemini-2.5-flash) — downscale ช่วยลด token
- **Grounding:** endpoint คืนเฉพาะ ยี่ห้อ/รุ่น/ปี ที่ resolve + ตารางที่ผู้ใช้เห็น — ไม่เอาฟิลด์ PII ไปใช้อัตโนมัติ (ADR 0018 §Out of scope)
- **PDFtoImage overload:** ปรับ `Conversion.ToImage(...)` ตามเวอร์ชันที่ติดตั้ง (บาง minor เปลี่ยนชื่อ param) — verify ด้วย Task 4 Step 6
