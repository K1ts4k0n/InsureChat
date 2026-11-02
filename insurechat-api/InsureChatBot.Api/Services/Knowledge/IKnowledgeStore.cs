using InsureChatBot.Api.Data.Entities;

namespace InsureChatBot.Api.Services.Knowledge
{
    /// <summary>sync วิกิ (file→DB) + อ่านบทความ (ADR 0016 Phase 3). ไฟล์ = source of truth.</summary>
    public interface IKnowledgeStore
    {
        /// <summary>สแกน wiki/*.md → upsert ตาม Slug (เทียบ ContentHash). คืนจำนวนที่ sync.</summary>
        int Sync();
        List<KnowledgeArticleRow> List();
        KnowledgeArticleRow Get(string slug);
    }
}
