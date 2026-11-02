using System.Diagnostics;
using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Implementations
{
    /// <summary>
    /// ค้นแพคเกจรถโดยเรียก subprocess ไปยัง tools/motor/query.py (ข้อมูล 330k ใน raw/).
    /// path มาจาก config (MotorTool) ไม่ hardcode แล้ว — ดู appsettings.json.
    /// </summary>
    public class MotorQueryService : IMotorQueryService
    {
        private readonly string _pythonExePath;
        private readonly string _pythonScriptPath;
        private readonly string _workingDirectory;

        public MotorQueryService(IOptions<MotorToolSettings> options)
        {
            var s = options.Value;
            _pythonExePath = string.IsNullOrWhiteSpace(s.PythonExe) ? "python" : s.PythonExe;
            _workingDirectory = s.WorkingDirectory;
            _pythonScriptPath = string.IsNullOrWhiteSpace(s.ScriptPath)
                ? Path.Combine(_workingDirectory ?? "", "query.py")
                : s.ScriptPath;
        }

        public async Task<List<MotorPackageDto>> SearchPackagesAsync(
            string brand,
            string model,
            int year,
            string insuranceClass,
            decimal sumInsure,
            int limit = 10)
        {
            // contractor/prod ที่ไม่มี vault tools → ไม่มี query.py: คืน list ว่างแทน throw (ADR 0017)
            if (string.IsNullOrWhiteSpace(_pythonScriptPath) || !File.Exists(_pythonScriptPath))
                return new List<MotorPackageDto>();
            string klass = (insuranceClass ?? "").Trim();

            // Progressive relaxation: ข้อมูล 330k เก็บปีจดทะเบียนจริงของแต่ละรถ การกรองปี/ทุน
            // แบบเป๊ะมักได้ 0 → ลองเข้มก่อน แล้วค่อย ๆ ผ่อนจนเจอ
            var attempts = new List<List<string>>
            {
                BuildArgs(brand, model, year, klass, sumInsure, limit), // full
                BuildArgs(brand, model, year, klass, 0m,        limit), // drop sum cap
                BuildArgs(brand, model, 0,   klass, 0m,        limit),  // drop year
                BuildArgs(brand, null,  0,   klass, 0m,        limit),  // drop model
                BuildArgs(brand, null,  0,   null,  0m,        limit),  // brand only
            };

            foreach (var args in attempts)
            {
                var packages = await RunQueryAsync(args, klass);
                if (packages.Count > 0)
                    return packages;
            }

            return new List<MotorPackageDto>();
        }

        private List<string> BuildArgs(string brand, string model, int year, string klass, decimal sumMax, int limit)
        {
            var args = new List<string> { _pythonScriptPath };
            if (!string.IsNullOrWhiteSpace(brand)) { args.Add("--brand"); args.Add(brand); }
            if (!string.IsNullOrWhiteSpace(model)) { args.Add("--model"); args.Add(model); }
            if (year > 0) { args.Add("--year"); args.Add(year.ToString()); }
            if (!string.IsNullOrWhiteSpace(klass)) { args.Add("--class"); args.Add(klass); }
            if (sumMax > 0) { args.Add("--sum-max"); args.Add(sumMax.ToString("F0")); }
            args.Add("--sort"); args.Add("prem");   // cheapest first
            args.Add("--limit"); args.Add(limit.ToString());
            args.Add("--json");
            return args;
        }

        private async Task<List<MotorPackageDto>> RunQueryAsync(List<string> args, string requestedClass)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonExePath,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            // ส่ง argument ทีละตัว (กัน path/ค่ามีช่องว่าง) แทนการ join string
            foreach (var a in args) processInfo.ArgumentList.Add(a);

            using var process = Process.Start(processInfo);
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Motor query failed: {error}");

            return ParseQueryOutput(output, requestedClass);
        }

        private List<MotorPackageDto> ParseQueryOutput(string jsonOutput, string requestedClass)
        {
            var packages = new List<MotorPackageDto>();
            if (string.IsNullOrEmpty(jsonOutput))
                return packages;

            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // query.py คืน { "matches": N, "results": [ ... ] }
            if (!root.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return packages;

            foreach (var item in results.EnumerateArray())
            {
                decimal premNet = GetJsonDecimal(item, "premNet");
                decimal premStm = GetJsonDecimal(item, "premStm"); // เบี้ยรวมอากร/แสตมป์
                string insurer = GetJsonString(item, "insureID");

                packages.Add(new MotorPackageDto
                {
                    InsurerId = insurer,
                    InsurerCode = insurer,
                    InsurerName = insurer,
                    CarBrand = GetJsonString(item, "carBrandName"),
                    CarModel = GetJsonString(item, "carModelName"),
                    Year = GetJsonInt(item, "year"),
                    // output ไม่มี field class — ใช้ชั้นที่ผู้ใช้เลือก (กรองด้วยชั้นนี้แล้ว)
                    InsuranceClass = requestedClass,
                    SumInsure = GetJsonDecimal(item, "sumInsureMax"),
                    Premium = premNet,
                    Tax = 0m,
                    Stamp = premStm > premNet ? premStm - premNet : 0m,
                    Total = premStm > 0 ? premStm : premNet
                });
            }

            return packages;
        }

        private string GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? "";
            return "";
        }

        private int GetJsonInt(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
            return 0;
        }

        private decimal GetJsonDecimal(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetDecimal();
            return 0m;
        }
    }
}
