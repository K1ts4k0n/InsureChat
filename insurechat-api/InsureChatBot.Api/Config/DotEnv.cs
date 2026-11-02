namespace InsureChatBot.Api.Config
{
    /// <summary>
    /// โหลดไฟล์ .env (KEY=VALUE) เข้าเป็น environment variables — ASP.NET ไม่อ่าน .env เอง.
    /// เดินขึ้น parent dir จาก startDir เพื่อหา .env (เพราะ .env อยู่ที่ InsureChat root, แอปรันใน app/api).
    /// ไม่ override ค่าที่ตั้งไว้ใน env จริงอยู่แล้ว.
    /// </summary>
    public static class DotEnv
    {
        public static void LoadFrom(string startDir, int maxUp = 4)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i <= maxUp && dir != null; i++, dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, ".env");
                if (File.Exists(path)) { Parse(path); return; }
            }
        }

        private static void Parse(string path)
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.StartsWith("export ")) line = line[7..];

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line[..eq].Trim();
                var val = line[(eq + 1)..].Trim();
                // ตัด quote ครอบถ้ามี
                if (val.Length >= 2 && ((val[0] == '"' && val[^1] == '"') || (val[0] == '\'' && val[^1] == '\'')))
                    val = val[1..^1];

                if (key.Length > 0 && Environment.GetEnvironmentVariable(key) == null)
                    Environment.SetEnvironmentVariable(key, val);
            }
        }
    }
}
