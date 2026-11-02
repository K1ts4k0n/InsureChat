using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Implementations;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class SqlUserStoreTests
    {
        private static User MakeUser(string username = "admin") => new()
        {
            Username = username, PasswordHash = "h", Role = UserRoles.Admin,
            CustCode = "ADMIN001", Name = "ผู้ดูแลระบบ", Email = "a@b.c"
        };

        [Fact]
        public void FindByUsername_returns_null_when_absent()
        {
            using var f = new TestDbContextFactory();
            var store = new SqlUserStore(f);
            Assert.Null(store.FindByUsername("admin"));
        }

        [Fact]
        public void TryAddUser_then_FindByUsername_roundtrips()
        {
            using var f = new TestDbContextFactory();
            var store = new SqlUserStore(f);

            Assert.True(store.TryAddUser(MakeUser()));
            var found = store.FindByUsername("admin");

            Assert.NotNull(found);
            Assert.Equal("admin", found.Username);
            Assert.Equal(UserRoles.Admin, found.Role);
            Assert.Equal("ADMIN001", found.CustCode);
        }

        [Fact]
        public void TryAddUser_returns_false_on_duplicate_username()
        {
            using var f = new TestDbContextFactory();
            var store = new SqlUserStore(f);

            Assert.True(store.TryAddUser(MakeUser()));
            Assert.False(store.TryAddUser(MakeUser()));
        }

        [Fact]
        public void TryAddUser_returns_false_on_blank_username()
        {
            using var f = new TestDbContextFactory();
            var store = new SqlUserStore(f);
            Assert.False(store.TryAddUser(new User { Username = "  " }));
        }
    }
}
