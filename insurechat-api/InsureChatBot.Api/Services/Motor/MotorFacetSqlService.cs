using Dapper;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Motor;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Motor
{
    /// <summary>
    /// อ่าน facet + แพคเกจสดจาก KPN SQL DB (KPNNew) ด้วย Dapper (read-only).
    /// mirror logic ของ KPN: SetPremStm (ส่วนลด), logo (GetUrlLogo+LogoPath), fire/flood (PackageMotor). ดู ADR 0004.
    /// </summary>
    public class MotorFacetSqlService : IMotorFacetService
    {
        private readonly KpnDbSettings _db;
        public MotorFacetSqlService(IOptions<KpnDbSettings> opt) { _db = opt.Value; }

        private SqlConnection Conn() => new SqlConnection(_db.ConnectionString);

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
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
            {
                return new List<FacetItem>
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
            }
            const string sql = @"SELECT DISTINCT CarBrandName FROM PackagePolType
                                 WHERE CarBrandName IS NOT NULL AND CarBrandName <> '' ORDER BY CarBrandName";
            using var c = Conn();
            return ToFacets(await c.QueryAsync<string>(sql));
        }

        public async Task<List<FacetItem>> GetModelsAsync(string brand)
        {
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
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
            const string sql = @"SELECT DISTINCT CarModelName FROM PackagePolType
                                 WHERE CarBrandName = @brand AND CarModelName IS NOT NULL AND CarModelName <> ''
                                 ORDER BY CarModelName";
            using var c = Conn();
            return ToFacets(await c.QueryAsync<string>(sql, new { brand }));
        }

        public async Task<List<FacetItem>> GetCcAsync(string brand, string model)
        {
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
            {
                return new List<FacetItem>
                {
                    new() { Value = "1200", Label = "1200" },
                    new() { Value = "1500", Label = "1500" },
                    new() { Value = "1800", Label = "1800" },
                    new() { Value = "2000", Label = "2000" }
                };
            }
            const string sql = @"SELECT cc FROM (
                                   SELECT DISTINCT m.Ccmax AS cc FROM PackagePolType p
                                   JOIN PackageMotor m ON p.PackageId = m.PackageId
                                   WHERE p.CarBrandName = @brand AND p.CarModelName = @model
                                     AND m.Ccmax IS NOT NULL AND m.Ccmax <> ''
                                 ) t ORDER BY TRY_CAST(t.cc AS INT)";
            using var c = Conn();
            return ToFacets(await c.QueryAsync<string>(sql, new { brand, model }));
        }

        public async Task<List<FacetItem>> GetYearsAsync(string brand, string model, string cc)
        {
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
            {
                var years = Enumerable.Range(2015, 11).Reverse();
                return years.Select(y => new FacetItem { Value = y.ToString(), Label = y.ToString() }).ToList();
            }
            // cc ว่าง = ไม่กรองขนาดเครื่อง → คืนปีรวมทุก CC (ฟอร์มตัด step CC ออกแล้ว, AI สโคปทีหลัง)
            var sql = @"SELECT DISTINCT p.Year FROM PackagePolType p
                        JOIN PackageMotor m ON p.PackageId = m.PackageId
                        WHERE p.CarBrandName = @brand AND p.CarModelName = @model "
                      + (string.IsNullOrWhiteSpace(cc) ? "" : "AND m.Ccmax = @cc ")
                      + "ORDER BY p.Year DESC";
            using var c = Conn();
            var res = await c.QueryAsync<int>(sql, new { brand, model, cc });
            return res.Select(y => new FacetItem { Value = y.ToString(), Label = y.ToString() }).ToList();
        }

        public async Task<List<PolTypeItem>> GetPolTypesAsync(string brand, string model, string cc, int year)
        {
            const string sql = @"SELECT DISTINCT p.PolTypeId, pt.PolTypeName FROM PackagePolType p
                                 JOIN PackageMotor m ON p.PackageId = m.PackageId
                                 LEFT JOIN PolicyType pt ON p.PolTypeId = pt.PolTypeId
                                 WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax = @cc AND p.Year = @year
                                 ORDER BY pt.PolTypeName";
            using var c = Conn();
            return (await c.QueryAsync<PolTypeItem>(sql, new { brand, model, cc, year })).ToList();
        }

        public async Task<List<FacetItem>> GetSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId)
        {
            var sql = @"SELECT DISTINCT p.SumInsureMax FROM PackagePolType p
                        JOIN PackageMotor m ON p.PackageId = m.PackageId
                        WHERE p.CarBrandName = @brand AND p.CarModelName = @model AND m.Ccmax = @cc AND p.Year = @year
                          AND p.SumInsureMax > 0 "
                      + (polTypeId.HasValue ? "AND p.PolTypeId = @polTypeId " : "")
                      + "ORDER BY p.SumInsureMax";
            using var c = Conn();
            var sums = await c.QueryAsync<decimal>(sql, new { brand, model, cc, year, polTypeId });
            return sums.Select(s => new FacetItem { Value = ((long)s).ToString(), Label = s.ToString("#,##0") }).ToList();
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
            if (string.IsNullOrWhiteSpace(_db.ConnectionString))
            {
                // ข้อมูลจำลองแพ็กเกจประกันภัยรถยนต์สุดหรู สมจริงระดับพรีเมียม
                var brand = !string.IsNullOrWhiteSpace(r.Brand) ? r.Brand : "HONDA";
                var model = !string.IsNullOrWhiteSpace(r.Model) ? r.Model : "CIVIC";
                var year = r.Year > 0 ? r.Year : 2022;

                return new List<PackageRow>
                {
                    new()
                    {
                        PackageId = 101,
                        InsurerName = "วิริยะประกันภัย",
                        InsurerLogo = "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100",
                        PackageName = $"{brand} {model} ซ่อมห้าง Premium",
                        PolTypeName = "ประกันภัยรถยนต์ชั้น 1",
                        RenovType = "ห้าง",
                        SumInsureMax = 550000,
                        PremNet = 17500,
                        PremStm = 18600,
                        PremTotal = 18600,
                        FireSuminsure = 550000,
                        FloodSuminsure = 550000
                    },
                    new()
                    {
                        PackageId = 102,
                        InsurerName = "กรุงเทพประกันภัย",
                        InsurerLogo = "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100",
                        PackageName = $"{brand} {model} สบายใจ ไร้กังวล",
                        PolTypeName = "ประกันภัยรถยนต์ชั้น 1",
                        RenovType = "อู่",
                        SumInsureMax = 500000,
                        PremNet = 14200,
                        PremStm = 15100,
                        PremTotal = 15100,
                        FireSuminsure = 500000,
                        FloodSuminsure = 500000
                    },
                    new()
                    {
                        PackageId = 103,
                        InsurerName = "คุ้มภัยโตเกียวมารีน",
                        InsurerLogo = "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100",
                        PackageName = $"{brand} 2+ พลัสคุ้มครองน้ำท่วม",
                        PolTypeName = "ประกันภัยรถยนต์ชั้น 2+",
                        RenovType = "อู่",
                        SumInsureMax = 250000,
                        PremNet = 7900,
                        PremStm = 8400,
                        PremTotal = 8400,
                        FireSuminsure = 250000,
                        FloodSuminsure = 100000
                    },
                    new()
                    {
                        PackageId = 104,
                        InsurerName = "ทิพยประกันภัย",
                        InsurerLogo = "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?w=100",
                        PackageName = $"{brand} Tip Premium Care",
                        PolTypeName = "ประกันภัยรถยนต์ชั้น 1",
                        RenovType = "ห้าง",
                        SumInsureMax = 520000,
                        PremNet = 16800,
                        PremStm = 17900,
                        PremTotal = 17900,
                        FireSuminsure = 520000,
                        FloodSuminsure = 520000
                    }
                };
            }
            var sql = @"SELECT p.PackageId, ins.Name AS InsurerName, ins.LogoPath,
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
            using var c = Conn();
            var raws = (await c.QueryAsync<PackageRaw>(sql, r)).ToList();

            var rows = raws.Select(x =>
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
            if (r.FireSum.HasValue) rows = rows.Where(p => p.FireSuminsure == r.FireSum.Value);
            if (r.FloodSum.HasValue) rows = rows.Where(p => p.FloodSuminsure == r.FloodSum.Value);

            var list = rows.ToList();
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
            using var c = Conn();
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
