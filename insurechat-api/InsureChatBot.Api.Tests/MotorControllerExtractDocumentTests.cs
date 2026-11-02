using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Controllers;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class MotorControllerExtractDocumentTests
    {
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

        private static MotorController NewController(FakeDocumentExtractionService ex, FakeMotorFacetService fc)
            => new MotorController(fc, ex, new FakePdfRasterizer(), NullLogger<MotorController>.Instance);

        [Fact]
        public async Task Rejects_unsupported_type_400()
        {
            var c = NewController(new FakeDocumentExtractionService(), new FakeMotorFacetService());
            var res = await c.ExtractDocument(MakeFile("text/plain", 3), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
        }

        [Fact]
        public async Task Rejects_oversize_400()
        {
            var c = NewController(new FakeDocumentExtractionService(), new FakeMotorFacetService());
            var res = await c.ExtractDocument(MakeFile("image/png", 9L * 1024 * 1024), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
        }

        [Fact]
        public async Task Non_vehicle_returns_422()
        {
            var ex = new FakeDocumentExtractionService { Result = new VisionExtractionResult { IsVehicleDocument = false, DocType = "other" } };
            var c = NewController(ex, new FakeMotorFacetService());
            var res = await c.ExtractDocument(MakeFile("image/png", 3), CancellationToken.None);
            var obj = Assert.IsType<ObjectResult>(res);
            Assert.Equal(422, obj.StatusCode);
        }

        [Fact]
        public async Task Vehicle_resolved_returns_200_no_unresolved()
        {
            var ex = new FakeDocumentExtractionService { Result = new VisionExtractionResult
            {
                IsVehicleDocument = true, DocType = "policy_schedule",
                Fields = new ExtractedVehicleFields { Brand = "KIA", Model = "K 2700 M", Year = 2005 },
                TableMarkdown = "| a | b |"
            }};
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult { Brand = "KIA", Model = "K2700", Year = 2005 } };
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
            var ex = new FakeDocumentExtractionService { Result = new VisionExtractionResult
            {
                IsVehicleDocument = true,
                Fields = new ExtractedVehicleFields { Brand = "KIA", Model = "???", Year = 2005 }
            }};
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult { Brand = "KIA", Model = null, Year = null } };
            var c = NewController(ex, fc);
            var res = await c.ExtractDocument(MakeFile("image/png", 3), CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<DocumentExtractResponse>(ok.Value);
            Assert.Contains("model", dto.Unresolved);
            Assert.Contains("year", dto.Unresolved);
        }

        [Fact]
        public async Task Corrupt_pdf_returns_400()
        {
            var ctrl = new MotorController(new FakeMotorFacetService(), new FakeDocumentExtractionService(), new ThrowingPdfRasterizer(), NullLogger<MotorController>.Instance);
            var res = await ctrl.ExtractDocument(MakeFile("application/pdf", 3), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
        }
    }
}
