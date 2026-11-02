# InsureChat SQL — Phase 1 Implementation Plan (EF foundation + Users + AppSettings)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ทำให้ DB `InsureChat` (SQL Server) เก็บ `Users` + `AppSettings` ผ่าน EF Core code-first โดยสลับ store จาก JSON → SQL ที่ seam `IUserStore`/`IAppSettingsStore` โดยไม่กระทบ controller/prompt logic เดิม

**Architecture:** EF Core code-first (`InsureChatDbContext`) ลงทะเบียนผ่าน `AddDbContextFactory` เพื่อให้ store ที่เป็น **Singleton** (ตามของเดิม + `SystemPromptProvider` พึ่งพา) สร้าง `DbContext` สั้น ๆ ต่อ operation ได้ (กัน captive dependency). DB ของ KPN (motor) คง Dapper read-only ไม่แตะ. **secret (LLM API key, รหัสอีเมล) ไม่ลง DB** — บังคับ env-only โดย store ไม่เก็บ/ไม่โหลด secret และ null ทิ้งใน cache (controller/DTO เดิมไม่ต้องแก้: `GetKeySource` คืน "env"/"none" อัตโนมัติเมื่อ ApiKey เป็น null)

**Tech Stack:** ASP.NET Core net10, EF Core 10 (SqlServer provider), Dapper (คงไว้สำหรับ motor), xUnit + EF Core Sqlite (in-memory) สำหรับ unit test store

---

## หมายเหตุก่อนเริ่ม (อ่านก่อน)

- **โฟลเดอร์นี้ยังไม่ใช่ git repo** — ถ้าต้องการ track ให้รัน `git init` ที่ `c:\CTD\ChatBot\InsureChat` ก่อน แล้ว step `Commit` จะใช้ได้; ถ้าไม่ init ให้ **ข้าม step commit** (งานยังถูกต้องอยู่)
- **คำสั่งทั้งหมดรันจาก root โปรเจค** `c:\CTD\ChatBot\InsureChat` (shell = PowerShell)
- DB จริงอยู่ remote (`210.1.60.34,14333`) — connstring `EFDbConnection` อยู่ใน `app/api/appsettings.json` แล้ว ต้องต่อเน็ตถึงเครื่องนั้นได้เวลา `database update`
- ไฟล์อ้างอิง: spec `docs/superpowers/specs/2026-06-18-insurechat-sql-database-design.md` *(ดู §4.1, §4.2, §6, §8)*

---

## โครงไฟล์ (สร้าง/แก้)

**สร้างใหม่:**
- `app/api/Data/Entities/UserRow.cs` — EF entity ของตาราง Users
- `app/api/Data/Entities/AppSettingsRow.cs` — EF entity ของตาราง AppSettings (flat, ไม่มี secret)
- `app/api/Data/InsureChatDbContext.cs` — DbContext + DbSet + Fluent config
- `app/api/Data/DataInitializer.cs` — seed admin + migrate settings (idempotent)
- `app/api/Services/Implementations/SqlUserStore.cs` — `IUserStore` บน EF
- `app/api/Services/Settings/SqlAppSettingsStore.cs` — `IAppSettingsStore` บน EF (strip secret)
- `app/api/Migrations/*` — generated โดย `dotnet ef`
- `app/api.Tests/InsureChatBot.Api.Tests.csproj` — test project
- `app/api.Tests/TestDbContextFactory.cs` — `IDbContextFactory` บน SQLite in-memory
- `app/api.Tests/SqlUserStoreTests.cs`
- `app/api.Tests/SqlAppSettingsStoreTests.cs`
- `docs/adr/0016-insurechat-owns-sql-db-ef-core.md`

**แก้ไข:**
- `app/api/InsureChatBot.Api.csproj` — เพิ่ม EF Core packages
- `app/api/Program.cs` — register DbContextFactory + สลับ DI store + เรียก DataInitializer
- `CONTEXT.md` — อัปเดตศัพท์ (Session/Order/Lead/KnowledgeArticle/key source)

> ไม่แตะ: `AuthController`, `AdminController`, `SettingsController`, `SystemPromptProvider`, DTO, `IUserStore`, `IAppSettingsStore` (interface), `MotorFacetSqlService`

---

## Task 1: เพิ่ม EF Core packages

**Files:**
- Modify: `app/api/InsureChatBot.Api.csproj:13-19`

- [ ] **Step 1: เพิ่ม PackageReference 3 ตัวใน ItemGroup แรก**

แก้ block `<ItemGroup>` แรก (บรรทัด 13-19) ให้เป็น:

```xml
  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.1.79" />
    <PackageReference Include="MailKit" Version="4.17.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.6" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
```

- [ ] **Step 2: restore + build เพื่อยืนยัน package ใช้ได้**

Run: `dotnet build app/api/InsureChatBot.Api.csproj`
Expected: Build succeeded (อาจมี warning แต่ต้องไม่มี error). ถ้า version 10.0.0 หาไม่เจอ ให้ใช้เวอร์ชัน net10 ล่าสุดที่ restore ได้ (เช็คด้วย `dotnet add app/api package Microsoft.EntityFrameworkCore.SqlServer` แล้วปล่อยให้มันเลือก)

- [ ] **Step 3: ติดตั้ง dotnet-ef tool (ถ้ายังไม่มี)**

Run: `dotnet tool install --global dotnet-ef` *(ถ้ามีแล้วจะแจ้ง "already installed" — ข้ามได้)*
ตรวจ: `dotnet ef --version` → ต้องขึ้นเวอร์ชัน (เช่น 10.x)

- [ ] **Step 4: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/InsureChatBot.Api.csproj
git commit -m "build: add EF Core SqlServer + Design packages"
```

---

## Task 2: สร้าง EF entities + DbContext

**Files:**
- Create: `app/api/Data/Entities/UserRow.cs`
- Create: `app/api/Data/Entities/AppSettingsRow.cs`
- Create: `app/api/Data/InsureChatDbContext.cs`

- [ ] **Step 1: สร้าง `app/api/Data/Entities/UserRow.cs`**

```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>ตาราง Users — ย้ายจาก users.json (ADR 0016). โดเมน POCO ยังเป็น Models.Auth.User; map ใน SqlUserStore.</summary>
    public class UserRow
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }   // "iterations.saltB64.hashB64" (PBKDF2-SHA256)
        public string Role { get; set; }            // admin (agent/customer = legacy)
        public string CustCode { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 2: สร้าง `app/api/Data/Entities/AppSettingsRow.cs`**

```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>
    /// ตาราง AppSettings — 1 แถว (Id=1). ย้ายจาก app-settings.json (ADR 0016).
    /// **ไม่มีคอลัมน์ secret** (LLM ApiKey / Email Password) — secret อยู่ env เท่านั้น.
    /// </summary>
    public class AppSettingsRow
    {
        public int Id { get; set; }                 // บังคับ = 1
        public string ThemeBrand { get; set; } = "#2563eb";

        // 4 ช่อง prompt (ADR 0014) — null = ใช้ default ในโค้ด
        public string Persona { get; set; }
        public string SalesApproach { get; set; }
        public string Compliance { get; set; }
        public string Style { get; set; }

        // LLM overrides (ไม่มี ApiKey) — null = ใช้ค่า base จาก config/env
        public bool? LlmEnabled { get; set; }
        public string LlmBaseUrl { get; set; }
        public string LlmModel { get; set; }
        public double? LlmTemperature { get; set; }
        public int? LlmMaxTokens { get; set; }
        public int? LlmTimeoutSeconds { get; set; }

        // Email overrides (ไม่มี Password)
        public string EmailUsername { get; set; }
        public string EmailTo { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 3: สร้าง `app/api/Data/InsureChatDbContext.cs`**

> หมายเหตุ: config ต้อง provider-agnostic (ห้ามใส่ `HasColumnType("nvarchar(max)")`) เพราะ unit test ใช้ SQLite. ใช้ convention + `HasMaxLength` พอ.

```csharp
using InsureChatBot.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Data
{
    /// <summary>DbContext ของ DB "InsureChat" (ของแอปเอง, เขียนได้) — ADR 0016. แยกจาก KPN DB (Dapper, read-only).</summary>
    public class InsureChatDbContext : DbContext
    {
        public InsureChatDbContext(DbContextOptions<InsureChatDbContext> options) : base(options) { }

        public DbSet<UserRow> Users => Set<UserRow>();
        public DbSet<AppSettingsRow> AppSettings => Set<AppSettingsRow>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<UserRow>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Username).HasMaxLength(64).IsRequired();
                e.HasIndex(x => x.Username).IsUnique();
                e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
                e.Property(x => x.Role).HasMaxLength(16).IsRequired();
                e.Property(x => x.CustCode).HasMaxLength(64);
                e.Property(x => x.Name).HasMaxLength(128);
                e.Property(x => x.Email).HasMaxLength(256);
            });

            b.Entity<AppSettingsRow>(e =>
            {
                e.ToTable("AppSettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();   // เราคุม Id=1 เอง
                e.Property(x => x.ThemeBrand).HasMaxLength(16);
                e.Property(x => x.LlmBaseUrl).HasMaxLength(256);
                e.Property(x => x.LlmModel).HasMaxLength(256);
                e.Property(x => x.EmailUsername).HasMaxLength(256);
                e.Property(x => x.EmailTo).HasMaxLength(256);
                // Persona/SalesApproach/Compliance/Style = nvarchar(max) ตาม convention (string ไม่จำกัด length)
            });
        }
    }
}
```

- [ ] **Step 4: build เพื่อยืนยัน compile ผ่าน**

Run: `dotnet build app/api/InsureChatBot.Api.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Data
git commit -m "feat: add InsureChatDbContext with UserRow + AppSettingsRow entities"
```

---

## Task 3: สร้าง migration InitialCreate + apply ไป DB

**Files:**
- Create: `app/api/Migrations/*` (auto-generated)

- [ ] **Step 1: ยืนยันว่า DbContext หา connstring ได้ตอน design-time**

`dotnet ef` ต้องสร้าง DbContext ได้. ตอนนี้ Program.cs ยังไม่ register DbContext (จะทำใน Task 7) — แต่ EF design-time ใช้ `AddDbContextFactory`/`AddDbContext` ใน Program.cs. ดังนั้น **ทำ Task 7 ก่อน Task 3** ถ้า `dotnet ef` หา context ไม่เจอ.
> ลำดับที่แนะนำ: ถ้า `dotnet ef migrations add` (Step 2) ฟ้อง "No DbContext was found" ให้ไปทำ Task 7 (register ใน Program.cs) ก่อน แล้วกลับมา.

- [ ] **Step 2: สร้าง migration**

Run: `dotnet ef migrations add InitialCreate --project app/api/InsureChatBot.Api.csproj --output-dir Migrations`
Expected: สร้างไฟล์ใน `app/api/Migrations/` (`*_InitialCreate.cs` + `InsureChatDbContextModelSnapshot.cs`). เปิดดู `Up()` ต้องมี `CREATE TABLE Users` (มี unique index Username) และ `AppSettings`.

- [ ] **Step 3: apply ไป DB จริง**

Run: `dotnet ef database update --project app/api/InsureChatBot.Api.csproj`
Expected: "Applying migration '..._InitialCreate'. Done." — ถ้าต่อ DB remote ไม่ได้ ให้ generate script แทน: `dotnet ef migrations script -o init.sql --project app/api/InsureChatBot.Api.csproj` แล้วรัน `init.sql` บน DB `InsureChat` ด้วย SSMS/sqlcmd

- [ ] **Step 4: ตรวจว่าตารางเกิดจริง**

Run (ปรับ user/pass ตาม connstring): `sqlcmd -S 210.1.60.34,14333 -U clicktodesign -P "c1!ckt0" -d InsureChat -Q "SELECT name FROM sys.tables"`
Expected: เห็น `Users`, `AppSettings`, `__EFMigrationsHistory`

- [ ] **Step 5: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Migrations
git commit -m "feat: add InitialCreate migration (Users, AppSettings)"
```

---

## Task 4: สร้าง test project + TestDbContextFactory

**Files:**
- Create: `app/api.Tests/InsureChatBot.Api.Tests.csproj`
- Create: `app/api.Tests/TestDbContextFactory.cs`

- [ ] **Step 1: สร้าง `app/api.Tests/InsureChatBot.Api.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\api\InsureChatBot.Api.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: สร้าง `app/api.Tests/TestDbContextFactory.cs`**

> ใช้ SQLite in-memory ที่ "เปิด connection ค้างไว้" (ปิดเมื่อ Dispose) เพื่อให้ทุก context เห็น schema เดียวกัน

```csharp
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
```

- [ ] **Step 3: build test project เพื่อยืนยัน reference ถูก**

Run: `dotnet build app/api.Tests/InsureChatBot.Api.Tests.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api.Tests/InsureChatBot.Api.Tests.csproj app/api.Tests/TestDbContextFactory.cs
git commit -m "test: add api.Tests project with SQLite in-memory factory"
```

---

## Task 5: SqlUserStore (TDD)

**Files:**
- Create: `app/api.Tests/SqlUserStoreTests.cs`
- Create: `app/api/Services/Implementations/SqlUserStore.cs`

- [ ] **Step 1: เขียน test ที่ยังไม่ผ่าน — `app/api.Tests/SqlUserStoreTests.cs`**

```csharp
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
```

- [ ] **Step 2: รัน test ให้เห็นว่า fail (compile error: SqlUserStore ไม่มี)**

Run: `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`
Expected: FAIL — compile error "type or namespace 'SqlUserStore' could not be found"

- [ ] **Step 3: เขียน `app/api/Services/Implementations/SqlUserStore.cs`**

```csharp
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
```

- [ ] **Step 4: รัน test ให้ผ่าน**

Run: `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`
Expected: PASS — 4 tests ของ SqlUserStoreTests ผ่านหมด

- [ ] **Step 5: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Services/Implementations/SqlUserStore.cs app/api.Tests/SqlUserStoreTests.cs
git commit -m "feat: SqlUserStore (EF) implementing IUserStore"
```

---

## Task 6: SqlAppSettingsStore (TDD) — บังคับ secret env-only

**Files:**
- Create: `app/api.Tests/SqlAppSettingsStoreTests.cs`
- Create: `app/api/Services/Settings/SqlAppSettingsStore.cs`

> หลักการ: map `AppSettingsRow` (DB, ไม่มี secret) ↔ `AppSettings` POCO (Models.Admin, มี Llm.ApiKey/Email.Password แต่จะ **null เสมอ**). `GetEffectiveLlm`/`GetKeySource` logic เหมือน `AppSettingsStore` เดิม → เมื่อ ApiKey เป็น null จะ fallback env และคืน source "env"/"none" อัตโนมัติ.

- [ ] **Step 1: เขียน test — `app/api.Tests/SqlAppSettingsStoreTests.cs`**

```csharp
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

            // store ใหม่ (อ่านจาก DB เดิม) ต้องเห็นค่าที่เซฟ
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

            // พยายามตั้ง admin key — ต้องถูก strip ทิ้ง (env-only, ADR 0016)
            store.Update(s => { s.Llm.ApiKey = "sk-admin-should-be-ignored"; });

            Assert.Equal("sk-env", store.GetEffectiveLlm().ApiKey);
            Assert.Equal("env", store.GetKeySource());
        }
    }
}
```

- [ ] **Step 2: รัน test ให้เห็น fail (SqlAppSettingsStore ไม่มี)**

Run: `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`
Expected: FAIL — compile error "SqlAppSettingsStore could not be found"

- [ ] **Step 3: เขียน `app/api/Services/Settings/SqlAppSettingsStore.cs`**

```csharp
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Settings
{
    /// <summary>
    /// IAppSettingsStore บน SQL (EF Core) — แทน AppSettingsStore (ADR 0016).
    /// Singleton + IDbContextFactory + cache ในหน่วยความจำ (โหลด 1 แถว Id=1).
    /// **secret env-only**: ไม่โหลด/ไม่เก็บ ApiKey & Password ลง DB และ null ทิ้งใน cache เสมอ
    /// → GetEffective* fallback env, GetKeySource คืน "env"/"none".
    /// </summary>
    public class SqlAppSettingsStore : IAppSettingsStore
    {
        private const int RowId = 1;
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        private readonly LlmSettings _baseLlm;
        private readonly OrderEmailSettings _baseEmail;
        private readonly object _lock = new();
        private AppSettings _settings;

        public SqlAppSettingsStore(
            IDbContextFactory<InsureChatDbContext> factory,
            IOptions<LlmSettings> baseLlm,
            IOptions<OrderEmailSettings> baseEmail)
        {
            _factory = factory;
            _baseLlm = baseLlm.Value;
            _baseEmail = baseEmail.Value;
            _settings = Load();
        }

        private AppSettings Load()
        {
            using var db = _factory.CreateDbContext();
            var row = db.AppSettings.AsNoTracking().FirstOrDefault(x => x.Id == RowId);
            return row == null ? new AppSettings() : ToPoco(row);
        }

        public AppSettings Get() { lock (_lock) return _settings; }

        public void Update(Action<AppSettings> mutate)
        {
            lock (_lock)
            {
                mutate(_settings);
                StripSecrets(_settings);   // env-only — ทิ้ง secret ที่ใครพยายามตั้ง

                using var db = _factory.CreateDbContext();
                var row = db.AppSettings.FirstOrDefault(x => x.Id == RowId);
                if (row == null) { row = new AppSettingsRow { Id = RowId }; db.AppSettings.Add(row); }
                WriteRow(row, _settings);
                row.UpdatedAt = DateTime.UtcNow;
                db.SaveChanges();
            }
        }

        public LlmSettings GetEffectiveLlm()
        {
            AppSettings s; lock (_lock) s = _settings;
            var o = s.Llm ?? new LlmOverrides();
            var b = _baseLlm;
            return new LlmSettings
            {
                Enabled = o.Enabled ?? b.Enabled,
                BaseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? b.BaseUrl : o.BaseUrl,
                Model = string.IsNullOrWhiteSpace(o.Model) ? b.Model : o.Model,
                ApiKey = string.IsNullOrWhiteSpace(o.ApiKey) ? b.ApiKey : o.ApiKey, // o.ApiKey = null เสมอ → env
                Temperature = o.Temperature ?? b.Temperature,
                MaxTokens = o.MaxTokens ?? b.MaxTokens,
                TimeoutSeconds = o.TimeoutSeconds ?? b.TimeoutSeconds,
                MaxToolIterations = b.MaxToolIterations
            };
        }

        public string GetKeySource()
        {
            AppSettings s; lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Llm?.ApiKey)) return "admin"; // ไม่เกิดขึ้น (strip) — กันไว้
            if (!string.IsNullOrWhiteSpace(_baseLlm.ApiKey)) return "env";
            return "none";
        }

        public OrderEmailSettings GetEffectiveEmail()
        {
            AppSettings s; lock (_lock) s = _settings;
            var o = s.Email ?? new OrderEmailOverrides();
            var b = _baseEmail;
            var username = string.IsNullOrWhiteSpace(o.Username) ? b.Username : o.Username.Trim();
            return new OrderEmailSettings
            {
                Host = b.Host,
                Port = b.Port,
                Username = username,
                From = username,
                Password = string.IsNullOrWhiteSpace(o.Password) ? b.Password : o.Password, // o.Password = null → env
                To = string.IsNullOrWhiteSpace(o.To) ? b.To : o.To.Trim()
            };
        }

        public string GetEmailKeySource()
        {
            AppSettings s; lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Email?.Password)) return "admin"; // ไม่เกิดขึ้น (strip)
            if (!string.IsNullOrWhiteSpace(_baseEmail.Password)) return "env";
            return "none";
        }

        // ===== mapping =====
        private static void StripSecrets(AppSettings s)
        {
            if (s.Llm != null) s.Llm.ApiKey = null;
            if (s.Email != null) s.Email.Password = null;
        }

        private static AppSettings ToPoco(AppSettingsRow r)
        {
            var s = new AppSettings
            {
                Theme = new ThemeSettings { Brand = string.IsNullOrWhiteSpace(r.ThemeBrand) ? "#2563eb" : r.ThemeBrand },
                Persona = r.Persona,
                SalesApproach = r.SalesApproach,
                Compliance = r.Compliance,
                Style = r.Style,
                Llm = new LlmOverrides
                {
                    Enabled = r.LlmEnabled, BaseUrl = r.LlmBaseUrl, Model = r.LlmModel,
                    Temperature = r.LlmTemperature, MaxTokens = r.LlmMaxTokens, TimeoutSeconds = r.LlmTimeoutSeconds,
                    ApiKey = null
                },
                Email = new OrderEmailOverrides { Username = r.EmailUsername, To = r.EmailTo, Password = null }
            };
            return s;
        }

        private static void WriteRow(AppSettingsRow r, AppSettings s)
        {
            r.ThemeBrand = s.Theme?.Brand;
            r.Persona = s.Persona;
            r.SalesApproach = s.SalesApproach;
            r.Compliance = s.Compliance;
            r.Style = s.Style;
            r.LlmEnabled = s.Llm?.Enabled;
            r.LlmBaseUrl = s.Llm?.BaseUrl;
            r.LlmModel = s.Llm?.Model;
            r.LlmTemperature = s.Llm?.Temperature;
            r.LlmMaxTokens = s.Llm?.MaxTokens;
            r.LlmTimeoutSeconds = s.Llm?.TimeoutSeconds;
            r.EmailUsername = s.Email?.Username;
            r.EmailTo = s.Email?.To;
            // ไม่เขียน ApiKey/Password — ไม่มีคอลัมน์ (env-only)
        }
    }
}
```

- [ ] **Step 4: รัน test ให้ผ่าน**

Run: `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`
Expected: PASS — ทุก test (SqlUserStore 4 + SqlAppSettingsStore 4) ผ่าน

- [ ] **Step 5: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Services/Settings/SqlAppSettingsStore.cs app/api.Tests/SqlAppSettingsStoreTests.cs
git commit -m "feat: SqlAppSettingsStore (EF) with env-only secrets"
```

---

## Task 7: เดินสาย DI ใน Program.cs

**Files:**
- Modify: `app/api/Program.cs:62-65` (DI registrations) + เพิ่ม using + DbContextFactory

- [ ] **Step 1: เพิ่ม using ด้านบนของ Program.cs**

หลังบรรทัด `using InsureChatBot.Api.Config;` (บรรทัด 3) เพิ่ม:

```csharp
using InsureChatBot.Api.Data;
using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 2: register DbContextFactory ก่อน register stores**

แทรกก่อนบรรทัด `builder.Services.AddSingleton<IUserStore, JsonUserStore>();` (เดิมบรรทัด 62):

```csharp
// EF Core — DB "InsureChat" ของแอปเอง (ADR 0016). ใช้ Factory เพื่อให้ singleton store สร้าง context สั้น ๆ ได้
builder.Services.AddDbContextFactory<InsureChatDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("EFDbConnection")));
```

- [ ] **Step 3: สลับ store JSON → SQL**

แก้ 2 บรรทัด:
- เดิม `builder.Services.AddSingleton<IUserStore, JsonUserStore>();`
  → `builder.Services.AddSingleton<IUserStore, SqlUserStore>();`
- เดิม `builder.Services.AddSingleton<IAppSettingsStore, AppSettingsStore>();`
  → `builder.Services.AddSingleton<IAppSettingsStore, SqlAppSettingsStore>();`

> เก็บ `JsonUserStore.cs` / `AppSettingsStore.cs` ไว้ (ไม่ลบ) — เผื่อ rollback; แค่ไม่ register

- [ ] **Step 4: build เพื่อยืนยัน DI compile + ไม่มี captive dependency error**

Run: `dotnet build app/api/InsureChatBot.Api.csproj`
Expected: Build succeeded

- [ ] **Step 5: ตรวจ DI graph ตอน start (เร็ว ๆ)**

Run: `dotnet run --project app/api/InsureChatBot.Api.csproj --launch-profile http` (ปล่อยขึ้นสักครู่)
Expected: แอป start ได้ ไม่มี exception "Cannot consume scoped service ... from singleton" — กด Ctrl+C ปิด
> ถ้าตอนนี้ DB ว่าง (ยังไม่ seed) แอปก็ต้อง start ได้ (store คืน default). seed อยู่ Task 8.

- [ ] **Step 6: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Program.cs
git commit -m "feat: wire EF DbContextFactory + swap to Sql stores"
```

---

## Task 8: DataInitializer — seed admin + migrate settings (idempotent)

**Files:**
- Create: `app/api/Data/DataInitializer.cs`
- Modify: `app/api/Program.cs` (เรียกหลัง `var app = builder.Build();`)

- [ ] **Step 1: สร้าง `app/api/Data/DataInitializer.cs`**

```csharp
using System.Text.Json;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Data
{
    /// <summary>
    /// เติมข้อมูลตั้งต้นแบบ idempotent (ADR 0016):
    ///  - ถ้าตาราง Users ว่าง → import จาก Data/users.json (ถ้ามี)
    ///  - ถ้ายังไม่มีแถว AppSettings → import จาก Data/app-settings.json (ถ้ามี) มิฉะนั้นสร้าง default (Id=1)
    /// secret ใน app-settings.json เดิม **ไม่ import** (อยู่ env เท่านั้น)
    /// </summary>
    public static class DataInitializer
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public static void Run(IDbContextFactory<InsureChatDbContext> factory, string contentRoot)
        {
            using var db = factory.CreateDbContext();

            // ── Users ──
            if (!db.Users.Any())
            {
                var usersPath = Path.Combine(contentRoot, "Data", "users.json");
                if (File.Exists(usersPath))
                {
                    var list = JsonSerializer.Deserialize<List<User>>(File.ReadAllText(usersPath), JsonOpts) ?? new();
                    var now = DateTime.UtcNow;
                    foreach (var u in list)
                        if (!string.IsNullOrWhiteSpace(u.Username))
                            db.Users.Add(new UserRow
                            {
                                Username = u.Username, PasswordHash = u.PasswordHash, Role = u.Role,
                                CustCode = u.CustCode, Name = u.Name, Email = u.Email,
                                CreatedAt = now, UpdatedAt = now
                            });
                    db.SaveChanges();
                }
            }

            // ── AppSettings (1 แถว) ──
            if (!db.AppSettings.Any())
            {
                var row = new AppSettingsRow { Id = 1, ThemeBrand = "#2563eb", UpdatedAt = DateTime.UtcNow };
                var settingsPath = Path.Combine(contentRoot, "Data", "app-settings.json");
                if (File.Exists(settingsPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                    var r = doc.RootElement;
                    if (r.TryGetProperty("Theme", out var t) && t.TryGetProperty("Brand", out var brand))
                        row.ThemeBrand = brand.GetString() ?? "#2563eb";
                    row.Persona = GetStr(r, "Persona");
                    row.SalesApproach = GetStr(r, "SalesApproach");
                    row.Compliance = GetStr(r, "Compliance");
                    row.Style = GetStr(r, "Style");
                    // ไม่ import secret (Llm.ApiKey / Email.Password) — env-only
                }
                db.AppSettings.Add(row);
                db.SaveChanges();
            }
        }

        private static string GetStr(JsonElement r, string name)
            => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
```

- [ ] **Step 2: เรียก DataInitializer ใน Program.cs**

แทรกหลังบรรทัด `var app = builder.Build();` (เดิมบรรทัด 107):

```csharp
// เติมข้อมูลตั้งต้น (idempotent) — ต้องรันหลังตารางถูกสร้างด้วย migration แล้ว (ADR 0016)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InsureChatDbContext>>();
    DataInitializer.Run(factory, app.Environment.ContentRootPath);
}
```

- [ ] **Step 3: build**

Run: `dotnet build app/api/InsureChatBot.Api.csproj`
Expected: Build succeeded

- [ ] **Step 4: รันแอปให้ seed จริง แล้วตรวจ DB**

Run: `dotnet run --project app/api/InsureChatBot.Api.csproj --launch-profile http` (ปล่อยขึ้น แล้ว Ctrl+C)
ตรวจ: `sqlcmd -S 210.1.60.34,14333 -U clicktodesign -P "c1!ckt0" -d InsureChat -Q "SELECT Username, Role FROM Users; SELECT Id, ThemeBrand FROM AppSettings"`
Expected: เห็น `admin / admin` และ AppSettings 1 แถว (`1 / #2563eb`)

- [ ] **Step 5: รันแอปซ้ำ → ยืนยัน idempotent (ไม่ insert ซ้ำ)**

Run: `dotnet run --project app/api/InsureChatBot.Api.csproj --launch-profile http` (Ctrl+C)
ตรวจ: `sqlcmd ... -Q "SELECT COUNT(*) FROM Users"`
Expected: ยังเท่าเดิม (ไม่เพิ่ม)

- [ ] **Step 6: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add app/api/Data/DataInitializer.cs app/api/Program.cs
git commit -m "feat: DataInitializer seeds admin + settings from JSON (idempotent)"
```

---

## Task 9: Smoke test ผ่าน HTTP (login admin จาก SQL + theme persist + key source = env)

**Files:** (ไม่มีไฟล์ใหม่ — ทดสอบ runtime)

- [ ] **Step 1: start แอป**

Run: `dotnet run --project app/api/InsureChatBot.Api.csproj --launch-profile http` (ปล่อยค้างไว้ใน terminal แยก; ดู URL เช่น `http://localhost:5xxx`)

- [ ] **Step 2: login admin (อ่านจาก SQL)**

Run (แทน PORT):
```bash
curl -s -X POST http://localhost:PORT/api/Auth/Login -H "Content-Type: application/json" -d '{"username":"admin","password":"<รหัส admin จริง>"}'
```
Expected: HTTP 200 + JSON มี `token`, `role":"admin"`. ถ้าไม่รู้รหัส admin: ทดสอบรหัสผิด → ต้องได้ 401 (พิสูจน์ว่า store อ่าน hash จาก SQL จริง)

- [ ] **Step 3: ตรวจ key source = env (ไม่ใช่ admin)**

ใช้ token จาก Step 2:
```bash
curl -s http://localhost:PORT/api/Admin/Llm -H "Authorization: Bearer <TOKEN>"
```
Expected: JSON `keySource` = `"env"` (ถ้าตั้ง OPENROUTER_API_KEY ใน .env) หรือ `"none"` — **ต้องไม่เป็น "admin"**

- [ ] **Step 4: ตรวจ theme persist ลง SQL**

```bash
curl -s -X PUT http://localhost:PORT/api/Admin/Theme -H "Authorization: Bearer <TOKEN>" -H "Content-Type: application/json" -d '{"brand":"#16a34a"}'
```
แล้ว `sqlcmd ... -Q "SELECT ThemeBrand FROM AppSettings WHERE Id=1"`
Expected: `#16a34a` (ค่าใหม่อยู่ใน SQL ไม่ใช่ app-settings.json)

- [ ] **Step 5: ตรวจว่า set api key ผ่าน admin ถูก strip (env-only)**

```bash
curl -s -X PUT http://localhost:PORT/api/Admin/Llm -H "Authorization: Bearer <TOKEN>" -H "Content-Type: application/json" -d '{"apiKey":"sk-should-be-ignored"}'
curl -s http://localhost:PORT/api/Admin/Llm -H "Authorization: Bearer <TOKEN>"
```
Expected: `keySource` ยังเป็น `"env"`/`"none"` (ไม่กลายเป็น "admin") — ยืนยัน secret env-only

- [ ] **Step 6: ปิดแอป (Ctrl+C). ไม่มี commit (ทดสอบล้วน)**

---

## Task 10: เอกสาร — ADR 0016 + อัปเดต CONTEXT.md

**Files:**
- Create: `docs/adr/0016-insurechat-owns-sql-db-ef-core.md`
- Modify: `CONTEXT.md` (Session row, API key/App Password rows, เพิ่มศัพท์ใหม่)

- [ ] **Step 1: สร้าง `docs/adr/0016-insurechat-owns-sql-db-ef-core.md`**

```markdown
# 0016 — InsureChat เป็นเจ้าของ SQL DB (EF Core)

standalone app ย้ายจาก "JSON now" ([[docs/adr/0002]]) มาเป็นเจ้าของ DB `InsureChat` ของตัวเอง
ผ่าน **EF Core code-first + migrations** (connstring `EFDbConnection`). DB นี้แยกจาก `KPNNew`
(KPN, read-only Dapper, [[docs/adr/0004]]) — motor ยังอ่านผ่าน Dapper เหมือนเดิม (dual-stack).

## ขอบเขต
- **Phase 1:** `Users` (ย้ายจาก users.json), `AppSettings` (1 แถว, ย้ายจาก app-settings.json)
- **Phase 2:** `Conversations` + `ConversationTurns` + `ConversationNeeds`, `Orders`
- **Phase 3:** `KnowledgeArticles` (วิกิ sync, ไฟล์ยังเป็น source of truth)

## ผลต่อ ADR เดิม
- **ปิดฉาก [[docs/adr/0002]]** — JSON→SQL เกิดขึ้นแล้ว ผ่าน seam `IUserStore`/`IAppSettingsStore` ที่ออกแบบไว้
  (สลับ impl ที่ DI; controller/prompt logic ไม่แตะ). `JsonUserStore`/`AppSettingsStore` คงไว้เผื่อ rollback
- **amend [[docs/adr/0011]] / [[docs/adr/0013]]** — secret (LLM API key, รหัสอีเมล) เก็บ **env เท่านั้น** ไม่ลง DB.
  admin **แก้ key/รหัสอีเมลผ่าน UI ไม่ได้อีก** (`SqlAppSettingsStore` strip secret ทิ้งเสมอ); `GetKeySource`/
  `GetEmailKeySource` คืน `env`/`none`. UI ช่องกรอก key เป็น no-op → cosmetic cleanup เลื่อนไป Phase 3 frontend
- **กลับ session-based MVP** (Phase 2) — บทสนทนาเก็บลง DB ทุก session; candidate set ไม่เก็บ (derive ใหม่)
- **เสริม [[docs/adr/0012]]** (Phase 2) — คำสั่งซื้อบันทึกลง `Orders` เพิ่มจากการยิงอีเมล (ไม่แทนที่)

## DI / Consequences
- ใช้ `AddDbContextFactory<InsureChatDbContext>` — store ที่เป็น **Singleton** (`SqlUserStore`,
  `SqlAppSettingsStore`) สร้าง `DbContext` สั้น ๆ ต่อ operation → กัน captive dependency กับ
  `SystemPromptProvider` (Singleton) ที่พึ่งพา `IAppSettingsStore`
- `DataInitializer` เติม admin + settings จาก JSON แบบ idempotent ตอน startup (ไม่ import secret)
- **Privacy:** `Conversations`/`Orders` (Phase 2) เก็บ PII (ชื่อ/เบอร์/บัตร) — อยู่ใน DB ภายในเท่านั้น
  (CLAUDE.md §12); แนะนำ retention job ภายหลัง
- **migration บน shared hosting:** ใช้ `dotnet ef migrations script` รันบน DB โดยตรง (ไม่พึ่ง `dotnet ef` บนเซิร์ฟเวอร์)
```

- [ ] **Step 2: อัปเดต `CONTEXT.md` — แถว Session (บรรทัด ~32)**

หา cell ของ **Session** เดิม:
> `| **Session** | การสนทนา 1 รอบ (ยังไม่บันทึกลง DB — session-based MVP) | ... |`

แก้เป็น:
> `| **Session** | การสนทนา 1 รอบ — **เก็บลง DB** ทุก session (`Conversations`+`ConversationTurns`, ADR 0016 Phase 2); candidate set ไม่เก็บ (derive จาก KPN ใหม่) | ... |`

- [ ] **Step 3: อัปเดต `CONTEXT.md` — แถว API key / App Password (บรรทัด ~26-27)**

เติมหมายเหตุท้าย cell ของ **API key (write-only)** และ **App Password (write-only)**:
> `> 🔄 **Superseded บางส่วน (ADR 0016):** admin แก้ key/รหัสผ่านผ่าน UI ไม่ได้แล้ว — secret มาจาก env เท่านั้น; key source คืน "env"/"none"`

- [ ] **Step 4: อัปเดต `CONTEXT.md` — เพิ่มศัพท์ใหม่ในหมวด "Proposal & Transaction" และ "Knowledge & Search"**

เพิ่มแถวในตารางหมวด 📋 Proposal & Transaction:
> `| **Order record** | คำสั่งซื้อที่บันทึกลงตาราง `Orders` (snapshot ลูกค้า/รถ/แผน/ราคา + status pipeline) เพิ่มจากการยิง [[#Order email]] (ADR 0016 Phase 2) | ตาราง Orders |`
> `| **Lead** | conversation ที่มีเบอร์ลูกค้าแล้วแต่ยังไม่มี Order — query เอาจาก `Conversations` (ไม่มีตารางแยก, ADR 0016) | — |`

เพิ่มแถวในตารางหมวด 🔍 Knowledge & Search:
> `| **KnowledgeArticle** | ตาราง sync วิกิ (file→DB ทางเดียว) ให้ chatbot keyword-search + admin ดู; ไฟล์ markdown ยังเป็น source of truth (ADR 0016 Phase 3) | ตาราง KnowledgeArticles |`

- [ ] **Step 5: Commit** *(ข้ามถ้าไม่ได้ git init)*

```bash
git add docs/adr/0016-insurechat-owns-sql-db-ef-core.md CONTEXT.md
git commit -m "docs: ADR 0016 (InsureChat SQL DB) + update CONTEXT glossary"
```

---

## Definition of Done (Phase 1)
- [ ] `dotnet build app/api` + `dotnet test app/api.Tests` ผ่านทั้งหมด
- [ ] ตาราง `Users`, `AppSettings`, `__EFMigrationsHistory` เกิดจริงบน DB `InsureChat`
- [ ] login admin ทำงาน (อ่าน hash จาก SQL), รหัสผิด = 401
- [ ] admin แก้ theme/prompt/LLM(non-secret)/email(non-secret) → persist ลง SQL
- [ ] key source = `env`/`none` (ตั้ง key ผ่าน admin ถูก strip)
- [ ] รันแอปซ้ำไม่ seed ซ้ำ (idempotent)
- [ ] ADR 0016 + CONTEXT.md อัปเดต

## ออกนอก scope Phase 1 (อยู่ Phase 2/3)
- Conversations/Turns/Needs, Orders, KnowledgeArticles + importer
- hook เก็บแชตใน `ChatbotService`/stream
- ลบช่องกรอก key/password ออกจาก Angular admin UI (cosmetic — Phase 3 + `/design-taste-frontend-v1`)
- retention/anonymize job สำหรับ PII
- ย้าย `EFDbConnection` (มี password) จาก appsettings.json ไป env เพื่อความสม่ำเสมอกับ KPN_DB_CONNECTION
```
