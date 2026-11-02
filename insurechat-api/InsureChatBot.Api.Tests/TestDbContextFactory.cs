using InsureChatBot.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Tests
{
    /// <summary>IDbContextFactory บน SQLite in-memory สำหรับ unit test (schema สร้างด้วย EnsureCreated).</summary>
    public sealed class TestDbContextFactory : IDbContextFactory<InsureChatDbContext>, IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly DbContextOptions<InsureChatDbContext> _options;

        public TestDbContextFactory()
        {
            _conn = new SqliteConnection("DataSource=:memory:");
            _conn.Open(); // ต้องเปิดค้างไว้ ไม่งั้น in-memory DB หาย
            _options = new DbContextOptionsBuilder<InsureChatDbContext>()
                .UseSqlite(_conn)
                .Options;
            using var db = new InsureChatDbContext(_options);
            db.Database.EnsureCreated();
        }

        public InsureChatDbContext CreateDbContext() => new(_options);

        public void Dispose() => _conn.Dispose();
    }
}
