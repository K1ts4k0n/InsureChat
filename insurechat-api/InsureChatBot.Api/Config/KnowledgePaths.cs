using Microsoft.Extensions.Configuration;

namespace InsureChatBot.Api.Config
{
    /// <summary>
    /// resolve โฟลเดอร์ความรู้ (markdown) ที่ bundle มากับ api — ใช้ร่วมโดย
    /// SystemPromptProvider (inject 6 ไฟล์) และ KnowledgeSyncService (sync → DB).
    /// ลำดับ: env KNOWLEDGE_DIR → config "Knowledge:Dir" → default "Knowledge" (relative ต่อ ContentRoot).
    /// แทน FindWikiDir เดิมที่ไต่ parent หา wiki (เลิกพึ่ง vault — ADR 0017).
    /// </summary>
    public static class KnowledgePaths
    {
        public static string Resolve(string contentRootPath, IConfiguration config)
        {
            var dir = System.Environment.GetEnvironmentVariable("KNOWLEDGE_DIR");
            if (string.IsNullOrWhiteSpace(dir)) dir = config?["Knowledge:Dir"];
            if (string.IsNullOrWhiteSpace(dir)) dir = "Knowledge";
            return Path.IsPathRooted(dir) ? dir : Path.Combine(contentRootPath, dir);
        }
    }
}
