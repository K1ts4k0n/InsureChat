using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Implementations
{
    /// <summary>
    /// อ่าน/เขียน user จากไฟล์ JSON (cache ในหน่วยความจำ). ที่เก็บชั่วคราวก่อนย้ายไป SQL Server (ADR 0002).
    /// </summary>
    public class JsonUserStore : IUserStore
    {
        private readonly Dictionary<string, User> _users;
        private readonly string _path;
        private readonly object _lock = new();
        private static readonly JsonSerializerOptions JsonOpts =
            new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

        public JsonUserStore(IOptions<UserStoreSettings> options, IWebHostEnvironment env)
        {
            var path = options.Value?.JsonPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(env.ContentRootPath, "Data", "users.json");
            _path = path;

            _users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(_path))
                throw new FileNotFoundException($"ไม่พบไฟล์ user store: {_path}");

            var list = JsonSerializer.Deserialize<List<User>>(File.ReadAllText(_path), JsonOpts)
                       ?? new List<User>();
            foreach (var u in list)
                if (!string.IsNullOrWhiteSpace(u.Username))
                    _users[u.Username] = u;
        }

        public User FindByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            lock (_lock)
                return _users.TryGetValue(username, out var u) ? u : null;
        }

        public bool TryAddUser(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Username)) return false;
            lock (_lock)
            {
                if (_users.ContainsKey(user.Username)) return false;
                _users[user.Username] = user;
                File.WriteAllText(_path, JsonSerializer.Serialize(_users.Values, JsonOpts));
                return true;
            }
        }
    }
}
