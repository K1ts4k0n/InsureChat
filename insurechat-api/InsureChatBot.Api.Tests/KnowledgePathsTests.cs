using InsureChatBot.Api.Config;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class KnowledgePathsTests
    {
        private static IConfiguration Empty() => new ConfigurationBuilder().Build();

        [Fact]
        public void Resolve_defaults_to_Knowledge_under_contentroot()
        {
            System.Environment.SetEnvironmentVariable("KNOWLEDGE_DIR", null);
            var result = KnowledgePaths.Resolve(@"C:\app", Empty());
            Assert.Equal(System.IO.Path.Combine(@"C:\app", "Knowledge"), result);
        }

        [Fact]
        public void Resolve_uses_absolute_env_override()
        {
            System.Environment.SetEnvironmentVariable("KNOWLEDGE_DIR", @"D:\kb");
            try { Assert.Equal(@"D:\kb", KnowledgePaths.Resolve(@"C:\app", Empty())); }
            finally { System.Environment.SetEnvironmentVariable("KNOWLEDGE_DIR", null); }
        }
    }
}
