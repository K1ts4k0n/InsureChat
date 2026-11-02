using System.Threading.Tasks;
using InsureChatBot.Api.Controllers;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class MotorControllerResolveVehicleTests
    {
        private static MotorController NewController(FakeMotorFacetService fc)
            => new MotorController(fc, new FakeDocumentExtractionService(), new FakePdfRasterizer(),
                                   NullLogger<MotorController>.Instance);

        [Fact]
        public async Task Returns_canonical_values_when_all_resolve()
        {
            var fc = new FakeMotorFacetService
            {
                Resolve = new VehicleResolveResult { Brand = "HONDA", Model = "CIVIC 1.5", Year = 2020 }
            };
            var res = await NewController(fc).ResolveVehicle("honda", "civic 1.5", 2020);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.Equal("HONDA", dto.Brand);
            Assert.Equal("CIVIC 1.5", dto.Model);
            Assert.Equal(2020, dto.Year);
            Assert.True(dto.BrandOk);
            Assert.True(dto.ModelOk);
            Assert.True(dto.YearOk);
        }

        [Fact]
        public async Task Returns_partial_when_only_brand_resolves()
        {
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult { Brand = "KIA" } };
            var res = await NewController(fc).ResolveVehicle("kia", "K2700", 2005);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.True(dto.BrandOk);
            Assert.False(dto.ModelOk);
            Assert.False(dto.YearOk);
            Assert.Null(dto.Model);
        }

        [Fact]
        public async Task Returns_all_null_when_nothing_resolves()
        {
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult() };
            var res = await NewController(fc).ResolveVehicle("zzz", null, null);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.False(dto.BrandOk);
            Assert.Null(dto.Brand);
        }
    }
}
