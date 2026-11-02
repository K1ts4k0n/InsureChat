using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Document;
using InsureChatBot.Api.Services.Motor;

namespace InsureChatBot.Api.Tests.Fakes
{
    /// <summary>fake vision extraction — คืนผลที่ตั้งไว้</summary>
    public class FakeDocumentExtractionService : IDocumentExtractionService
    {
        public VisionExtractionResult Result = new();
        public Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    /// <summary>fake PDF rasterizer — คืน byte เดิม</summary>
    public class FakePdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf) => pdf;
    }

    /// <summary>fake PDF rasterizer ที่โยน exception (ทดสอบ PDF เสีย)</summary>
    public class ThrowingPdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf) => throw new InvalidOperationException("corrupt pdf");
    }

    /// <summary>fake facet service — ใช้เฉพาะ ResolveVehicleAsync, เมธอดอื่นไม่ถูกเรียกในเทสต์</summary>
    public class FakeMotorFacetService : IMotorFacetService
    {
        public VehicleResolveResult Resolve = new();

        public Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year)
            => Task.FromResult(Resolve);

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
}
