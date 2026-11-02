using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Implementations
{
    /// <summary>IUserStore บน SQL (EF Core) — แทน JsonUserStore (ADR 0016). Singleton + IDbContextFactory.</summary>
    public class SqlUserStore : IUserStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;

        public SqlUserStore(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public User FindByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            using var db = _factory.CreateDbContext();
            var row = db.Users.AsNoTracking().FirstOrDefault(u => u.Username == username);
            return row == null ? null : ToUser(row);
        }

        public bool TryAddUser(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Username)) return false;
            using var db = _factory.CreateDbContext();
            if (db.Users.Any(u => u.Username == user.Username)) return false;

            var now = DateTime.UtcNow;
            db.Users.Add(new UserRow
            {
                Username = user.Username,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                CustCode = user.CustCode,
                Name = user.Name,
                Email = user.Email,
                CreatedAt = now,
                UpdatedAt = now
            });
            try { db.SaveChanges(); return true; }
            catch (DbUpdateException) { return false; } // กันแข่งกัน insert ชน unique index
        }

        private static User ToUser(UserRow r) => new()
        {
            Username = r.Username, PasswordHash = r.PasswordHash, Role = r.Role,
            CustCode = r.CustCode, Name = r.Name, Email = r.Email
        };
    }
}
