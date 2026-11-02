using System.Collections.Generic;
using System.Linq;
using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Motor
{
    /// <summary>
    /// แมตช์ค่าที่ vision อ่านได้ (free text) → ค่า canonical ใน facet DB ก่อน prefill.
    /// pure = unit-testable ไม่พึ่ง DB. ดู ADR 0018 (Facet resolve).
    /// </summary>
    public static class VehicleResolver
    {
        // เทียบแบบ normalize: ตัวพิมพ์ใหญ่ + เหลือเฉพาะตัวอักษร/ตัวเลข ("K 2700 M" → "K2700M")
        private static string Norm(string s)
            => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static string ResolveBrand(string extracted, IReadOnlyList<FacetItem> brands)
            => ResolveFacet(extracted, brands);

        public static string ResolveModel(string extracted, IReadOnlyList<FacetItem> models)
            => ResolveFacet(extracted, models);

        // logic ร่วม brand/model: exact normalize ก่อน แล้วค่อย fuzzy contains
        private static string ResolveFacet(string extracted, IReadOnlyList<FacetItem> items)
        {
            if (string.IsNullOrWhiteSpace(extracted) || items == null) return null;
            var n = Norm(extracted);
            if (n.Length == 0) return null;

            // exact normalized match (ไม่มี length guard — ชื่อสั้นจริง เช่น "MG" ต้องแมตช์ได้)
            var exact = items.FirstOrDefault(x => Norm(x.Value) == n);
            if (exact != null) return exact.Value;

            // fuzzy contains — เฉพาะเมื่อทั้งสองฝั่งยาวพอ (>= 3) กัน false-positive จาก OCR สั้น/เพี้ยน
            if (n.Length < 3) return null;
            return items
                .Where(x => { var xn = Norm(x.Value); return xn.Length >= 3 && (xn.Contains(n) || n.Contains(xn)); })
                .OrderBy(x => Norm(x.Value).Length)   // เลือก canonical ที่สั้น/ใกล้สุด ("K2700" ก่อน "K2700MEGA")
                .FirstOrDefault()?.Value;
        }

        public static int? ResolveYear(int? extracted, IReadOnlyList<FacetItem> years)
        {
            if (extracted == null || years == null) return null;
            var target = extracted.Value.ToString();
            return years.Any(y => y.Value == target) ? extracted : (int?)null;
        }
    }
}
