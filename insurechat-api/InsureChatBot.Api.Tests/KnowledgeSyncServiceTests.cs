using InsureChatBot.Api.Services.Knowledge;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class KnowledgeSyncServiceTests
    {
        private const string Sample =
            "---\n" +
            "type: concept\n" +
            "title: ประกันรถยนต์ (ชั้น 1/2+/3+)\n" +
            "aliases: [ประกันรถ, ชั้น 1, motor]\n" +
            "tags: [concept, motor]\n" +
            "sources: [S-007]\n" +
            "---\n" +
            "\n# ประกันรถยนต์\n\nเนื้อหา body ตรงนี้\n";

        [Fact]
        public void Parse_extracts_frontmatter_and_body()
        {
            var p = KnowledgeSyncService.ParseFrontmatter(Sample);
            Assert.Equal("concept", p.Type);
            Assert.Equal("ประกันรถยนต์ (ชั้น 1/2+/3+)", p.Title);
            Assert.Equal("ประกันรถ, ชั้น 1, motor", p.Aliases);
            Assert.Equal("concept, motor", p.Tags);
            Assert.Contains("# ประกันรถยนต์", p.Body);
            Assert.DoesNotContain("type: concept", p.Body);
        }

        [Fact]
        public void Parse_handles_missing_frontmatter()
        {
            var p = KnowledgeSyncService.ParseFrontmatter("# หัวข้อ\nเนื้อหา");
            Assert.Null(p.Type);
            Assert.Contains("# หัวข้อ", p.Body);
        }

        [Fact]
        public void Hash_is_stable_and_changes_with_content()
        {
            Assert.Equal(KnowledgeSyncService.Hash("abc"), KnowledgeSyncService.Hash("abc"));
            Assert.NotEqual(KnowledgeSyncService.Hash("abc"), KnowledgeSyncService.Hash("abd"));
        }
    }
}
