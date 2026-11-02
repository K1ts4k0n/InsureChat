# -*- coding: utf-8 -*-
"""ไลบรารีกลางสำหรับชุดข้อมูลแพคเกจรถยนต์ T Broker (โหลด + จัดหมวด)
ใช้ร่วมกันโดย explore.py / query.py / build_catalog.py เพื่อให้ตรรกะการจัดชั้นตรงกัน"""
import json, glob, os, re

RAW = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "raw"))

def data_files():
    return sorted(glob.glob(os.path.join(RAW, "ข้อมูลแพคเกจรถยนต์_Page*.json")))

def iter_records():
    """อ่านทีละไฟล์ (ประหยัดหน่วยความจำ) แล้ว yield ทีละ record"""
    for fp in data_files():
        with open(fp, encoding="utf-8") as f:
            obj = json.load(f)
        for r in obj.get("data", []):
            yield r
        del obj

def detect_class(text):
    """แปลงชื่อแพคเกจ → ชั้นประกันมาตรฐาน (รองรับทั้ง 'ประเภท 1' และย่อ 'ป.1')"""
    t = text or ""
    if "2+" in t: return "ชั้น 2+"
    if "3+" in t: return "ชั้น 3+"
    if re.search(r"ประเภท\s*1|ชั้น\s*1|ป\.\s*1", t): return "ชั้น 1"
    if re.search(r"ประเภท\s*2|ชั้น\s*2|ป\.\s*2", t): return "ชั้น 2"
    if re.search(r"ประเภท\s*3|ชั้น\s*3|ป\.\s*3", t): return "ชั้น 3"
    if re.search(r"พ\.?ร\.?บ", t): return "พ.ร.บ."
    return "อื่น ๆ/ไม่ระบุ"

def record_class(r):
    return detect_class((r.get("packageName") or "") + " " + (r.get("polTypeID") or ""))
