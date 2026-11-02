# -*- coding: utf-8 -*-
"""สร้างหน้าคลังข้อมูลแพคเกจรถยนต์ (wiki/products/แพคเกจรถยนต์-T-Broker.md) อัตโนมัติ
สแกนข้อมูลจริงรอบเดียว → ออกตารางหมวดหมู่ + crosstab. รันซ้ำได้ทุกครั้งที่ข้อมูลอัปเดต"""
import os, sys, datetime
from collections import Counter, defaultdict
import motorlib as M
sys.stdout.reconfigure(encoding="utf-8")

total = 0
insurers = Counter(); classes = Counter(); brands = Counter(); years = Counter(); renov = Counter()
ins_cls = defaultdict(Counter)        # insurer -> class -> n
brand_cls = defaultdict(Counter)      # brand -> class -> n
cls_prem = defaultdict(lambda: [float("inf"), 0, 0.0, 0])  # class -> [min,max,sum,n]

for r in M.iter_records():
    total += 1
    ins = (r.get("insureID") or "ไม่ระบุ").strip()
    cls = M.record_class(r)
    brand = (r.get("carBrandName") or "ไม่ระบุ").strip()
    insurers[ins]+=1; classes[cls]+=1; brands[brand]+=1; renov[(r.get("renovType") or "-").strip()]+=1
    ins_cls[ins][cls]+=1; brand_cls[brand][cls]+=1
    if r.get("year"): years[r["year"]]+=1
    pn = r.get("premNet")
    if isinstance(pn,(int,float)) and pn>0:
        s = cls_prem[cls]; s[0]=min(s[0],pn); s[1]=max(s[1],pn); s[2]+=pn; s[3]+=1

CLASS_ORDER = ["ชั้น 1","ชั้น 2+","ชั้น 3+","ชั้น 2","ชั้น 3","พ.ร.บ.","อื่น ๆ/ไม่ระบุ"]
present = [c for c in CLASS_ORDER if classes.get(c)]
def short_ins(name):
    for k in ["ธนชาต","คุ้มภัยโตเกียว","เมืองไทย","วิริยะ"]:
        if k in name: return k
    return name[:14]

today = "2026-06-01"
L = []
L.append("---")
L.append("type: product")
L.append("title: แพคเกจรถยนต์ T Broker (คลังข้อมูล)")
L.append("aliases: [แพคเกจรถยนต์, คลังแพคเกจรถ, motor packages, T Broker motor]")
L.append("tags: [product, motor, dataset, วินาศภัย]")
L.append(f"created: {today}")
L.append(f"updated: {today}")
L.append("sources: [S-009, S-010]")
L.append("---")
L.append("")
L.append("# 🚗 แพคเกจรถยนต์ T Broker — คลังข้อมูล")
L.append("")
L.append(f"> 🤖 หน้านี้ **สร้างอัตโนมัติ** จาก `tools/motor/build_catalog.py` (สแกน {total:,} แพคเกจจริง) — อย่าแก้มือ รันสคริปต์ใหม่เมื่อข้อมูลเปลี่ยน")
L.append(f"> ข้อมูลดิบ (source of truth ไม่แตะ): `raw/ข้อมูลแพคเกจรถยนต์_Page1-10.json` · ที่มา [[S-010-dataset-แพคเกจรถยนต์|S-010]] ผ่าน [[T-Broker-โบรกเกอร์|T Broker]] ([[S-009-T-Broker-portal|S-009]])")
L.append("")
L.append(f"**รวมทั้งสิ้น {total:,} แพคเกจ** · ปีรถ {min(years)}–{max(years)} · ซ่อม: " + ", ".join(f"{k} {v:,}" for k,v in renov.most_common()))
L.append("")
L.append("## 🔎 วิธีค้นหา (ข้อมูลครบ ไม่ตก — ค้นจาก JSON ดิบโดยตรง)")
L.append("ใช้เครื่องมือ `tools/motor/query.py` กรองได้หลายเงื่อนไข (ดู [[T-Broker-โบรกเกอร์|คู่มือ]]):")
L.append("```bash")
L.append('python tools/motor/query.py --brand HONDA --model CIVIC --class 1 --year 2020')
L.append('python tools/motor/query.py --brand TOYOTA --class 2+ --prem-max 15000 --sort prem')
L.append("```")
L.append("")
# ตารางบริษัท × ชั้น
L.append("## 🏢 หมวดหมู่: บริษัทประกัน × ชั้น")
head = "| บริษัท | " + " | ".join(present) + " | **รวม** |"
sep = "|" + "---|"*(len(present)+2)
L.append(head); L.append(sep)
for ins,_ in insurers.most_common():
    row = f"| {short_ins(ins)} | " + " | ".join(f"{ins_cls[ins].get(c,0):,}" for c in present) + f" | **{insurers[ins]:,}** |"
    L.append(row)
L.append("| **รวม** | " + " | ".join(f"**{classes.get(c,0):,}**" for c in present) + f" | **{total:,}** |")
L.append("")
# ชั้น + ช่วงเบี้ย
L.append("## 🛡️ หมวดหมู่: ชั้นประกัน + ช่วงเบี้ยสุทธิ")
L.append("| ชั้น | จำนวน | เบี้ยต่ำสุด | เบี้ยเฉลี่ย | เบี้ยสูงสุด |")
L.append("|------|------:|----------:|----------:|----------:|")
for c in present:
    mn,mx,sm,n = cls_prem[c]
    avg = sm/n if n else 0
    L.append(f"| {c} | {classes[c]:,} | {mn:,.0f} | {avg:,.0f} | {mx:,.0f} |")
L.append("")
# ยี่ห้อ × ชั้น (top 20)
L.append("## 🚙 หมวดหมู่: ยี่ห้อรถ × ชั้น (เรียงตามจำนวน, แสดง 20 อันดับแรก จากทั้งหมด %d ยี่ห้อ)" % len(brands))
head = "| ยี่ห้อ | " + " | ".join(present) + " | **รวม** |"
L.append(head); L.append("|" + "---|"*(len(present)+2))
for brand,_ in brands.most_common(20):
    L.append(f"| {brand} | " + " | ".join(f"{brand_cls[brand].get(c,0):,}" for c in present) + f" | **{brands[brand]:,}** |")
L.append("")
L.append("> ยี่ห้ออื่น ๆ (นอก 20 อันดับ) ค้นได้ผ่าน `query.py --brand <ชื่อ>` เช่น BYD, TESLA, ORA, NETA, LEXUS, KIA ฯลฯ")
L.append("")
L.append("## เชื่อมโยง")
L.append("- ความรู้ชั้นประกัน: [[ประกันรถยนต์]] · เปรียบเทียบ/จับคู่: [[เปรียบเทียบแผน-จับคู่-need]]")
L.append("- โบรกเกอร์/พาร์ทเนอร์: [[T-Broker-โบรกเกอร์]] · ภาพรวมสาย: [[ประเภทประกันวินาศภัย]]")

out_path = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "wiki", "products", "แพคเกจรถยนต์-T-Broker.md"))
with open(out_path, "w", encoding="utf-8") as f:
    f.write("\n".join(L) + "\n")
print("[generated]", out_path, "|", total, "records")
