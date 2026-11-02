using System.IO;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Document;
using InsureChatBot.Api.Services.Motor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    /// <summary>facet (dropdown cascade) + ตารางเปรียบเทียบแพคเกจ — ข้อมูลสดจาก KPN DB (ADR 0004)</summary>
    // public — quote page เปิดให้ทุกคน (ADR 0011)
    [AllowAnonymous]
    [Route("api/Motor")]
    [ApiController]
    public class MotorController : ControllerBase
    {
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

        private async Task<IActionResult> Guarded(Func<Task<object>> run)
        {
            try { return Ok(await run()); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Motor facet query failed");
                return StatusCode(500, new { message = "ดึงข้อมูลแพคเกจไม่สำเร็จ", detail = ex.Message });
            }
        }

        [HttpGet("Brands")]
        public Task<IActionResult> Brands() => Guarded(async () => await _facets.GetBrandsAsync());

        [HttpGet("Models")]
        public Task<IActionResult> Models([FromQuery] string brand) => Guarded(async () => await _facets.GetModelsAsync(brand));

        [HttpGet("Cc")]
        public Task<IActionResult> Cc([FromQuery] string brand, [FromQuery] string model) => Guarded(async () => await _facets.GetCcAsync(brand, model));

        [HttpGet("Years")]
        public Task<IActionResult> Years([FromQuery] string brand, [FromQuery] string model, [FromQuery] string cc) => Guarded(async () => await _facets.GetYearsAsync(brand, model, cc));

        [HttpGet("PolTypes")]
        public Task<IActionResult> PolTypes([FromQuery] string brand, [FromQuery] string model, [FromQuery] string cc, [FromQuery] int year) => Guarded(async () => await _facets.GetPolTypesAsync(brand, model, cc, year));

        [HttpGet("SumInsures")]
        public Task<IActionResult> SumInsures([FromQuery] string brand, [FromQuery] string model, [FromQuery] string cc, [FromQuery] int year, [FromQuery] int? polTypeId) => Guarded(async () => await _facets.GetSumInsuresAsync(brand, model, cc, year, polTypeId));

        [HttpGet("FireSumInsures")]
        public Task<IActionResult> FireSumInsures([FromQuery] string brand, [FromQuery] string model, [FromQuery] string cc, [FromQuery] int year, [FromQuery] int? polTypeId) => Guarded(async () => await _facets.GetFireSumInsuresAsync(brand, model, cc, year, polTypeId));

        [HttpGet("FloodSumInsures")]
        public Task<IActionResult> FloodSumInsures([FromQuery] string brand, [FromQuery] string model, [FromQuery] string cc, [FromQuery] int year, [FromQuery] int? polTypeId) => Guarded(async () => await _facets.GetFloodSumInsuresAsync(brand, model, cc, year, polTypeId));

        [HttpGet("Provinces")]
        public Task<IActionResult> Provinces() => Guarded(async () => await _facets.GetProvincesAsync());

        [HttpPost("Search")]
        public Task<IActionResult> Search([FromBody] PackageSearchRequest req) => Guarded(async () => await _facets.SearchPackagesAsync(req ?? new PackageSearchRequest()));

        [HttpGet("PackageDetail")]
        public Task<IActionResult> PackageDetail([FromQuery] int packageId) => Guarded(async () => await _facets.GetPackageDetailAsync(packageId));

        /// <summary>resolve ค่าที่ผู้ใช้พิมพ์เอง → ค่า facet canonical (ใช้โดย intake gate ในแชต). ดู ADR 0019</summary>
        [HttpGet("ResolveVehicle")]
        public Task<IActionResult> ResolveVehicle([FromQuery] string brand, [FromQuery] string model, [FromQuery] int? year)
            => Guarded(async () => await _facets.ResolveVehicleAsync(brand, model, year));

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
                try { bytes = _pdf.RenderFirstPagePng(bytes); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PDF rasterize failed (no PII logged)");
                    return BadRequest(new { message = "ไฟล์ PDF เสียหายหรืออ่านไม่ได้ ลองใหม่หรือส่งเป็นรูปภาพนะคะ" });
                }
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
    }
}
