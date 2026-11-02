# -*- coding: utf-8 -*-
"""ค้นหาแพคเกจประกันรถยนต์จากชุดข้อมูล T Broker (330,408 แพคเกจ)

ตัวอย่าง:
  python query.py --brand HONDA --model CIVIC --class 1 --year 2020
  python query.py --brand TOYOTA --class 2+ --insurer ธนชาต --prem-max 15000 --sort prem
  python query.py --brand BENZ --sum-min 1000000 --limit 30 --json
"""
import argparse, sys, json
import motorlib as M
sys.stdout.reconfigure(encoding="utf-8")

CLASS_ALIAS = {"1":"ชั้น 1","2":"ชั้น 2","3":"ชั้น 3","2+":"ชั้น 2+","3+":"ชั้น 3+",
               "ชั้น1":"ชั้น 1","ป1":"ชั้น 1"}

def norm_class(c):
    if not c: return None
    c = c.strip()
    return CLASS_ALIAS.get(c.replace("ชั้น ","").replace(" ",""), c if c.startswith("ชั้น") else "ชั้น "+c)

def main():
    ap = argparse.ArgumentParser(description="ค้นหาแพคเกจรถยนต์ T Broker")
    ap.add_argument("--brand"); ap.add_argument("--model")
    ap.add_argument("--class", dest="klass", help="1 / 2+ / 3+ / 2 / 3")
    ap.add_argument("--insurer", help="ชื่อบริษัท (บางส่วน) เช่น ธนชาต / เมืองไทย / วิริยะ / โตเกียว")
    ap.add_argument("--year", type=int); ap.add_argument("--year-min", type=int); ap.add_argument("--year-max", type=int)
    ap.add_argument("--renov", help="อู่ / ห้าง")
    ap.add_argument("--sum-min", type=float); ap.add_argument("--sum-max", type=float)
    ap.add_argument("--prem-max", type=float); ap.add_argument("--prem-min", type=float)
    ap.add_argument("--sort", choices=["prem","sum","year"], default="prem")
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args()
    want_class = norm_class(a.klass)

    out = []
    for r in M.iter_records():
        if a.brand and a.brand.upper() not in (r.get("carBrandName") or "").upper(): continue
        if a.model and a.model.upper() not in (r.get("carModelName") or "").upper(): continue
        if want_class and M.record_class(r) != want_class: continue
        if a.insurer and a.insurer not in (r.get("insureID") or ""): continue
        if a.renov and a.renov not in (r.get("renovType") or ""): continue
        y = r.get("year") or 0
        if a.year and y != a.year: continue
        if a.year_min and y < a.year_min: continue
        if a.year_max and y > a.year_max: continue
        si = r.get("sumInsureMax") or 0
        if a.sum_min and si < a.sum_min: continue
        if a.sum_max and si > a.sum_max: continue
        pn = r.get("premNet") or 0
        if a.prem_max and pn > a.prem_max: continue
        if a.prem_min and pn < a.prem_min: continue
        out.append(r)

    key = {"prem":"premNet","sum":"sumInsureMax","year":"year"}[a.sort]
    out.sort(key=lambda r: r.get(key) or 0)
    total = len(out)
    shown = out[:a.limit]

    if a.json:
        print(json.dumps({"matches": total, "results": shown}, ensure_ascii=False, indent=2)); return

    print(f"พบ {total:,} แพคเกจที่ตรงเงื่อนไข (แสดง {len(shown)} เรียงตาม {a.sort})\n")
    hdr = f'{"บริษัท":<14}{"ชั้น":<7}{"ยี่ห้อ/รุ่น":<22}{"ปี":<6}{"ทุน":>11}{"เบี้ยสุทธิ":>11}{"ซ่อม":>6}'
    print(hdr); print("-"*len(hdr))
    for r in shown:
        ins = (r.get("insureID") or "")[:12]
        cls = M.record_class(r).replace("ชั้น ","")
        car = ((r.get("carBrandName") or "")+" "+(r.get("carModelName") or "")).strip()[:21]
        print(f'{ins:<14}{cls:<7}{car:<22}{str(r.get("year") or "-"):<6}'
              f'{(r.get("sumInsureMax") or 0):>11,.0f}{(r.get("premNet") or 0):>11,.0f}{(r.get("renovType") or ""):>6}')
    if total > a.limit:
        print(f"\n... อีก {total-a.limit:,} รายการ (ใช้ --limit เพิ่ม หรือกรองให้แคบลง)")

if __name__ == "__main__":
    main()
