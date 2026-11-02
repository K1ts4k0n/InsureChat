using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Interfaces
{
    public interface IMotorQueryService
    {
        Task<List<MotorPackageDto>> SearchPackagesAsync(
            string brand,
            string model,
            int year,
            string insuranceClass,
            decimal sumInsure,
            int limit = 10);
    }
}
