# แยก api/web เป็น repo คนละตัว — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** แยกโค้ด standalone chatbot ออกจาก Obsidian vault เป็น **2 git repo อิสระ** (`insurechat-api`, `insurechat-web`) ที่ส่งให้ dev คนอื่นได้ โดยไม่รั่ว PII/secret และ api ทำงานได้เองโดยไม่ต้องมี vault

**Architecture:** ย้ายไฟล์เป็น sibling 3 โฟลเดอร์ (`InsureChat/` vault คงเดิม, `insurechat-api/`, `insurechat-web/`). api เลิกอ่าน `wiki/` ตอน runtime → bundle ไฟล์ความรู้ (ไม่มี PII) เข้า `Knowledge/` แทน; ดึง secret (รหัส DB, JWT, MotorTool path) ออกจากไฟล์ที่ commit ไปเป็น env. **scrub secret เสร็จก่อน `git init`** แล้วค่อย fresh-init repo ใหม่ (ไม่พา history เก่าที่มี secret ไปด้วย)

**Tech Stack:** .NET 10 (ASP.NET Core), xUnit, Angular 21, Git (git bash บน Windows)

**อ้างอิง:** [spec](../specs/2026-06-25-split-api-web-repos-design.md) · [ADR 0017](../../adr/0017-split-api-web-into-separate-repos.md)

---

## ลำดับสำคัญ (อ่านก่อนเริ่ม)

ทุก Task 1–4 ทำบนไฟล์ที่ **copy ไปไว้ใน `c:\CTD\ChatBot\insurechat-api\` แล้ว แต่ยัง `git init`** —
เพื่อให้ commit แรกของ api repo **สะอาดจาก secret**. อย่า `git init`/commit จนกว่าจะถึง Task 5.
vault repo (`InsureChat/`) ไม่ต้อง commit อะไรจนถึง Task 7.

> path ในแผนนี้ใช้รูปแบบ git bash (`/c/CTD/ChatBot/...`). ทุก `dotnet`/`npm` รันจาก path ที่ระบุใน Task

---

## ไฟล์ที่จะสร้าง/แก้ (decomposition)

| ไฟล์ | หน้าที่ | Task |
|------|---------|------|
| `insurechat-api/InsureChatBot.Api/Config/KnowledgePaths.cs` | **ใหม่** — resolve โฟลเดอร์ความรู้จุดเดียว (แทน `FindWikiDir` ที่ซ้ำ 2 ที่) | 2 |
| `.../Services/Conversation/SystemPromptProvider.cs` | แก้ ctor ใช้ `KnowledgePaths`; ลบ `FindWikiDir` | 2 |
| `.../Services/Knowledge/KnowledgeSyncService.cs` | แก้ ctor ใช้ `KnowledgePaths`; ลบ `FindWikiDir` | 2 |
| `insurechat-api/InsureChatBot.Api.Tests/KnowledgePathsTests.cs` | **ใหม่** — test path resolution | 2 |
| `.../Services/Implementations/MotorQueryService.cs` | degrade เงียบถ้าไม่พบ Python script | 4 |
| `insurechat-api/InsureChatBot.Api/Knowledge/**` | **ใหม่** — corpus ความรู้ที่ bundle (ไม่มี PII) | 3 |
| `.../InsureChatBot.Api.csproj` | copy `Knowledge/**` → output; แก้ path `.env` | 3,5 |
| `.../appsettings.json` | ลบ secret (DB/JWT/MotorTool) → `""` | 4 |
| `.../Program.cs` | อ่าน secret จาก env (EF/JWT/Motor) | 4 |
| `insurechat-api/.env.example`,`.env`,`.gitignore`,`README.md`,`InsureChatBot.sln` | **ใหม่** — repo scaffold | 4,5 |
| `insurechat-web/**` + `.gitignore`,`README.md` | **ใหม่** — web repo | 6 |
| `InsureChat/.gitignore`,`CONTEXT.md` | ลบ `app/` paths; แก้ลิงก์เอกสาร | 7 |

---

## Task 1: ย้ายไฟล์ api ไปโฟลเดอร์ใหม่ (ยังไม่ git)

**Files:**
- Create: `c:\CTD\ChatBot\insurechat-api\InsureChatBot.Api\` (จาก `InsureChat/app/api`)
- Create: `c:\CTD\ChatBot\insurechat-api\InsureChatBot.Api.Tests\` (จาก `InsureChat/app/api.Tests`)
- Modify: `insurechat-api/InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj` (ProjectReference)
- Modify: `insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj` (path `.env`)

- [ ] **Step 1: copy ไฟล์ + ตัด artifact/secret runtime ทิ้ง**

```bash
cd /c/CTD/ChatBot
mkdir -p insurechat-api
cp -r InsureChat/app/api insurechat-api/InsureChatBot.Api
cp -r InsureChat/app/api.Tests insurechat-api/InsureChatBot.Api.Tests
# ตัด build artifact + runtime secret ที่ไม่ควรติดไป
rm -rf insurechat-api/InsureChatBot.Api/bin insurechat-api/InsureChatBot.Api/obj \
       insurechat-api/InsureChatBot.Api/.vs insurechat-api/InsureChatBot.Api/publish \
       insurechat-api/InsureChatBot.Api.Tests/bin insurechat-api/InsureChatBot.Api.Tests/obj
rm -f  insurechat-api/InsureChatBot.Api/Data/app-settings.json \
       insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj.user
```

- [ ] **Step 2: แก้ ProjectReference ใน test csproj**

แก้ `insurechat-api/InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj`:

```xml
<!-- เดิม -->
<ProjectReference Include="..\api\InsureChatBot.Api.csproj" />
<!-- เป็น -->
<ProjectReference Include="..\InsureChatBot.Api\InsureChatBot.Api.csproj" />
```

- [ ] **Step 3: แก้ path `.env` ใน api csproj**

ใน `insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj` เปลี่ยน include `.env` ให้ชี้ repo root (1 ชั้น) แทน 2 ชั้น:

```xml
<!-- เดิม: .env อยู่ที่ InsureChat root (../../) -->
<None Include="$(MSBuildProjectDirectory)\..\..\.env" Link=".env"
      Condition="Exists('$(MSBuildProjectDirectory)\..\..\.env')">
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
<!-- เป็น: .env อยู่ที่ insurechat-api root (../) -->
<None Include="$(MSBuildProjectDirectory)\..\.env" Link=".env"
      Condition="Exists('$(MSBuildProjectDirectory)\..\.env')">
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

- [ ] **Step 4: build + test ให้เขียว (ยังไม่แตะ logic)**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj
dotnet test  InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj
```
Expected: build SUCCESS, test PASS (3 tests เดิมใน `KnowledgeSyncServiceTests` ผ่าน). ยังไม่ commit.

> หมายเหตุ: ตอนนี้ `FindWikiDir` หา wiki ไม่เจอ (vault ไม่ใช่ parent แล้ว) — knowledge injection จะว่าง
> แต่ build/test ยังเขียว เพราะเทสต์ไม่ได้เช็คการ inject. จะแก้ใน Task 2–3

---

## Task 2: รวม path ความรู้จุดเดียว + เลิกพึ่ง wiki parent (TDD)

**Files:**
- Create: `insurechat-api/InsureChatBot.Api/Config/KnowledgePaths.cs`
- Create: `insurechat-api/InsureChatBot.Api.Tests/KnowledgePathsTests.cs`
- Modify: `.../Services/Conversation/SystemPromptProvider.cs`
- Modify: `.../Services/Knowledge/KnowledgeSyncService.cs`

- [ ] **Step 1: เขียน test ที่ fail ก่อน**

สร้าง `insurechat-api/InsureChatBot.Api.Tests/KnowledgePathsTests.cs`:

```csharp
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
```

- [ ] **Step 2: รัน test ให้เห็นว่า fail**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet test InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj
```
Expected: FAIL — compile error "KnowledgePaths does not exist"

- [ ] **Step 3: สร้าง `KnowledgePaths`**

สร้าง `insurechat-api/InsureChatBot.Api/Config/KnowledgePaths.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace InsureChatBot.Api.Config
{
    /// <summary>
    /// resolve โฟลเดอร์ความรู้ (markdown) ที่ bundle มากับ api — ใช้ร่วมโดย
    /// SystemPromptProvider (inject 6 ไฟล์) และ KnowledgeSyncService (sync → DB).
    /// ลำดับ: env KNOWLEDGE_DIR → config "Knowledge:Dir" → default "Knowledge" (relative ต่อ ContentRoot).
    /// แทน FindWikiDir เดิมที่ไต่ parent หา wiki (เลิกพึ่ง vault — ADR 0017).
    /// </summary>
    public static class KnowledgePaths
    {
        public static string Resolve(string contentRootPath, IConfiguration config)
        {
            var dir = System.Environment.GetEnvironmentVariable("KNOWLEDGE_DIR");
            if (string.IsNullOrWhiteSpace(dir)) dir = config?["Knowledge:Dir"];
            if (string.IsNullOrWhiteSpace(dir)) dir = "Knowledge";
            return Path.IsPathRooted(dir) ? dir : Path.Combine(contentRootPath, dir);
        }
    }
}
```

- [ ] **Step 4: รัน test ให้ผ่าน**

```bash
dotnet test InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj --filter KnowledgePathsTests
```
Expected: PASS (2 tests)

- [ ] **Step 5: แก้ `SystemPromptProvider` ใช้ `KnowledgePaths` + ลบ `FindWikiDir`**

ใน `.../Services/Conversation/SystemPromptProvider.cs`:

(ก) เพิ่ม using ใต้บรรทัด `using InsureChatBot.Api.Services.Settings;`:
```csharp
using InsureChatBot.Api.Config;
using Microsoft.Extensions.Configuration;
```

(ข) แก้ constructor — เพิ่มพารามิเตอร์ `IConfiguration config` และเปลี่ยนการหา dir:
```csharp
// เดิม
public SystemPromptProvider(IAppSettingsStore store, IWebHostEnvironment env, ILogger<SystemPromptProvider> logger)
{
    _store = store;
    var wikiDir = FindWikiDir(env.ContentRootPath);
    var knowledge = new StringBuilder();
    if (wikiDir != null)
    {
        foreach (var rel in KnowledgeFiles)
        {
            var path = Path.Combine(wikiDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                knowledge.AppendLine($"\n===== {rel} =====");
                knowledge.AppendLine(File.ReadAllText(path));
            }
            else logger.LogWarning("System prompt: ไม่พบไฟล์วิกิ {Path}", path);
        }
    }
    else logger.LogWarning("System prompt: หาโฟลเดอร์ wiki ไม่เจอจาก {Root}", env.ContentRootPath);

    _knowledgeBlock = "# วิธีการขาย (ใช้เป็นแนวทาง)\n" + knowledge;
    _mechanicsBlock = LockedMechanics;
}
```
```csharp
// เป็น
public SystemPromptProvider(IAppSettingsStore store, IWebHostEnvironment env, IConfiguration config, ILogger<SystemPromptProvider> logger)
{
    _store = store;
    var knowledgeDir = KnowledgePaths.Resolve(env.ContentRootPath, config);
    var knowledge = new StringBuilder();
    if (Directory.Exists(knowledgeDir))
    {
        foreach (var rel in KnowledgeFiles)
        {
            var path = Path.Combine(knowledgeDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                knowledge.AppendLine($"\n===== {rel} =====");
                knowledge.AppendLine(File.ReadAllText(path));
            }
            else logger.LogWarning("System prompt: ไม่พบไฟล์ความรู้ {Path}", path);
        }
    }
    else logger.LogWarning("System prompt: หาโฟลเดอร์ Knowledge ไม่เจอจาก {Dir}", knowledgeDir);

    _knowledgeBlock = "# วิธีการขาย (ใช้เป็นแนวทาง)\n" + knowledge;
    _mechanicsBlock = LockedMechanics;
}
```

(ค) ลบเมธอด `FindWikiDir` ทั้งก้อน (บรรทัด ~119-128):
```csharp
private static string FindWikiDir(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    for (int i = 0; i <= 4 && dir != null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "wiki");
        if (Directory.Exists(candidate)) return candidate;
    }
    return null;
}
```

- [ ] **Step 6: แก้ `KnowledgeSyncService` ใช้ `KnowledgePaths` + ลบ `FindWikiDir`**

ใน `.../Services/Knowledge/KnowledgeSyncService.cs`:

(ก) เพิ่ม using ใต้ `using Microsoft.EntityFrameworkCore;`:
```csharp
using InsureChatBot.Api.Config;
using Microsoft.Extensions.Configuration;
```

(ข) แก้ constructor:
```csharp
// เดิม
public KnowledgeSyncService(IDbContextFactory<InsureChatDbContext> factory, IWebHostEnvironment env, ILogger<KnowledgeSyncService> logger)
{
    _factory = factory;
    _logger = logger;
    _wikiDir = FindWikiDir(env.ContentRootPath);
}
```
```csharp
// เป็น
public KnowledgeSyncService(IDbContextFactory<InsureChatDbContext> factory, IWebHostEnvironment env, IConfiguration config, ILogger<KnowledgeSyncService> logger)
{
    _factory = factory;
    _logger = logger;
    _wikiDir = KnowledgePaths.Resolve(env.ContentRootPath, config);
}
```

(ค) แก้ guard ใน `Sync()` (KnowledgePaths คืน path เสมอ ไม่ null):
```csharp
// เดิม
if (_wikiDir == null) { _logger.LogWarning("Knowledge sync: หาโฟลเดอร์ wiki ไม่เจอ"); return 0; }
// เป็น
if (!Directory.Exists(_wikiDir)) { _logger.LogWarning("Knowledge sync: หาโฟลเดอร์ Knowledge ไม่เจอ ({Dir})", _wikiDir); return 0; }
```

(ง) ลบเมธอด `FindWikiDir` ทั้งก้อน (บรรทัด ~129-138 — โค้ดเดียวกับใน SystemPromptProvider)

- [ ] **Step 7: build + test ทั้งหมดให้เขียว**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj
dotnet test  InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj
```
Expected: build SUCCESS, test PASS (เดิม 3 + ใหม่ 2 = 5). DI หา `IConfiguration` ให้เองอัตโนมัติ ไม่ต้องแก้ Program.cs

---

## Task 3: bundle corpus ความรู้ (ไม่มี PII) + copy เข้า output

**Files:**
- Create: `insurechat-api/InsureChatBot.Api/Knowledge/{concepts,playbooks,products,sources}/**`
- Modify: `insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj`

- [ ] **Step 1: copy เฉพาะ corpus non-PII จาก vault (ตัด `clients/` ออก)**

```bash
cd /c/CTD/ChatBot/insurechat-api/InsureChatBot.Api
mkdir -p Knowledge
cp -r /c/CTD/ChatBot/InsureChat/wiki/concepts  Knowledge/
cp -r /c/CTD/ChatBot/InsureChat/wiki/playbooks Knowledge/
cp -r /c/CTD/ChatBot/InsureChat/wiki/products  Knowledge/
cp -r /c/CTD/ChatBot/InsureChat/wiki/sources   Knowledge/
# ❌ ห้าม copy: wiki/clients (PII), wiki/log.md, raw/
```

- [ ] **Step 2: ยืนยันว่า 6 ไฟล์ inject + ไม่มี clients/ ติดมา**

```bash
cd /c/CTD/ChatBot/insurechat-api/InsureChatBot.Api
ls Knowledge/playbooks/spin-selling-การขายแบบที่ปรึกษา.md \
   Knowledge/playbooks/รับมือข้อโต้แย้ง-laarc-feel-felt-found.md \
   Knowledge/playbooks/เทคนิคปิดการขาย.md \
   Knowledge/playbooks/เปรียบเทียบแผน-จับคู่-need.md \
   Knowledge/concepts/ประกันรถยนต์.md \
   Knowledge/concepts/คปภ-การกำกับและจรรยาบรรณตัวแทน.md
test ! -d Knowledge/clients && echo "OK: ไม่มี clients/ (PII)"
```
Expected: 6 ไฟล์ครบ + "OK: ไม่มี clients/ (PII)"

- [ ] **Step 3: ให้ csproj copy `Knowledge/**` เข้า output + publish**

เพิ่ม ItemGroup ใน `insurechat-api/InsureChatBot.Api/InsureChatBot.Api.csproj` (ใต้ ItemGroup ของ `Data\users.json`):

```xml
<ItemGroup>
  <!-- ไฟล์ความรู้ (markdown, ไม่มี PII) ที่ bundle มา — อ่านตอน runtime โดย
       SystemPromptProvider + KnowledgeSyncService (ADR 0017). source of truth ยังเป็น wiki ใน vault -->
  <None Include="Knowledge\**\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 4: build + ยืนยันไฟล์ถูก copy ไป output**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj -c Debug
ls InsureChatBot.Api/bin/Debug/net10.0/Knowledge/concepts/ประกันรถยนต์.md
```
Expected: build SUCCESS + ไฟล์โผล่ใน `bin/.../Knowledge/concepts/`

---

## Task 4: ดึง secret ออกจากไฟล์ที่ commit → env (ทำก่อน git init)

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/appsettings.json`
- Modify: `insurechat-api/InsureChatBot.Api/Program.cs`
- Modify: `insurechat-api/InsureChatBot.Api/Services/Implementations/MotorQueryService.cs`

- [ ] **Step 1: ล้าง secret ออกจาก `appsettings.json` (ค่าที่ commit = ว่าง)**

แก้ `insurechat-api/InsureChatBot.Api/appsettings.json`:

```jsonc
// Jwt.Key → ว่าง (ไปอยู่ env JWT_KEY)
"Jwt": {
  "Key": "",
  "Issuer": "InsureChatBot",
  "Audience": "InsureChatBot",
  "ExpiresMinutes": 480
},
// MotorTool → ว่าง (path vault ไปอยู่ env MOTOR_SCRIPT_PATH / MOTOR_WORKING_DIR)
"MotorTool": {
  "PythonExe": "python",
  "ScriptPath": "",
  "WorkingDirectory": ""
},
// ConnectionStrings.EFDbConnection → ว่าง (ไปอยู่ env EF_DB_CONNECTION)
"ConnectionStrings": {
  "EFDbConnection": ""
}
```

- [ ] **Step 2: `Program.cs` — อ่าน EF connection + JWT key + MotorTool จาก env**

(ก) MotorTool: หลังบรรทัด `builder.Services.Configure<MotorToolSettings>(builder.Configuration.GetSection("MotorTool"));` เพิ่ม:
```csharp
// path Python tool อยู่ใน vault (owner-only) — มาจาก env, ไม่ commit. contractor ไม่มี → fallback search ว่าง
builder.Services.PostConfigure<MotorToolSettings>(s =>
{
    var sp = Environment.GetEnvironmentVariable("MOTOR_SCRIPT_PATH");
    if (!string.IsNullOrWhiteSpace(sp)) s.ScriptPath = sp.Trim();
    var wd = Environment.GetEnvironmentVariable("MOTOR_WORKING_DIR");
    if (!string.IsNullOrWhiteSpace(wd)) s.WorkingDirectory = wd.Trim();
});
```

(ข) JWT key: แก้บรรทัด `var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();` เป็น:
```csharp
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
// signing key จาก env (ไม่ commit). ถ้าไม่ตั้ง → ใช้ค่าใน appsettings (dev/ว่าง)
var jwtKeyEnv = Environment.GetEnvironmentVariable("JWT_KEY");
if (!string.IsNullOrWhiteSpace(jwtKeyEnv)) jwt.Key = jwtKeyEnv.Trim();
builder.Services.PostConfigure<JwtSettings>(s =>
{
    if (!string.IsNullOrWhiteSpace(jwtKeyEnv)) s.Key = jwtKeyEnv.Trim();
});
```

(ค) EF connection: แก้ block `AddDbContextFactory` (เดิมบรรทัด ~65-66) เป็น:
```csharp
// connstring ของ DB "InsureChat" จาก env EF_DB_CONNECTION (.env, ไม่ commit); fallback appsettings (dev)
var efConn = Environment.GetEnvironmentVariable("EF_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(efConn)) efConn = builder.Configuration.GetConnectionString("EFDbConnection");
builder.Services.AddDbContextFactory<InsureChatDbContext>(o => o.UseSqlServer(efConn));
```

- [ ] **Step 3: `MotorQueryService` — degrade เงียบถ้าไม่พบ Python script**

ใน `.../Services/Implementations/MotorQueryService.cs` ใส่ guard เป็น **บรรทัดแรกในบอดี้** ของ
`public async Task<List<MotorPackageDto>> SearchPackagesAsync(...)` (ก่อน `string klass = (insuranceClass ?? "").Trim();`):

```csharp
// contractor/prod ที่ไม่มี vault tools → ไม่มี query.py: คืน list ว่างแทน throw (ADR 0017)
if (string.IsNullOrWhiteSpace(_pythonScriptPath) || !File.Exists(_pythonScriptPath))
    return new List<MotorPackageDto>();
```
> return type จริง = `Task<List<MotorPackageDto>>` (ดู `IMotorQueryService`); เมธอดเป็น `async` อยู่แล้ว
> จึง `return new List<MotorPackageDto>();` ได้ตรง ๆ

- [ ] **Step 4: สร้าง `.env.example` (commit) + `.env` (ของ owner, ไม่ commit)**

สร้าง `insurechat-api/.env.example`:

```bash
# === InsureChat API — ตัวอย่าง env (คัดลอกเป็น .env แล้วเติมค่า) ===
# DB ของแอป (SQL Server) — ใช้แทน appsettings (ห้าม commit ค่าจริง)
EF_DB_CONNECTION=
# KPN live package DB (read-only) — ADR 0004
KPN_DB_CONNECTION=
# JWT signing key (ตั้งให้ยาว/สุ่ม สำหรับ production)
JWT_KEY=
# LLM (OpenRouter) — ADR 0003
OPENROUTER_API_KEY=
# อีเมลคำสั่งซื้อ (Gmail SMTP) — ADR 0012/0013
SMTP_USERNAME=
SMTP_PASSWORD=
ORDER_EMAIL_TO=
# Python motor tool (owner-only — ต้องมี vault tools/ + raw/) เว้นว่างได้
MOTOR_SCRIPT_PATH=
MOTOR_WORKING_DIR=
# โฟลเดอร์ความรู้ (เว้นว่าง = ใช้ ./Knowledge ที่ bundle มา)
KNOWLEDGE_DIR=
```

สร้าง `insurechat-api/.env` (owner เติมค่าจริงจาก `.env` เดิมของ vault — ไฟล์นี้ **gitignored**):
```bash
cp /c/CTD/ChatBot/insurechat-api/.env.example /c/CTD/ChatBot/insurechat-api/.env
# จากนั้นเปิด .env เติม: EF_DB_CONNECTION (= ค่า EFDbConnection เดิมใน appsettings),
# OPENROUTER_API_KEY/KPN_DB_CONNECTION/SMTP_* (จาก .env เดิมของ vault), JWT_KEY (สุ่มใหม่)
```

- [ ] **Step 5: build + รันสตาร์ทแอป ยืนยันอ่าน env ได้ + knowledge inject ทำงาน**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj
dotnet run  --project InsureChatBot.Api/InsureChatBot.Api.csproj --urls http://localhost:5228 &
sleep 12 && curl -s http://localhost:5228/openapi/v1.json -o /dev/null -w "%{http_code}\n"
# ปิด process หลังเช็ค (kill %1 หรือปิด terminal)
```
Expected: แอปสตาร์ตได้ (HTTP 200 จาก openapi), log **ไม่ขึ้น** "หาโฟลเดอร์ Knowledge ไม่เจอ" และ **ไม่ขึ้น** "ไม่พบไฟล์ความรู้"
> ต้องตั้ง `.env` ให้ `EF_DB_CONNECTION` ใช้ได้ก่อน (หรือ DB เข้าถึงได้) แอปจึงจะสตาร์ตครบ

- [ ] **Step 6: scan ยืนยันไม่มี secret หลงเหลือในไฟล์ที่จะ commit**

```bash
cd /c/CTD/ChatBot/insurechat-api
grep -rniE "c1!ckt0|Password=|sk-or-|OPENROUTER|210\.1\.60\.34" \
  --include=*.json --include=*.cs InsureChatBot.Api | grep -v ".env"
```
Expected: **ไม่มีผลลัพธ์** (ถ้าเจอ = ยังมี secret ค้าง ต้องล้างก่อนไปต่อ)

---

## Task 5: scaffold + `git init` api repo (commit แรกสะอาด)

**Files:**
- Create: `insurechat-api/.gitignore`, `insurechat-api/README.md`, `insurechat-api/InsureChatBot.sln`

- [ ] **Step 1: สร้าง `.gitignore` ของ api repo**

สร้าง `insurechat-api/.gitignore`:
```gitignore
# secrets
.env
*.env.local
# admin runtime settings (มี key) — ห้าม commit
InsureChatBot.Api/Data/app-settings.json
# .NET build
[Bb]in/
[Oo]bj/
.vs/
*.user
InsureChatBot.Api/publish/
# logs
*.log
# OS/editor
Thumbs.db
.DS_Store
```

- [ ] **Step 2: สร้าง `InsureChatBot.sln` ผูก 2 โปรเจกต์**

```bash
cd /c/CTD/ChatBot/insurechat-api
dotnet new sln -n InsureChatBot
dotnet sln add InsureChatBot.Api/InsureChatBot.Api.csproj
dotnet sln add InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj
```

- [ ] **Step 3: สร้าง `README.md`**

สร้าง `insurechat-api/README.md`:
```markdown
# InsureChat API (backend)

ASP.NET Core (net10) — chatbot ขายประกันรถ. แยกจาก vault เป็น repo อิสระ (ADR 0017).

## รัน dev
1. ติดตั้ง .NET 10 SDK + เข้าถึง SQL Server ได้
2. `cp .env.example .env` แล้วเติมค่า (อย่างน้อย `EF_DB_CONNECTION`, `OPENROUTER_API_KEY`)
3. `dotnet run --project InsureChatBot.Api --urls http://localhost:5228`
4. OpenAPI: http://localhost:5228/openapi/v1.json

## ทดสอบ
`dotnet test`

## หมายเหตุ
- frontend (`insurechat-web`) เรียก API ผ่าน proxy ที่ `:5228`
- ไฟล์ความรู้อยู่ใน `InsureChatBot.Api/Knowledge/` (sync มาจาก wiki ของ owner — source of truth)
- การค้นแพคเกจด้วย Python tool เป็น owner-only (ต้องตั้ง `MOTOR_SCRIPT_PATH`); ไม่ตั้ง = fallback ว่าง
- EF migrations: `dotnet ef database update --project InsureChatBot.Api`
```

- [ ] **Step 4: git init + commit แรก**

```bash
cd /c/CTD/ChatBot/insurechat-api
git init -b main
git add -A
git status   # ยืนยัน .env ไม่ติด staged
git commit -m "chore: initial commit — InsureChat API (split from vault, ADR 0017)"
```
Expected: commit สำเร็จ, `.env` + `bin/obj` + `app-settings.json` ไม่อยู่ใน commit

- [ ] **Step 5: ยืนยัน history สะอาด (ไม่มี secret)**

```bash
cd /c/CTD/ChatBot/insurechat-api
git log --oneline
git ls-files | grep -E "\.env$|app-settings\.json" && echo "พบ secret!" || echo "OK: ไม่มี secret tracked"
```
Expected: 1 commit + "OK: ไม่มี secret tracked"

---

## Task 6: สร้าง web repo

**Files:**
- Create: `insurechat-web/**` (จาก `InsureChat/app/web`), `insurechat-web/README.md`

- [ ] **Step 1: copy web + ตัด build artifact**

```bash
cd /c/CTD/ChatBot
mkdir -p insurechat-web
cp -r InsureChat/app/web/. insurechat-web/
rm -rf insurechat-web/node_modules insurechat-web/dist insurechat-web/.angular
```

- [ ] **Step 2: ยืนยัน `.gitignore` ของ web มีอยู่ (ติดมากับ copy)**

```bash
cd /c/CTD/ChatBot/insurechat-web
cat .gitignore | grep -E "node_modules|dist|.angular" && echo "OK"
```
Expected: เจอ pattern + "OK" (ถ้าไม่มี ให้สร้าง `.gitignore` ด้วยบรรทัด `node_modules/`, `dist/`, `.angular/`)

- [ ] **Step 3: สร้าง `README.md`**

สร้าง `insurechat-web/README.md`:
```markdown
# InsureChat Web (frontend)

Angular 21 — หน้า chatbot + admin. แยกจาก vault เป็น repo อิสระ (ADR 0017).

## รัน dev
1. `npm ci`
2. `npm start` (= `ng serve`, proxy ที่ `proxy.conf.json` → backend `http://localhost:5228`)
3. เปิด http://localhost:4200

## build
`npm run build` → `dist/`

## หมายเหตุ
- ต้องมี backend (`insurechat-api`) รันที่ `:5228` (หรือแก้ target ใน `proxy.conf.json`)
```

- [ ] **Step 4: build ให้ผ่าน**

```bash
cd /c/CTD/ChatBot/insurechat-web
npm ci
npm run build
```
Expected: `ng build` SUCCESS → โฟลเดอร์ `dist/` ถูกสร้าง

- [ ] **Step 5: git init + commit แรก**

```bash
cd /c/CTD/ChatBot/insurechat-web
git init -b main
git add -A
git status   # ยืนยัน node_modules/dist ไม่ติด
git commit -m "chore: initial commit — InsureChat Web (split from vault, ADR 0017)"
git ls-files | grep -E "node_modules|/dist/" && echo "พบ artifact!" || echo "OK: clean"
```
Expected: commit สำเร็จ + "OK: clean"

---

## Task 7: เก็บกวาด vault (`InsureChat/`)

**Files:**
- Delete: `InsureChat/app/`
- Modify: `InsureChat/.gitignore`, `InsureChat/CONTEXT.md`

- [ ] **Step 1: ลบโฟลเดอร์ `app/` (โค้ดย้ายออกไปแล้ว)**

```bash
cd /c/CTD/ChatBot/InsureChat
rm -rf app
```

- [ ] **Step 2: แก้ `.gitignore` ของ vault — ลบ path `app/...` ที่ไม่มีแล้ว**

แก้ `InsureChat/.gitignore` ลบบรรทัดที่อ้าง `app/`:
```gitignore
# ลบทิ้ง:
#   app/api/Data/app-settings.json
#   app/api/bin/  app/api/obj/  app/api.Tests/bin/  app/api.Tests/obj/
#   app/web/dist/  app/web/.angular/  app/.e2e/test-results/  app/.e2e/playwright-report/
# คงไว้: .env, *.env.local, raw/, node_modules/, Thumbs.db, .DS_Store, *.log
```
ผลลัพธ์ `.gitignore` เหลือ:
```gitignore
# Secrets
.env
*.env.local
# raw source data (heavy, user-owned)
raw/
# Node (เผื่อมีใน vault tools)
node_modules/
# OS/editor
Thumbs.db
.DS_Store
# runtime logs
*.log
```

- [ ] **Step 3: แก้ลิงก์เอกสารใน `CONTEXT.md` ที่ชี้ `app/`**

ใน `InsureChat/CONTEXT.md` §ลิงก์เอกสาร (บรรทัด ~128-130) แก้:
```markdown
- **Standalone app — Backend:** repo `insurechat-api/` (ASP.NET Core) — แยกออกตาม ADR 0017
- **Standalone app — Frontend:** repo `insurechat-web/` (Angular) — แยกออกตาม ADR 0017
```

- [ ] **Step 4: commit ลง vault repo**

```bash
cd /c/CTD/ChatBot/InsureChat
git add -A
git commit -m "refactor: extract api/web to separate repos; vault keeps wiki+docs (ADR 0017)"
```
Expected: commit สำเร็จ (เห็น `app/` ถูกลบจำนวนมาก + แก้ .gitignore/CONTEXT.md)

---

## Task 8: verify ขั้นสุดท้าย (handoff-ready)

- [ ] **Step 1: clean-clone test ของ api (จำลอง contractor — ไม่มี vault)**

```bash
cd /tmp && rm -rf api-clone && git clone /c/CTD/ChatBot/insurechat-api api-clone
cd api-clone
dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj
dotnet test  InsureChatBot.Api.Tests/InsureChatBot.Api.Tests.csproj
ls InsureChatBot.Api/Knowledge/concepts/ประกันรถยนต์.md   # ความรู้ติดมากับ repo
```
Expected: build SUCCESS, test PASS (5), ไฟล์ความรู้อยู่ครบ — โดยไม่ต้องมี vault

- [ ] **Step 2: clean-clone test ของ web**

```bash
cd /tmp && rm -rf web-clone && git clone /c/CTD/ChatBot/insurechat-web web-clone
cd web-clone && npm ci && npm run build
```
Expected: build SUCCESS

- [ ] **Step 3: secret scan ทั้ง 2 repo ที่แชร์**

```bash
cd /c/CTD/ChatBot/insurechat-api && git grep -niE "c1!ckt0|210\.1\.60\.34|sk-or-|Password=[^\"]" -- '*.json' '*.cs' ; echo "api exit=$?"
cd /c/CTD/ChatBot/insurechat-web && git grep -niE "sk-or-|password|secret" -- '*.ts' '*.json' | grep -vi "type\|interface\|placeholder" ; echo "web exit=$?"
```
Expected: api ไม่เจอ secret (exit=1 จาก grep = ไม่พบ); web ไม่มีคีย์จริง

- [ ] **Step 4: ยืนยัน publish ยัง 2 ที่ตามเดิม**

```bash
cd /c/CTD/ChatBot/insurechat-api && dotnet publish InsureChatBot.Api/InsureChatBot.Api.csproj -c Release -o /tmp/pub-api
ls /tmp/pub-api/Knowledge/concepts/ประกันรถยนต์.md   # Knowledge ติดไป publish
cd /c/CTD/ChatBot/insurechat-web && npm run build
```
Expected: publish api สำเร็จ (Knowledge อยู่ใน output) + web build = ครบ 2 artifact (ไม่เพิ่ม target ที่ 3)

---

## Definition of Done (เทียบ spec §8)

- [ ] api clone สะอาด → build + test ผ่าน (Task 8.1)
- [ ] api: knowledge inject ทำงานจาก `Knowledge/` (Task 4.5 ไม่มี warning)
- [ ] web clone สะอาด → build ผ่าน (Task 8.2)
- [ ] ไม่มี secret/PII ใน 2 repo ที่แชร์; fresh init history สะอาด (Task 5.5, 6.5, 8.3)
- [ ] vault ไม่มี `app/`; Obsidian graph ปกติ; เอกสาร path อัปเดต (Task 7)
- [ ] publish ยัง 2 ที่ (Task 8.4)
- [ ] ADR 0017 มีแล้ว (commit `3809ad1`)

## นอกสโคป plan นี้ (ดู spec §7, §10)

- **Phase 2** — จัดระเบียบ `Services/Implementations` + `Services/Interfaces` → feature folder (แผนแยก, หลัง Phase 1)
- ตั้ง remote/push ขึ้น GitHub + CI ต่อ repo
- แยก `llm` เป็น library/service (ตัดทิ้งตาม spec D1)
