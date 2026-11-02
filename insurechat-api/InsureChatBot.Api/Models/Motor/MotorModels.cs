namespace InsureChatBot.Api.Models.Motor
{
    /// <summary>แพคเกจรถ 1 รายการที่ค้นได้จาก query.py (ผ่าน MotorQueryService)</summary>
    public class MotorPackageDto
    {
        public string InsurerId { get; set; }
        public string InsurerCode { get; set; }
        public string InsurerName { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public decimal SumInsure { get; set; }
        public decimal Premium { get; set; }
        public decimal Tax { get; set; }
        public decimal Stamp { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>ข้อมูลที่ใช้ประกอบ proposal JSON ไปยัง T Broker</summary>
    public class MotorProposalData
    {
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public decimal SumInsure { get; set; }
        public string InsurerId { get; set; }
        public string InsurerCode { get; set; }
        public string InsurerName { get; set; }
        public decimal Premium { get; set; }
        public decimal Tax { get; set; }
        public decimal Stamp { get; set; }
        public decimal Total { get; set; }

        // ข้อมูลผู้เอาประกัน/รถ สำหรับ T Broker live (ว่างได้ในโหมด mock)
        public string Phone { get; set; }
        public string IdCard { get; set; }
        public string LicensePlate { get; set; }
    }

    /// <summary>รายละเอียดความคุ้มครองเต็มของแพคเกจ 1 รายการ (modal "ดูรายละเอียด") — ข้อมูลจริงจาก PackageMotor</summary>
    public class MotorPackageDetail
    {
        public int PackageId { get; set; }
        public string InsurerName { get; set; }
        public string InsurerLogo { get; set; }
        public string PackageName { get; set; }
        public string PolTypeName { get; set; }    // ชั้นประกัน
        public string CarUsage { get; set; }       // การใช้งานรถ
        public string RenovType { get; set; }      // อู่ | ห้าง
        public decimal SumInsure { get; set; }     // ทุนประกัน (ตัวรถ)
        public decimal Deductible { get; set; }    // ค่าเสียหายส่วนแรก (OwnAmtDeduc)
        public decimal PremNet { get; set; }
        public decimal PremTotal { get; set; }     // เบี้ยหลังส่วนลด

        // ความคุ้มครองตัวรถ
        public decimal FireTheft { get; set; }     // สูญหาย/ไฟไหม้
        public decimal Flood { get; set; }         // อุทกภัย
        public decimal Terrorism { get; set; }     // ภัยก่อการร้าย

        // ความรับผิดต่อบุคคลภายนอก
        public decimal ThirdBodilyPers { get; set; }    // ชีวิต/ร่างกาย ต่อคน
        public decimal ThirdBodilyAccid { get; set; }   // ต่อครั้ง
        public decimal ThirdProperty { get; set; }      // ทรัพย์สินคู่กรณี/ครั้ง (ThirdCtpl)
        public decimal ThirdPropertyDeduc { get; set; } // ค่าเสียหายส่วนแรกทรัพย์สิน (ThirdAmtDeduc)

        // ความคุ้มครองบุคคลในรถ
        public int BodilyDrivCount { get; set; }
        public decimal BodilyDrivCover { get; set; }    // ค่าอุบัติเหตุ /คน
        public int BodilyPassCount { get; set; }
        public decimal BodilyPassCover { get; set; }
        public int MedicalPers { get; set; }
        public decimal MedicalBaht { get; set; }        // ค่ารักษาพยาบาล /คน
        public decimal BailBondIns { get; set; }        // ค่าประกันตัวผู้ขับขี่
    }

    public class ProposalSubmissionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TransactionId { get; set; }
        public string PolicyNumber { get; set; }
        public string ErrorDetails { get; set; }
    }
}
