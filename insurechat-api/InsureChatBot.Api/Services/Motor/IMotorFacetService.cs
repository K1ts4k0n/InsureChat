using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Motor
{
    /// <summary>facet (dropdown cascade) + ตารางแพคเกจ — อ่านจาก KPN SQL DB สด (ADR 0004), mirror CheckPrm ของ KPN</summary>
    public interface IMotorFacetService
    {
        Task<List<FacetItem>> GetBrandsAsync();
        Task<List<FacetItem>> GetModelsAsync(string brand);
        Task<List<FacetItem>> GetCcAsync(string brand, string model);
        Task<List<FacetItem>> GetYearsAsync(string brand, string model, string cc);
        Task<List<PolTypeItem>> GetPolTypesAsync(string brand, string model, string cc, int year);
        Task<List<FacetItem>> GetSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId);
        Task<List<FacetItem>> GetFireSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId);
        Task<List<FacetItem>> GetFloodSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId);
        Task<List<FacetItem>> GetProvincesAsync();
        Task<List<PackageRow>> SearchPackagesAsync(PackageSearchRequest req);
        Task<MotorPackageDetail> GetPackageDetailAsync(int packageId);   // ความคุ้มครองเต็มของแพคเกจ (modal)

        /// <summary>resolve ค่าที่ vision อ่านได้ → ค่า facet canonical (cascade brand→model→year). ADR 0018</summary>
        Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year);
    }
}
