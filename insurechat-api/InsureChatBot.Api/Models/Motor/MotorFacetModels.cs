namespace InsureChatBot.Api.Models.Motor
{
    /// <summary>ตัวเลือก dropdown ทั่วไป (value/label สำหรับ brand/model/cc/year/sum/fire/flood/province)</summary>
    public class FacetItem
    {
        public string Value { get; set; }
        public string Label { get; set; }
    }

    /// <summary>ผลิตภัณฑ์ (polType) — id + ชื่อ</summary>
    public class PolTypeItem
    {
        public int PolTypeId { get; set; }
        public string PolTypeName { get; set; }
    }

    /// <summary>1 แถวในตารางเปรียบเทียบแพคเกจ (mirror Search_GetData ของ KPN)</summary>
    public class PackageRow
    {
        public int PackageId { get; set; }
        public string InsurerName { get; set; }
        public string InsurerLogo { get; set; }     // URL เต็ม (logoBase + LogoPath)
        public string PackageName { get; set; }
        public string PolTypeName { get; set; }
        public string RenovType { get; set; }       // "อู่" | "ห้าง"
        public decimal SumInsureMax { get; set; }
        public decimal PremNet { get; set; }         // เบี้ยสุทธิ/ปี
        public decimal PremStm { get; set; }         // เบี้ยรวมหลังหักส่วนลด (= SetPremStm) — ตรงกับ KPN
        public decimal PremTotal { get; set; }       // เบี้ยรวมอากรดิบ (ก่อนส่วนลด)
        public decimal Discount { get; set; }
        public string DiscType { get; set; }         // "%" | "บาท" | "อื่นๆ" | ""
        public decimal FireSuminsure { get; set; }
        public decimal FloodSuminsure { get; set; }
    }

    public class PackageSearchRequest
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Cc { get; set; }
        public int Year { get; set; }
        public int? ProvinId { get; set; }           // จังหวัดทะเบียน — กรองผ่าน ProvinSale (ADR 0005)
        public int? PolTypeId { get; set; }
        public decimal? SumInsure { get; set; }
        public decimal? FireSum { get; set; }
        public decimal? FloodSum { get; set; }
        public string Sort { get; set; }             // "prem" | "prem_desc" | "sum" | "sum_desc"
    }

    /// <summary>row ดิบจาก SQL ก่อนคำนวณ SetPremStm/logo</summary>
    internal class PackageRaw
    {
        public int PackageId { get; set; }
        public string InsurerName { get; set; }
        public string LogoPath { get; set; }
        public string PackageName { get; set; }
        public string PolTypeName { get; set; }
        public string RenovType { get; set; }
        public decimal SumInsureMax { get; set; }
        public decimal PremNet { get; set; }
        public decimal PremStm { get; set; }
        public decimal Discount { get; set; }
        public string DiscType { get; set; }
        public string FireTheft { get; set; }
        public string Flood { get; set; }
    }

    /// <summary>ผลการ resolve เอกสารรถ → ค่า facet canonical (ADR 0018)</summary>
    public class VehicleResolveResult
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public int? Year { get; set; }
        public bool BrandOk => Brand != null;
        public bool ModelOk => Model != null;
        public bool YearOk => Year != null;
    }
}
