namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>mirror วิกิ 1 หน้า (file→DB sync ทางเดียว) — ADR 0016 Phase 3. ไฟล์ markdown = source of truth.</summary>
    public class KnowledgeArticleRow
    {
        public long Id { get; set; }
        public string Slug { get; set; }        // path สัมพัทธ์ไม่รวม .md เช่น "concepts/ประกันรถยนต์" (unique)
        public string Title { get; set; }
        public string Type { get; set; }        // concept | playbook | source | product | ...
        public string Content { get; set; }     // markdown body (หลัง frontmatter)
        public string Tags { get; set; }        // csv
        public string Aliases { get; set; }     // csv
        public string SourceFile { get; set; }  // path สัมพัทธ์รวม .md
        public string ContentHash { get; set; } // SHA256 hex ของไฟล์เต็ม (ตรวจ change)
        public bool IsInjected { get; set; }    // เป็น 1 ใน 6 ไฟล์ที่ป้อน prompt ไหม
        public DateTime UpdatedAt { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
