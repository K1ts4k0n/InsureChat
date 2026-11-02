using System.IdentityModel.Tokens.Jwt;
using System.Text;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Data;
using Microsoft.EntityFrameworkCore;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Implementations;
using InsureChatBot.Api.Services.Interfaces;
using InsureChatBot.Api.Services.Llm;
using InsureChatBot.Api.Services.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// โหลด .env (KEY=VALUE) → env vars ก่อนอ่าน config (.env อยู่ที่ InsureChat root)
DotEnv.LoadFrom(builder.Environment.ContentRootPath);

// อย่า remap ชื่อ claim ขาเข้า — ให้คงชื่อ "role"/"name"/"custCode"/"email" ตามที่ออก token
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

const string CorsPolicy = "WebApp";

// --- Config binding ---
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MotorToolSettings>(builder.Configuration.GetSection("MotorTool"));
// path Python tool อยู่ใน vault (owner-only) — มาจาก env, ไม่ commit. contractor ไม่มี → fallback search ว่าง
builder.Services.PostConfigure<MotorToolSettings>(s =>
{
    var sp = Environment.GetEnvironmentVariable("MOTOR_SCRIPT_PATH");
    if (!string.IsNullOrWhiteSpace(sp)) s.ScriptPath = sp.Trim();
    var wd = Environment.GetEnvironmentVariable("MOTOR_WORKING_DIR");
    if (!string.IsNullOrWhiteSpace(wd)) s.WorkingDirectory = wd.Trim();
});
builder.Services.Configure<TBrokerSettings>(builder.Configuration.GetSection("TBroker"));
// อีเมลคำสั่งซื้อ (ADR 0012) — password จาก env SMTP_PASSWORD, To override ด้วย ORDER_EMAIL_TO
builder.Services.Configure<OrderEmailSettings>(builder.Configuration.GetSection("OrderEmail"));
builder.Services.PostConfigure<OrderEmailSettings>(s =>
{
    if (string.IsNullOrWhiteSpace(s.Password))
        s.Password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "";
    // บัญชีผู้ส่ง — Gmail บังคับ From = บัญชีที่ auth จึง set ทั้ง Username + From จาก env SMTP_USERNAME
    var user = Environment.GetEnvironmentVariable("SMTP_USERNAME");
    if (!string.IsNullOrWhiteSpace(user)) { s.Username = user.Trim(); s.From = user.Trim(); }
    // ปลายทาง: ORDER_EMAIL_TO ถ้าตั้ง, ไม่งั้นส่งหาตัวเอง (= ผู้ส่ง) เพื่อให้เห็นใน inbox เดียวกัน
    var to = Environment.GetEnvironmentVariable("ORDER_EMAIL_TO");
    s.To = !string.IsNullOrWhiteSpace(to) ? to.Trim() : s.Username;
});
builder.Services.Configure<UserStoreSettings>(builder.Configuration.GetSection("UserStore"));
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"));
// API key มาจาก env OPENROUTER_API_KEY (ไม่เก็บใน appsettings ที่ commit)
builder.Services.PostConfigure<LlmSettings>(s =>
{
    if (string.IsNullOrWhiteSpace(s.ApiKey))
        s.ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
});
// KPN SQL DB (read-only, สด) — connstring จาก env KPN_DB_CONNECTION (.env, ไม่ commit). ดู ADR 0004
builder.Services.Configure<KpnDbSettings>(builder.Configuration.GetSection("KpnDb"));
builder.Services.PostConfigure<KpnDbSettings>(s =>
{
    if (string.IsNullOrWhiteSpace(s.ConnectionString))
        s.ConnectionString = Environment.GetEnvironmentVariable("KPN_DB_CONNECTION") ?? "";
});

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
// signing key จาก env (ไม่ commit). ถ้าไม่ตั้ง → ใช้ค่าใน appsettings (dev/ว่าง)
var jwtKeyEnv = Environment.GetEnvironmentVariable("JWT_KEY");
if (!string.IsNullOrWhiteSpace(jwtKeyEnv)) jwt.Key = jwtKeyEnv.Trim();
builder.Services.PostConfigure<JwtSettings>(s =>
{
    if (!string.IsNullOrWhiteSpace(jwtKeyEnv)) s.Key = jwtKeyEnv.Trim();
});

// LINE Messaging Settings
builder.Services.Configure<LineSettings>(builder.Configuration.GetSection("Line"));
builder.Services.PostConfigure<LineSettings>(s =>
{
    var token = Environment.GetEnvironmentVariable("LINE_CHANNEL_ACCESS_TOKEN");
    if (!string.IsNullOrWhiteSpace(token)) s.ChannelAccessToken = token.Trim();
    var secret = Environment.GetEnvironmentVariable("LINE_CHANNEL_SECRET");
    if (!string.IsNullOrWhiteSpace(secret)) s.ChannelSecret = secret.Trim();
    var admin = Environment.GetEnvironmentVariable("LINE_ADMIN_USER_ID");
    if (!string.IsNullOrWhiteSpace(admin)) s.AdminUserId = admin.Trim();
});

// Services ---
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<InsureChatBot.Api.Services.Line.ILineService, InsureChatBot.Api.Services.Line.LineService>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Line.ILineSessionManager, InsureChatBot.Api.Services.Line.LineSessionManager>();

// EF Core — DB "InsureChat" ของแอปเอง (ADR 0016).
// รองรับทั้ง SQL Server และ SQLite อัตโนมัติ (ถ้าไม่ได้ระบุ SQL Server หรือเริ่มด้วย "Data Source=" จะใช้ SQLite ทันที)
var efConn = Environment.GetEnvironmentVariable("EF_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(efConn)) efConn = builder.Configuration.GetConnectionString("EFDbConnection");

var useSqlite = string.IsNullOrWhiteSpace(efConn) || efConn.Contains(".db", StringComparison.OrdinalIgnoreCase) || efConn.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);
if (useSqlite && string.IsNullOrWhiteSpace(efConn))
{
    var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dataDir);
    efConn = $"Data Source={Path.Combine(dataDir, "insurechat.db")}";
}

builder.Services.AddDbContextFactory<InsureChatDbContext>(o =>
{
    if (useSqlite)
        o.UseSqlite(efConn);
    else
        o.UseSqlServer(efConn);
});
builder.Services.AddSingleton<IUserStore, SqlUserStore>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
// settings ที่ admin แก้ runtime (theme/persona/LLM) — persist app-settings.json (ADR 0010/0011)
builder.Services.AddSingleton<IAppSettingsStore, SqlAppSettingsStore>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Conversation.IConversationStore, InsureChatBot.Api.Services.Conversation.EfConversationStore>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Orders.IOrderStore, InsureChatBot.Api.Services.Orders.EfOrderStore>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Knowledge.IKnowledgeStore, InsureChatBot.Api.Services.Knowledge.KnowledgeSyncService>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Admin.IAdminQueryService, InsureChatBot.Api.Services.Admin.EfAdminQueryService>();
builder.Services.AddScoped<IMotorQueryService, MotorQueryService>();
builder.Services.AddScoped<IProposalService, ProposalService>();
builder.Services.AddScoped<InsureChatBot.Api.Services.Email.IOrderEmailService, InsureChatBot.Api.Services.Email.OrderEmailService>();
builder.Services.AddScoped<InsureChatBot.Api.Services.Motor.IMotorFacetService, InsureChatBot.Api.Services.Motor.MotorFacetSqlService>();
builder.Services.AddScoped<InsureChatBot.Api.Services.Document.IPdfRasterizer, InsureChatBot.Api.Services.Document.PdfRasterizer>();
builder.Services.AddHttpClient<InsureChatBot.Api.Services.Document.IDocumentExtractionService, InsureChatBot.Api.Services.Document.DocumentExtractionService>();

// LLM conversation engine (ADR 0003): client + system prompt + 2 engines + router
builder.Services.AddHttpClient<IChatCompletionClient, OpenAiCompatibleClient>();
builder.Services.AddSingleton<SystemPromptProvider>();
builder.Services.AddScoped<LlmConversationEngine>();
builder.Services.AddScoped<StateMachineConversationEngine>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// --- Auth (JWT bearer) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key ?? "")),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization();

// --- CORS (Angular dev) ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.SetIsOriginAllowed(origin =>
        allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
        || (Uri.TryCreate(origin, UriKind.Absolute, out var u)
            && u.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase)))
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// เติมข้อมูลตั้งต้น (idempotent) — ต้องรันหลังตารางถูกสร้างด้วย migration แล้ว (ADR 0016)
// ใช้ TryRun: DB พังต้องไม่ฆ่า process ตอน startup (จะกลายเป็น HTTP 502.5 ที่หาสาเหตุไม่ได้)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InsureChatDbContext>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataInitializer");
    DataInitializer.TryRun(factory, app.Environment.ContentRootPath, logger);

    try
    {
        var kStore = scope.ServiceProvider.GetRequiredService<InsureChatBot.Api.Services.Knowledge.IKnowledgeStore>();
        var syncedCount = kStore.Sync();
        logger.LogInformation("Knowledge sync completed: {Count} articles synced to database.", syncedCount);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to auto-sync knowledge articles on startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    // Swagger (dev-only) — Swashbuckle serve document ที่ /swagger/v1/swagger.json, UI ที่ /swagger
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "InsureChat API v1"));
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
