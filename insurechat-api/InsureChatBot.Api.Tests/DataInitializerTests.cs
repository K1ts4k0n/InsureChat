using InsureChatBot.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class DataInitializerTests
    {
        /// <summary>factory ที่ต่อ DB ไม่ได้ — จำลองสภาพบนเซิร์ฟเวอร์ที่ .env หาย หรือ SQL ล่ม</summary>
        private sealed class ThrowingDbContextFactory : IDbContextFactory<InsureChatDbContext>
        {
            public InsureChatDbContext CreateDbContext()
                => throw new InvalidOperationException("connection string is not configured");
        }

        [Fact]
        public void TryRun_WhenDbUnavailable_ReturnsFalseAndDoesNotThrow()
        {
            var ok = DataInitializer.TryRun(
                new ThrowingDbContextFactory(),
                AppContext.BaseDirectory,
                NullLogger.Instance);

            Assert.False(ok);
        }
    }
}
