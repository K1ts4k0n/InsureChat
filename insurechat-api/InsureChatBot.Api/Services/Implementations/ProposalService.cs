using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Implementations
{
    /// <summary>
    /// ประกอบ proposal JSON ตามสคีมา T Broker แล้วส่ง.
    /// SubmitMode = "mock" (default) → ไม่ยิงจริง คืน MOCK transactionId + log JSON.
    /// SubmitMode = "live" → POST ไป TBroker:ApiUrl. (ดู ADR 0001)
    /// </summary>
    public class ProposalService : IProposalService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProposalService> _logger;
        private readonly TBrokerSettings _settings;

        public ProposalService(
            IHttpClientFactory httpClientFactory,
            IOptions<TBrokerSettings> options,
            ILogger<ProposalService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = options.Value;
            _logger = logger;
        }

        private bool IsLive =>
            string.Equals(_settings.SubmitMode, "live", StringComparison.OrdinalIgnoreCase);

        public async Task<ProposalSubmissionResponse> SubmitMotorProposalAsync(
            string userCode,
            string userName,
            string userEmail,
            MotorProposalData proposalData)
        {
            try
            {
                var proposalJson = BuildProposalJson(userCode, userName, userEmail, proposalData);
                var serialized = JsonSerializer.Serialize(proposalJson, new JsonSerializerOptions { WriteIndented = false });

                if (!IsLive)
                {
                    // MOCK: ไม่ยิงปลายทางตัวแทนจริง
                    _logger.LogInformation("[ProposalService:MOCK] proposal JSON = {Json}", serialized);
                    return new ProposalSubmissionResponse
                    {
                        Success = true,
                        Message = "ข้อเสนอถูกบันทึก (โหมดทดสอบ — ยังไม่ส่งให้ตัวแทนจริง)",
                        TransactionId = "MOCK-" + Guid.NewGuid().ToString("N")[..12],
                        PolicyNumber = ""
                    };
                }

                if (string.IsNullOrWhiteSpace(_settings.ApiUrl))
                {
                    return new ProposalSubmissionResponse
                    {
                        Success = false,
                        Message = "โหมด live แต่ยังไม่ได้ตั้งค่าปลายทางตัวแทน"
                    };
                }

                var httpClient = _httpClientFactory.CreateClient();
                var jsonContent = new StringContent(serialized, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(_settings.ApiUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new ProposalSubmissionResponse
                    {
                        Success = false,
                        Message = $"ระบบตัวแทนตอบกลับ {response.StatusCode}",
                        ErrorDetails = errorContent
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                return new ProposalSubmissionResponse
                {
                    Success = true,
                    Message = "ข้อเสนอถูกส่งเรียบร้อย",
                    TransactionId = ExtractJsonString(responseObj, "transactionId") ?? Guid.NewGuid().ToString(),
                    PolicyNumber = ExtractJsonString(responseObj, "policyNumber") ?? "",
                };
            }
            catch (Exception ex)
            {
                return new ProposalSubmissionResponse
                {
                    Success = false,
                    Message = $"Error submitting proposal: {ex.Message}",
                    ErrorDetails = ex.StackTrace
                };
            }
        }

        private object BuildProposalJson(
            string userCode,
            string userName,
            string userEmail,
            MotorProposalData data)
        {
            // carFeetList (รายละเอียดรถ)
            var carFeetList = new[] {
                new {
                    rowID = 0,
                    motCode = "",
                    carBrand = data.CarBrand,
                    carModel = data.CarModel,
                    licno = data.LicensePlate ?? "",
                    carPvn = 0,
                    modelYearEn = data.Year.ToString(),
                    modelYearTh = "",
                    modelYearCount = "",
                    chsNo = "",
                    egnNo = "",
                    numCc = 0,
                    numGvw = 0,
                    numSea = 0,
                    fuel = "",
                    gasNo = "",
                    carColor = "",
                    driverSpecify = "N",
                    driverName1 = "",
                    driverAge1 = 0,
                    driverBirthDay1 = "",
                    driverLiceNo1 = "",
                    driverWork1 = "",
                    driverName2 = "",
                    driverAge2 = 0,
                    driverBirthDay2 = "",
                    driverLiceNo2 = "",
                    driverWork2 = "",
                    inspectStation = "",
                    inspectStatus = "",
                    carpirce = 0,
                    driverExpireDate1 = "",
                    driverExpireDate2 = "",
                    driverIssueDate1 = "",
                    driverIssueDate2 = "",
                    carUseName = "",
                    carRegisDate = "",
                    carTaxLastDate = "",
                    polCarType = "",
                    deductDiscPerc = 0,
                    deductDiscBaht = 0,
                    fleetDiscPerc = 0,
                    fleetDiscBaht = 0,
                    ageDiscPerc = 0,
                    ageDiscBaht = 0,
                    ncbdiscPerc = 0,
                    ncbdiscBaht = 0,
                    otherDiscPerc = 0,
                    otherDiscBaht = 0,
                    surchAmtPerc = 0,
                    surchAmtBaht = 0,
                    bodilyDrivCount = 0,
                    bodilyDrivCover = 0,
                    bodilyPassCount = 0,
                    bodilyPassCover = 0,
                    tempDrivPers = 0,
                    tempDrivBaht = 0,
                    tempPassPers = 0,
                    tempPassBaht = 0,
                    medicalPers = 0,
                    medicalBaht = 0,
                    medicalPassPers = 0,
                    medicalPassBaht = 0,
                    bailBondIns = 0,
                    renovType = "",
                    sumInsure = data.SumInsure,
                    ownDamage = 0,
                    ownAmtDeduc = 0,
                    fireTheft = data.SumInsure,
                    thirdBodilyPers = 0,
                    thirdBodilyAccid = 0,
                    thirdCtpl = 0,
                    thirdAmtDeduc = 0,
                    beneficiary = "",
                    flood = 0,
                    terrorism = 0,
                    accessory = 0
                }
            };

            // quotationPremCoverList (รายละเอียดเบี้ย)
            // ใช้ Dictionary เพราะสคีมามีทั้ง "rowID" และ "rowId" (ต่างแค่ case) ซึ่ง JSON
            // ถือเป็นคนละคีย์ แต่ anonymous type + System.Text.Json จะ clash — dict คงคีย์เป๊ะ
            var quotationPremCoverList = new[] {
                new Dictionary<string, object> {
                    ["rowID"] = 0,
                    ["quotationId"] = 0,
                    ["coverCode"] = "",
                    ["coverName"] = "",
                    ["coverType"] = "",
                    ["coverLimit"] = 0,
                    ["coverPremium"] = 0,
                    ["coverTax"] = 0,
                    ["coverStamp"] = 0,
                    ["coverTotal"] = 0,
                    ["coverStatus"] = "",
                    ["coverRemark"] = "",
                    ["rowId"] = 1,
                    ["fireTheft"] = (int)data.SumInsure,
                    ["sumInsure"] = (int)data.SumInsure,
                    ["ownDamage"] = 0,
                    ["motPremNet"] = (int)data.Premium,
                    ["motPremTax"] = (int)data.Tax,
                    ["tempDrivPers"] = 0,
                    ["tempDrivBaht"] = 0,
                    ["tempPassPers"] = 0,
                    ["tempPassBaht"] = 0,
                    ["medicalPers"] = 0,
                    ["medicalBaht"] = 0,
                    ["medicalPassPers"] = 0,
                    ["medicalPassBaht"] = 0,
                    ["bailBondIns"] = (int)data.SumInsure,
                    ["motpolquo"] = "",
                    ["motPremStm"] = (int)data.Stamp,
                    ["motPremWtax"] = (int)data.Total,
                    ["motDiscStmPerc"] = 0,
                    ["motDiscStmBath"] = 0,
                    ["compPremNet"] = 0,
                    ["compPremStm"] = 0,
                    ["compPremWtax"] = 0,
                    ["compPremTax"] = 0,
                    ["compDiscStmPerc"] = 0,
                    ["compDiscStmBath"] = 0,
                    ["compAgentRatePerc"] = 0,
                    ["compAgentRateBath"] = 0,
                    ["compAgentExcRatePerc"] = 0,
                    ["compAgentExcRateBath"] = 0,
                    ["moAgentRatePerc"] = 0,
                    ["moAgentRateBath"] = 0,
                    ["moAgentExcRatePerc"] = 0,
                    ["moAgentExcRateBath"] = 0,
                    ["accessory"] = 0,
                    ["terrorism"] = 0,
                    ["thirdBodilyPers"] = 0,
                    ["thirdBodilyAccid"] = 0,
                    ["thirdCtpl"] = 0,
                    ["thirdAmtDeduc"] = 0,
                    ["beneficiary"] = "",
                    ["bodilyDrivCount"] = 0,
                    ["bodilyDrivCover"] = 0,
                    ["bodilyPassCount"] = 0,
                    ["bodilyPassCover"] = 0,
                    ["insureid"] = data.InsurerId,
                    ["insureCode"] = data.InsurerCode,
                    ["insureName"] = data.InsurerName,
                    ["polTypeid"] = 2,
                    ["renovType"] = "",
                    ["renovTypeName"] = "",
                    ["ownAmtDeduc"] = 0,
                    ["flood"] = 0,
                    ["accessorytext"] = "",
                    ["motPremDty"] = 0,
                    ["moBrokerageRateBath"] = 0,
                    ["moBrokerageOvrateBath"] = 0,
                    ["moTaxCommBath"] = 0,
                    ["moTaxCommPerc"] = 0,
                    ["moBrokerageRatePerc"] = 0,
                    ["moBrokerageOvratePerc"] = 0,
                    ["compPremDty"] = 0,
                    ["compTaxCommBath"] = 0,
                    ["compBrokerageRateBath"] = 0,
                    ["compBrokerageOvrateBath"] = 0,
                    ["compTaxCommPerc"] = 0,
                    ["compBrokerageRatePerc"] = 0,
                    ["compBrokerageOvratePerc"] = 0,
                    ["brokerCode"] = ""
                }
            };

            var firstName = string.IsNullOrWhiteSpace(userName) ? "" : userName.Split(' ')[0];
            var lastName = !string.IsNullOrWhiteSpace(userName) && userName.Contains(' ')
                ? userName.Substring(userName.IndexOf(' ') + 1) : "";

            return new
            {
                carFeetList,
                quotationPremCoverList,
                policyType = "Q",
                polStatus = "NEW",
                polYearCount = 1,
                custCode = userCode,
                custName = userName,
                firstName,
                lastName,
                custHouseNo = "",
                custBuilding = "",
                custVillageNo = "",
                custVillageName = "",
                custLane = "",
                custRoad = "",
                tambon = "",
                amphur = "",
                provin = "",
                zipCode = "",
                phone = data.Phone ?? "",
                mobile = data.Phone ?? "",
                email = userEmail,
                idcard = data.IdCard ?? "",
                agentCode = "",
                polTypeCode = "2",
                persType = "1",
                polFlag = "Q",
                carBrand = data.CarBrand,
                carModel = data.CarModel,
                carPvn = 0,
                modelYearEn = data.Year.ToString(),
                numCc = 0,
                sumInsure = data.SumInsure,
                fireTheft = data.SumInsure,
                notifDate = DateTime.Now.ToString("dd/MM/yyyy"),
                followStatus = "",
                driverSpecify = "N",
                custGroupCode = "0",
                followList = new object[] { }
            };
        }

        private string ExtractJsonString(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
            catch { }
            return null;
        }
    }
}
