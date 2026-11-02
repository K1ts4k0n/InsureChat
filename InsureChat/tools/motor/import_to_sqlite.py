# -*- coding: utf-8 -*-
"""สคริปต์นำเข้าข้อมูลแพ็กเกจรถยนต์ 330,408 รายการจากโฟลเดอร์ raw/ เข้าสู่ SQLite (insurechat.db)
รันเมื่อต้องการสร้างหรืออัปเดตฐานข้อมูลออฟไลน์ระดับเครื่อง:
  python3 InsureChat/tools/motor/import_to_sqlite.py
"""
import sqlite3, glob, json, os, re, time

base_dir = os.path.dirname(os.path.abspath(__file__))
repo_root = os.path.normpath(os.path.join(base_dir, "..", ".."))
db_path = os.path.join(repo_root, "insurechat-api", "InsureChatBot.Api", "App_Data", "insurechat.db")
raw_dir = os.path.join(repo_root, "InsureChat", "raw")

print(f"Connecting to SQLite: {db_path}...")
os.makedirs(os.path.dirname(db_path), exist_ok=True)
conn = sqlite3.connect(db_path)
cur = conn.cursor()

cur.execute("PRAGMA journal_mode = WAL;")
cur.execute("PRAGMA synchronous = NORMAL;")

print("Creating MotorPackages table schema...")
cur.execute("DROP TABLE IF EXISTS MotorPackages;")
cur.execute("""
CREATE TABLE MotorPackages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RowKey TEXT,
    DocNo TEXT,
    PackageName TEXT,
    InsurerName TEXT,
    BrokerPayId INTEGER,
    PolTypeName TEXT,
    InsuranceClass TEXT,
    SumInsureMax REAL,
    RenovType TEXT,
    CarBrandName TEXT,
    CarModelName TEXT,
    Ccmax TEXT,
    Year INTEGER,
    PremNet REAL,
    PremStm REAL,
    PremType TEXT,
    ComeInCode TEXT,
    ProvinceName TEXT,
    FireSuminsure REAL,
    FloodSuminsure REAL
);
""")

def detect_class(text):
    t = text or ""
    if "2+" in t: return "2+"
    if "3+" in t: return "3+"
    if re.search(r"ประเภท\s*1|ชั้น\s*1|ป\.\s*1", t): return "1"
    if re.search(r"ประเภท\s*2|ชั้น\s*2|ป\.\s*2", t): return "2"
    if re.search(r"ประเภท\s*3|ชั้น\s*3|ป\.\s*3", t): return "3"
    if re.search(r"พ\.?ร\.?บ", t): return "พ.ร.บ."
    return "1"

files = sorted(glob.glob(os.path.join(raw_dir, "ข้อมูลแพคเกจรถยนต์_Page*.json")))
insert_sql = """
INSERT INTO MotorPackages (
    RowKey, DocNo, PackageName, InsurerName, BrokerPayId,
    PolTypeName, InsuranceClass, SumInsureMax, RenovType,
    CarBrandName, CarModelName, Ccmax, Year, PremNet, PremStm,
    PremType, ComeInCode, ProvinceName, FireSuminsure, FloodSuminsure
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
"""

total_rows = 0
start_time = time.time()

for fp in files:
    with open(fp, "r", encoding="utf-8") as f:
        data = json.load(f).get("data", [])
    batch = []
    for r in data:
        pkg_name = (r.get("packageName") or "").strip()
        pol_type = (r.get("polTypeID") or "").strip()
        cls = detect_class(pkg_name + " " + pol_type)
        sum_insure = float(r.get("sumInsureMax") or 0)
        
        if cls == "1":
            fire = sum_insure
            flood = sum_insure
        elif cls == "2+":
            fire = sum_insure
            flood = 100000.0 if ("น้ำท่วม" in pkg_name or "flood" in pkg_name.lower()) else (sum_insure if sum_insure > 0 else 100000.0)
        else:
            fire = 0.0
            flood = 0.0

        batch.append((
            r.get("rowKey"),
            r.get("docNo"),
            pkg_name,
            (r.get("insureID") or "").strip(),
            r.get("brokerPayID"),
            pol_type if pol_type else f"ประกันภัยรถยนต์ชั้น {cls}",
            cls,
            sum_insure,
            (r.get("renovType") or "อู่").strip(),
            (r.get("carBrandName") or "").strip().upper(),
            (r.get("carModelName") or "").strip().upper(),
            str(r.get("ccmax") or ""),
            int(r.get("year") or 0),
            float(r.get("premNet") or 0),
            float(r.get("premStm") or 0),
            r.get("premType"),
            r.get("comeInCode"),
            r.get("provinName"),
            fire,
            flood
        ))
    cur.executemany(insert_sql, batch)
    total_rows += len(batch)
    print(f"Loaded {os.path.basename(fp)}: {len(batch)} rows (Total: {total_rows})")

print("Creating SQL indexes for fast filtering...")
cur.execute("CREATE INDEX idx_mp_brand ON MotorPackages(CarBrandName);")
cur.execute("CREATE INDEX idx_mp_brand_model ON MotorPackages(CarBrandName, CarModelName);")
cur.execute("CREATE INDEX idx_mp_brand_model_year ON MotorPackages(CarBrandName, CarModelName, Year);")
cur.execute("CREATE INDEX idx_mp_class ON MotorPackages(InsuranceClass);")
cur.execute("CREATE INDEX idx_mp_prem ON MotorPackages(PremNet);")
cur.execute("CREATE INDEX idx_mp_sum ON MotorPackages(SumInsureMax);")

conn.commit()
conn.close()

elapsed = time.time() - start_time
print(f"DONE! Successfully imported {total_rows:,} records into SQLite in {elapsed:.2f} seconds.")
