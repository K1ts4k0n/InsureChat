using InsureChatBot.Api.Models.Auth;

namespace InsureChatBot.Api.Services.Interfaces
{
    /// <summary>
    /// ที่เก็บ user — ตอนนี้ JSON (JsonUserStore), อนาคต SQL Server (SqlUserStore).
    /// ทุกการอ่าน user ต้องผ่าน interface นี้เท่านั้น เพื่อให้ย้าย backend ได้สะอาด (ADR 0002).
    /// </summary>
    public interface IUserStore
    {
        User FindByUsername(string username);

        /// <summary>เพิ่ม user ใหม่ (คืน false ถ้า username ซ้ำ). JSON เขียนลงไฟล์, SQL ภายหลังลง DB.</summary>
        bool TryAddUser(User user);
    }
}
