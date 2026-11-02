using InsureChatBot.Api.Config;
using InsureChatBot.Api.Services.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class SqlAppSettingsStoreTests
    {
        private static SqlAppSettingsStore NewStore(TestDbContextFactory f, string envApiKey = "", string envPassword = "")
        {
            var llm = Options.Create(new LlmSettings { ApiKey = envApiKey, Model = "base-model", BaseUrl = "https://base", Enabled = true });
            var email = Options.Create(new OrderEmailSettings { Password = envPassword, Username = "u@g.com", To = "to@g.com" });
            return new SqlAppSettingsStore(f, llm, email);
        }

        [Fact]
        public void Get_returns_default_theme_when_empty()
        {
            using var f = new TestDbContextFactory();
            var store = NewStore(f);
            Assert.Equal("#2563eb", store.Get().Theme.Brand);
        }

        [Fact]
        public void Update_persists_theme_and_persona()
        {
            using var f = new TestDbContextFactory();
            var store = NewStore(f);

            store.Update(s => { s.Theme.Brand = "#16a34a"; s.Persona = "โทนสุภาพ"; });

            var store2 = NewStore(f);
            Assert.Equal("#16a34a", store2.Get().Theme.Brand);
            Assert.Equal("โทนสุภาพ", store2.Get().Persona);
        }

        [Fact]
        public void GetKeySource_is_env_when_env_key_set_and_none_otherwise()
        {
            using var f = new TestDbContextFactory();
            Assert.Equal("env", NewStore(f, envApiKey: "sk-xxx").GetKeySource());
            Assert.Equal("none", NewStore(f, envApiKey: "").GetKeySource());
        }

        [Fact]
        public void Secret_set_via_update_is_ignored_env_wins()
        {
            using var f = new TestDbContextFactory();
            var store = NewStore(f, envApiKey: "sk-env");

            store.Update(s => { s.Llm.ApiKey = "sk-admin-should-be-ignored"; });

            Assert.Equal("sk-env", store.GetEffectiveLlm().ApiKey);
            Assert.Equal("env", store.GetKeySource());
        }
    }
}
