using Dapper;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Motor;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace InsureChatBot.Api.Services.Motor
{
    /// <summary>
    /// อ่าน facet + แพคเกจสดจาก KPN SQL DB (KPNNew) ด้วย Dapper (read-only).
    /// หากไม่ได้ตั้ง ConnectionString จะสลับไปอ่านจาก SQLite (insurechat.db) ที่เก็บ 330,408 แพ็กเกจจริง.
    /// </summary>
    public class MotorFacetSqlService : IMotorFacetService
    {
        private readonly KpnDbSettings _db;
        private readonly string _sqlitePath;

        public MotorFacetSqlService(IOptions<KpnDbSettings> opt, IWebHostEnvironment env)
        {
            _db = opt.Value;
            _sqlitePath = Path.Combine(env.ContentRootPath, "App_Data", "insurechat.db");
        }

        private bool UseSqlite => string.IsNullOrWhiteSpace(_db?.ConnectionString);
        private DbConnection Conn() => UseSqlite
            ? new SqliteConnection($"Data Source={_sqlitePath}")
            : new SqlConnection(_db.ConnectionString);

        private static List<FacetItem> ToFacets(IEnumerable<string> vals) =>
            vals.Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => new FacetItem { Value = v, Label = v }).ToList();

        // คืนรายการเงินที่ unique + เรียงเลข (ใช้กับ fire/flood ที่เก็บเป็น string)
        private static List<FacetItem> ToMoneyFacets(IEnumerable<string> raw)
        {
            var nums = new SortedSet<decimal>();
            foreach (var s in raw)
                if (decimal.TryParse(s, out var d) && d > 0) nums.Add(d);
            return nums.Select(n => new FacetItem { Value = ((long)n).ToString(), Label = n.ToString("#,##0") }).ToList();
        }

        public async Task<List<FacetItem>> GetBrandsAsync()
        {
            using var c = Conn();
            string sql = UseSqlite
                ? @"SELECT DISTINCT CarBrandName FROM MotorPackages WHERE CarBrandName IS NOT NULL AND CarBrandName <> '' ORDER BY CarBrandName"
                : @"SELECT DISTINCT CarBrandName FROM PackagePolType WHERE CarBrandName IS NOT NULL AND CarBrandName <> '' ORDER BY CarBrandName";
            try
            {
                var rows = await c.QueryAsync<string>(sql);
                var facets = ToFacets(rows);
                if (facets.Count > 0) return facets;
            }
            catch { }

            return DefaultBrands();
        }

        private static List<FacetItem> DefaultBrands() => new()
        {
            new() { Value = "HONDA", Label = "HONDA" },
            new() { Value = "TOYOTA", Label = "TOYOTA" },
            new() { Value = "MAZDA", Label = "MAZDA" },
            new() { Value = "ISUZU", Label = "ISUZU" },
            new() { Value = "NISSAN", Label = "NISSAN" },
            new() { Value = "MITSUBISHI", Label = "MITSUBISHI" },
            new() { Value = "FORD", Label = "FORD" },
            new() { Value = "MG", Label = "MG" },
            new() { Value = "BYD", Label = "BYD" }
        };

        public async Task<List<FacetItem>> GetModelsAsync(string brand)
        {
            using var c = Conn();
            string sql = UseSqlite
                ? @"SELECT DISTINCT CarModelName FROM MotorPackages WHERE CarBrandName = @brand AND CarModelName IS NOT NULL AND CarModelName <> '' ORDER BY CarModelName"
                : @"SELECT DISTINCT CarModelName FROM PackagePolType WHERE CarBrandName = @brand AND CarModelName IS NOT NULL AND CarModelName <> '' ORDER BY CarModelName";
            try
            {
                var rows = await c.QueryAsync<string>(sql, new { brand });
                var facets = ToFacets(rows);
                if (facets.Count > 0) return facets;
            }
            catch { }

            return DefaultModels(brand);
        }

        private static List<FacetItem> DefaultModels(string brand)
        {
            var b = (brand ?? "").ToUpperInvariant();
            var list = b switch
            {
                "HONDA" => new[] { "CIVIC", "CITY", "HR-V", "CR-V", "JAZZ", "ACCORD" },
                "TOYOTA" => new[] { "YARIS", "YARIS ATIV", "COROLLA ALTIS", "COROLLA CROSS", "FORTUNER", "CAMRY", "HILUX REVO" },
                "MAZDA" => new[] { "MAZDA 2", "MAZDA 3", "CX-3", "CX-30", "CX-5" },
                "BYD" => new[] { "ATTO 3", "DOLPHIN", "SEAL" },
                _ => new[] { "รุ่นยอดนิยม 1", "รุ่นยอดนิยม 2", "รุ่นอื่นๆ" }
            };
            return list.Select(m => new FacetItem { Value = m, Label = m }).ToList();
        }

        public async Task<List<FacetItem>> GetCcAsync(string brand, string model)
        {
            using var c = Conn();
            string sql = UseSqlite
                ? @"SELECT DISTINCT Ccmax FROM MotorPackages WHERE CarBrandName = @brand AND CarModelName = @model AND Ccmax IS NOT NULL AND Ccmax <> '' ORDER BY CAST(Ccmax AS INT)"
                : @"SELECT cc FROM (SELECT DISTINCT m.Ccmax AS cc FROM PackagePolType p JOIN PackageMotor m ON p.PackageId = m.PackageId WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax IS NOT NULL AND m.Ccmax <> '') t ORDER BY TRY_CAST(t.cc AS INT)";
            try
            {
                var rows = await c.QueryAsync<string>(sql, new { brand, model });
                var facets = ToFacets(rows);
                if (facets.Count > 0) return facets;
            }
            catch { }

            return new List<FacetItem>
            {
                new() { Value = "1200", Label = "1200" },
                new() { Value = "1500", Label = "1500" },
                new() { Value = "1800", Label = "1800" },
                new() { Value = "2000", Label = "2000" }
            };
        }

        public async Task<List<FacetItem>> GetYearsAsync(string brand, string model, string cc)
        {
            using var c = Conn();
            string sql = UseSqlite
                ? @"SELECT DISTINCT Year FROM MotorPackages WHERE CarBrandName = @brand AND CarModelName = @model "
                  + (string.IsNullOrWhiteSpace(cc) ? "" : "AND Ccmax = @cc ")
                  + "ORDER BY Year DESC"
                : @"SELECT DISTINCT p.Year FROM PackagePolType p JOIN PackageMotor m ON p.PackageId = m.PackageId WHERE p.CarBrandName = @brand AND p.CarModelName = @model "
                  + (string.IsNullOrWhiteSpace(cc) ? "" : "AND m.Ccmax = @cc ")
                  + "ORDER BY p.Year DESC";
            try
            {
                var res = await c.QueryAsync<int>(sql, new { brand, model, cc });
                var list = res.Where(y => y > 0).Select(y => new FacetItem { Value = y.ToString(), Label = y.ToString() }).ToList();
                if (list.Count > 0) return list;
            }
            catch { }

            var years = Enumerable.Range(2015, 11).Reverse();
            return years.Select(y => new FacetItem { Value = y.ToString(), Label = y.ToString() }).ToList();
        }

        public async Task<List<PolTypeItem>> GetPolTypesAsync(string brand, string model, string cc, int year)
        {
            using var c = Conn();
            if (UseSqlite)
            {
                string sql = @"SELECT DISTINCT PolTypeName FROM MotorPackages WHERE CarBrandName = @brand AND CarModelName = @model "
                           + (year > 0 ? "AND Year = @year " : "")
                           + "ORDER BY PolTypeName";
                var rows = await c.QueryAsync<string>(sql, new { brand, model, year });
                return rows.Select((p, idx) => new PolTypeItem { PolTypeId = idx + 1, PolTypeName = p }).ToList();
            }
            else
            {
                const string sql = @"SELECT DISTINCT p.PolTypeId, pt.PolTypeName FROM PackagePolType p
                                     JOIN PackageMotor m ON p.PackageId = m.PackageId
                                     LEFT JOIN PolicyType pt ON p.PolTypeId = pt.PolTypeId
                                     WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax = @cc AND p.Year = @year
                                     ORDER BY pt.PolTypeName";
                return (await c.QueryAsync<PolTypeItem>(sql, new { brand, model, cc, year })).ToList();
            }
        }

        public async Task<List<FacetItem>> GetSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId)
        {
            using var c = Conn();
            if (UseSqlite)
            {
                string sql = @"SELECT DISTINCT SumInsureMax FROM MotorPackages WHERE CarBrandName = @brand AND CarModelName = @model "
                           + (year > 0 ? "AND Year = @year " : "")
                           + "AND SumInsureMax > 0 ORDER BY SumInsureMax";
                var sums = await c.QueryAsync<decimal>(sql, new { brand, model, year });
                return sums.Select(s => new FacetItem { Value = ((long)s).ToString(), Label = s.ToString("#,##0") }).ToList();
            }
            else
            {
                var sql = @"SELECT DISTINCT p.SumInsureMax FROM PackagePolType p
                            JOIN PackageMotor m ON p.PackageId = m.PackageId
                            WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax = @cc AND p.Year = @year
                              AND p.SumInsureMax > 0 "
                          + (polTypeId.HasValue ? "AND p.PolTypeId = @polTypeId " : "")
                          + "ORDER BY p.SumInsureMax";
                var sums = await c.QueryAsync<decimal>(sql, new { brand, model, cc, year, polTypeId });
                return sums.Select(s => new FacetItem { Value = ((long)s).ToString(), Label = s.ToString("#,##0") }).ToList();
            }
        }

        public async Task<List<FacetItem>> GetFireSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId)
            => await MotorMoneyFacet("FireTheft", brand, model, cc, year, polTypeId);

        public async Task<List<FacetItem>> GetFloodSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId)
            => await MotorMoneyFacet("Flood", brand, model, cc, year, polTypeId);

        private async Task<List<FacetItem>> MotorMoneyFacet(string col, string brand, string model, string cc, int year, int? polTypeId)
        {
            // col = FireTheft | Flood (อยู่บน PackageMotor, เก็บเป็น string)
            var sql = $@"SELECT DISTINCT m.{col} AS v FROM PackagePolType p
                         JOIN PackageMotor m ON p.PackageId = m.PackageId
                         WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax = @cc AND p.Year = @year "
                       + (polTypeId.HasValue ? "AND p.PolTypeId = @polTypeId " : "");
            using var c = Conn();
            return ToMoneyFacets(await c.QueryAsync<string>(sql, new { brand, model, cc, year, polTypeId }));
        }

        public async Task<List<FacetItem>> GetProvincesAsync()
        {
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
            {
                return new List<FacetItem>
                {
                    new() { Value = "1", Label = "กรุงเทพมหานคร" },
                    new() { Value = "2", Label = "นนทบุรี" },
                    new() { Value = "3", Label = "ปทุมธานี" },
                    new() { Value = "4", Label = "สมุทรปราการ" },
                    new() { Value = "5", Label = "เชียงใหม่" },
                    new() { Value = "6", Label = "ชลบุรี" },
                    new() { Value = "7", Label = "ขอนแก่น" },
                    new() { Value = "8", Label = "ภูเก็ต" },
                    new() { Value = "9", Label = "สงขลา" },
                    new() { Value = "10", Label = "นครราชสีมา" }
                };
            }
            const string sql = @"SELECT ProvId, ProvName FROM Province
                                 WHERE ProvName IS NOT NULL AND ProvName <> '' ORDER BY ProvName";
            using var c = Conn();
            var rows = await c.QueryAsync(sql);
            return rows.Select(r => new FacetItem
            {
                Value = ((object)r.ProvId)?.ToString(),
                Label = (string)r.ProvName
            }).ToList();
        }

        public async Task<List<PackageRow>> SearchPackagesAsync(PackageSearchRequest r)
        {
            using var c = Conn();
            if (UseSqlite)
            {
                var brand = !string.IsNullOrWhiteSpace(r.Brand) ? r.Brand.Trim().ToUpperInvariant() : "HONDA";
                var model = !string.IsNullOrWhiteSpace(r.Model) ? r.Model.Trim().ToUpperInvariant() : "CIVIC";
                var year = r.Year > 0 ? r.Year : 2022;

                var sql = @"SELECT Id AS PackageId, InsurerName, 'https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100' AS InsurerLogo,
                                   PackageName, PolTypeName, RenovType, SumInsureMax,
                                   PremNet, PremStm, PremStm AS PremTotal, FireSuminsure, FloodSuminsure
                            FROM MotorPackages
                            WHERE CarBrandName = @brand AND CarModelName = @model ";
                if (r.Year > 0) sql += "AND Year = @year ";
                if (!string.IsNullOrWhiteSpace(r.Cc)) sql += "AND Ccmax = @Cc ";
                if (r.SumInsure.HasValue && r.SumInsure.Value > 0) sql += "AND SumInsureMax = @SumInsure ";

                sql += (r.Sort) switch
                {
                    "prem_desc" => "ORDER BY PremNet DESC ",
                    "sum"       => "ORDER BY SumInsureMax ASC, PremNet ASC ",
                    "sum_desc"  => "ORDER BY SumInsureMax DESC, PremNet ASC ",
                    _           => "ORDER BY PremNet ASC "
                };
                sql += "LIMIT 500";

                var rows = (await c.QueryAsync<PackageRow>(sql, new { brand, model, year, Cc = r.Cc, SumInsure = r.SumInsure })).ToList();
                if (rows.Count == 0)
                {
                    // Fallback using LIKE in case model is partial (e.g. CAMRY matching CAMRY 2.0)
                    var likeSql = @"SELECT Id AS PackageId, InsurerName, 'https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100' AS InsurerLogo,
                                           PackageName, PolTypeName, RenovType, SumInsureMax,
                                           PremNet, PremStm, PremStm AS PremTotal, FireSuminsure, FloodSuminsure
                                    FROM MotorPackages
                                    WHERE CarBrandName = @brand AND CarModelName LIKE @modelPattern ";
                    if (r.Year > 0) likeSql += "AND Year = @year ";
                    likeSql += "ORDER BY PremNet ASC LIMIT 500";
                    rows = (await c.QueryAsync<PackageRow>(likeSql, new { brand, modelPattern = $"%{model}%", year })).ToList();
                }

                if (rows.Count > 0)
                {
                    if (r.FireSum.HasValue && r.FireSum.Value > 0) rows = rows.Where(p => p.FireSuminsure >= r.FireSum.Value).ToList();
                    if (r.FloodSum.HasValue && r.FloodSum.Value > 0) rows = rows.Where(p => p.FloodSuminsure >= r.FloodSum.Value).ToList();
                    return rows;
                }
            }

            var serverSql = @"SELECT p.PackageId, ins.Name AS InsurerName, ins.LogoPath,
                               p.PackageName, pt.PolTypeName, m.RenovType, p.SumInsureMax,
                               p.PremNet, p.PremStm, p.Discount, p.DiscType, m.FireTheft, m.Flood
                        FROM PackagePolType p
                        JOIN PackageMotor m ON p.PackageId = m.PackageId
                        LEFT JOIN PolicyType pt ON p.PolTypeId = pt.PolTypeId
                        LEFT JOIN Insure ins ON p.InsureId = ins.InsureId
                        WHERE p.CarBrandName = @Brand AND p.CarModelName = @Model AND p.Year = @Year "
                      + (string.IsNullOrWhiteSpace(r.Cc) ? "" : "AND m.Ccmax = @Cc ")   // cc ว่าง = รวมทุกขนาดเครื่อง
                      + (r.PolTypeId.HasValue ? "AND p.PolTypeId = @PolTypeId " : "")
                      + (r.SumInsure.HasValue ? "AND p.SumInsureMax = @SumInsure " : "");
                      // หมายเหตุ: จังหวัด (ProvinId) ไม่กรอง — ทุกแพคเกจเป็น nationwide (ProvinId=0) และไม่อยู่ใน
                      // ProvinSale; ตรงกับ KPN live (provinnamePackage="ไม่มีข้อมูล"). เก็บค่าไว้ลง proposal เท่านั้น
            var raws = (await c.QueryAsync<PackageRaw>(serverSql, r)).ToList();

            var serverRows = raws.Select(x =>
            {
                decimal fire = ParseMoney(x.FireTheft), flood = ParseMoney(x.Flood);
                return new PackageRow
                {
                    PackageId = x.PackageId,
                    InsurerName = x.InsurerName,
                    InsurerLogo = BuildLogoUrl(x.LogoPath),
                    PackageName = x.PackageName,
                    PolTypeName = x.PolTypeName,
                    RenovType = x.RenovType == "2" ? "ห้าง" : "อู่",
                    SumInsureMax = x.SumInsureMax,
                    PremNet = x.PremNet,
                    PremTotal = x.PremStm,                                    // เบี้ยรวมอากรดิบ
                    PremStm = SetPremStm(x.DiscType, x.Discount, x.PremNet, x.PremStm), // หลังส่วนลด (ตรง KPN)
                    Discount = (x.DiscType != null && x.DiscType != "0") ? x.Discount : 0,
                    DiscType = x.DiscType == "P" ? "%" : x.DiscType == "B" ? "บาท"
                               : (!string.IsNullOrEmpty(x.DiscType) && x.DiscType != "0" ? "อื่นๆ" : ""),
                    FireSuminsure = fire,
                    FloodSuminsure = flood
                };
            });

            // กรอง fire/flood ฝั่ง C# (เก็บเป็น string ใน DB) ถ้าผู้ใช้เลือก
            if (r.FireSum.HasValue) serverRows = serverRows.Where(p => p.FireSuminsure == r.FireSum.Value);
            if (r.FloodSum.HasValue) serverRows = serverRows.Where(p => p.FloodSuminsure == r.FloodSum.Value);

            var list = serverRows.ToList();
            list = (r.Sort) switch
            {
                "prem_desc" => list.OrderByDescending(p => p.PremStm).ToList(),
                "sum"       => list.OrderBy(p => p.SumInsureMax).ToList(),
                "sum_desc"  => list.OrderByDescending(p => p.SumInsureMax).ToList(),
                _           => list.OrderBy(p => p.PremStm).ToList()
            };
            return list.Take(500).ToList();   // candidate set ใช้เต็ม (AI สโคปต่อ); UI เดิมเลิกใช้แล้ว
        }

        private class PkgDetailRaw
        {
            public int PackageId { get; set; }
            public string InsurerName { get; set; }
            public string LogoPath { get; set; }
            public string PackageName { get; set; }
            public string PolTypeName { get; set; }
            public string CarUsage { get; set; }
            public string RenovType { get; set; }
            public decimal SumInsureMax { get; set; }
            public string OwnAmtDeduc { get; set; }
            public decimal PremNet { get; set; }
            public decimal PremStm { get; set; }
            public decimal Discount { get; set; }
            public string DiscType { get; set; }
            public string FireTheft { get; set; }
            public string Flood { get; set; }
            public string Terrorism { get; set; }
            public decimal? ThirdBodilyPers { get; set; }
            public decimal? ThirdBodilyAccid { get; set; }
            public decimal? ThirdCtpl { get; set; }
            public decimal? ThirdAmtDeduc { get; set; }
            public short? BodilyDrivCount { get; set; }
            public decimal? BodilyDrivCover { get; set; }
            public short? BodilyPassCount { get; set; }
            public decimal? BodilyPassCover { get; set; }
            public short? MedicalPers { get; set; }
            public decimal? MedicalBaht { get; set; }
            public decimal? BailBondIns { get; set; }
        }

        public async Task<MotorPackageDetail> GetPackageDetailAsync(int packageId)
        {
            using var c = Conn();
            if (UseSqlite)
            {
                var sqliteSql = @"SELECT Id AS PackageId, InsurerName, 'https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100' AS InsurerLogo,
                                         PackageName, PolTypeName, '110' AS CarUsage, RenovType, SumInsureMax AS SumInsure,
                                         0 AS Deductible, PremNet, PremStm AS PremTotal, FireSuminsure AS FireTheft, FloodSuminsure AS Flood, 0 AS Terrorism
                                  FROM MotorPackages
                                  WHERE Id = @packageId LIMIT 1";
                var p = await c.QueryFirstOrDefaultAsync<MotorPackageDetail>(sqliteSql, new { packageId });
                return p;
            }

            const string sql = @"SELECT TOP 1 p.PackageId, ins.Name AS InsurerName, ins.LogoPath, p.PackageName,
                                        pt.PolTypeName, m.CarUsage, m.RenovType, p.SumInsureMax,
                                        m.OwnAmtDeduc, p.PremNet, p.PremStm, p.Discount, p.DiscType,
                                        m.FireTheft, m.Flood, m.Terrorism,
                                        m.ThirdBodilyPers, m.ThirdBodilyAccid, m.ThirdCtpl, m.ThirdAmtDeduc,
                                        m.BodilyDrivCount, m.BodilyDrivCover, m.BodilyPassCount, m.BodilyPassCover,
                                        m.MedicalPers, m.MedicalBaht, m.BailBondIns
                                 FROM PackagePolType p
                                 JOIN PackageMotor m ON p.PackageId = m.PackageId
                                 LEFT JOIN PolicyType pt ON p.PolTypeId = pt.PolTypeId
                                 LEFT JOIN Insure ins ON p.InsureId = ins.InsureId
                                 WHERE p.PackageId = @packageId";
            var x = await c.QueryFirstOrDefaultAsync<PkgDetailRaw>(sql, new { packageId });
            if (x == null) return null;
            return new MotorPackageDetail
            {
                PackageId = x.PackageId,
                InsurerName = x.InsurerName,
                InsurerLogo = BuildLogoUrl(x.LogoPath),
                PackageName = x.PackageName,
                PolTypeName = x.PolTypeName,
                CarUsage = x.CarUsage?.Trim(),
                RenovType = x.RenovType?.Trim() == "2" ? "ห้าง" : "อู่",
                SumInsure = x.SumInsureMax,
                Deductible = ParseMoney(x.OwnAmtDeduc),
                PremNet = x.PremNet,
                PremTotal = SetPremStm(x.DiscType, x.Discount, x.PremNet, x.PremStm),
                FireTheft = ParseMoney(x.FireTheft),
                Flood = ParseMoney(x.Flood),
                Terrorism = ParseMoney(x.Terrorism),
                ThirdBodilyPers = x.ThirdBodilyPers ?? 0,
                ThirdBodilyAccid = x.ThirdBodilyAccid ?? 0,
                ThirdProperty = x.ThirdCtpl ?? 0,
                ThirdPropertyDeduc = x.ThirdAmtDeduc ?? 0,
                BodilyDrivCount = x.BodilyDrivCount ?? 0,
                BodilyDrivCover = x.BodilyDrivCover ?? 0,
                BodilyPassCount = x.BodilyPassCount ?? 0,
                BodilyPassCover = x.BodilyPassCover ?? 0,
                MedicalPers = x.MedicalPers ?? 0,
                MedicalBaht = x.MedicalBaht ?? 0,
                BailBondIns = x.BailBondIns ?? 0
            };
        }

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

        // ===== helpers (mirror KPN) =====
        private static decimal ParseMoney(string s) => decimal.TryParse(s, out var d) ? d : 0m;

        private string BuildLogoUrl(string logoPath) =>
            string.IsNullOrWhiteSpace(logoPath) ? "" : _db.LogoBaseUrl + logoPath;

        // ตรงกับ CheckPrmCheckPrmController.SetPremStm
        private static decimal SetPremStm(string discType, decimal discount, decimal premNet, decimal premStm)
        {
            var value = premStm;
            if (discount > 0)
            {
                if (discType == "P") value -= (premNet * discount) / 100;
                else if (discType == "B") value -= discount;
            }
            return value;
        }
    }
}
