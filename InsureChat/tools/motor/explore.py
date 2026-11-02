# -*- coding: utf-8 -*-
"""สแกนข้อมูลแพคเกจรถยนต์ทั้งหมด เพื่อสรุปหมวดหมู่ + บันทึก _summary.json"""
import json, os, sys
from collections import Counter
import motorlib as M
sys.stdout.reconfigure(encoding="utf-8")

total = 0
insurers = Counter(); classes = Counter(); brands = Counter()
renov = Counter(); years = Counter(); comein = Counter()
brand_by_insurer = {}
prem_min, prem_max = float("inf"), 0
sum_min, sum_max = float("inf"), 0
keys_seen = set()

for r in M.iter_records():
    total += 1
    keys_seen.update(r.keys())
    ins = (r.get("insureID") or "ไม่ระบุ").strip()
    insurers[ins] += 1
    classes[M.record_class(r)] += 1
    brand = (r.get("carBrandName") or "ไม่ระบุ").strip()
    brands[brand] += 1
    renov[(r.get("renovType") or "ไม่ระบุ").strip()] += 1
    y = r.get("year")
    if y: years[y] += 1
    comein[(r.get("comeInCode") or "ไม่ระบุ").strip()] += 1
    pn = r.get("premNet")
    if isinstance(pn,(int,float)) and pn>0:
        prem_min = min(prem_min, pn); prem_max = max(prem_max, pn)
    si = r.get("sumInsureMax")
    if isinstance(si,(int,float)) and si>0:
        sum_min = min(sum_min, si); sum_max = max(sum_max, si)

print("=== รวมทั้งหมด:", f"{total:,}", "รายการ ===")
print("\n--- ฟิลด์ ---"); print(sorted(keys_seen))
print("\n--- บริษัทประกัน ---")
for k,v in insurers.most_common(): print(f"  {v:>7,}  {k}")
print("\n--- ชั้นประกัน ---")
for k,v in classes.most_common(): print(f"  {v:>7,}  {k}")
print("\n--- ประเภทซ่อม ---")
for k,v in renov.most_common(): print(f"  {v:>7,}  {k}")
print("\n--- ยี่ห้อรถ (top 40 / ทั้งหมด %d) ---" % len(brands))
for k,v in brands.most_common(40): print(f"  {v:>7,}  {k}")
print("\n--- ปีรถ:", min(years) if years else "-", "ถึง", max(years) if years else "-",
      "| เบี้ย: %.0f-%.0f | ทุน: %.0f-%.0f ---" % (prem_min, prem_max, sum_min, sum_max))

summary = {
    "total": total, "fields": sorted(keys_seen),
    "insurers": insurers.most_common(), "classes": classes.most_common(),
    "renovType": renov.most_common(), "comeInCode": comein.most_common(),
    "brands": brands.most_common(),
    "year_min": min(years) if years else None, "year_max": max(years) if years else None,
    "prem_min": prem_min, "prem_max": prem_max, "sum_min": sum_min, "sum_max": sum_max,
}
with open(os.path.join(os.path.dirname(__file__), "_summary.json"), "w", encoding="utf-8") as f:
    json.dump(summary, f, ensure_ascii=False, indent=2)
print("\n[saved] _summary.json")
