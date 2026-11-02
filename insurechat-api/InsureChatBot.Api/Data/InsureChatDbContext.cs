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
        public DbSet<ConversationRow> Conversations => Set<ConversationRow>();
        public DbSet<ConversationTurnRow> ConversationTurns => Set<ConversationTurnRow>();
        public DbSet<ConversationNeedRow> ConversationNeeds => Set<ConversationNeedRow>();
        public DbSet<OrderRow> Orders => Set<OrderRow>();
        public DbSet<KnowledgeArticleRow> KnowledgeArticles => Set<KnowledgeArticleRow>();

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

            b.Entity<ConversationRow>(e =>
            {
                e.ToTable("Conversations");
                e.HasKey(x => x.Id);
                e.Property(x => x.SessionKey).HasMaxLength(40).IsRequired();
                e.HasIndex(x => x.SessionKey).IsUnique();
                e.Property(x => x.ProductType).HasMaxLength(16);
                e.Property(x => x.CarBrand).HasMaxLength(64);
                e.Property(x => x.CarModel).HasMaxLength(64);
                e.Property(x => x.InsuranceClass).HasMaxLength(32);  // รองรับชื่อชั้นไทย เช่น "ประเภท 2 พิเศษ" (กัน SQL 8152 truncate → order ไม่ลง)
                e.Property(x => x.SumInsure).HasPrecision(18, 2);
                e.Property(x => x.CurrentStep).HasMaxLength(32);
                e.Property(x => x.ProspectName).HasMaxLength(128);
                e.Property(x => x.ProspectPhone).HasMaxLength(32);
                e.Property(x => x.ProspectEmail).HasMaxLength(256);
                e.Property(x => x.ProspectIdCard).HasMaxLength(32);
                e.Property(x => x.ProspectLicensePlate).HasMaxLength(32);
                e.Property(x => x.OutcomeStatus).HasMaxLength(16);
                e.HasMany(x => x.Turns).WithOne(t => t.Conversation)
                    .HasForeignKey(t => t.ConversationId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Needs).WithOne(n => n.Conversation)
                    .HasForeignKey(n => n.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });
            b.Entity<ConversationTurnRow>(e =>
            {
                e.ToTable("ConversationTurns");
                e.HasKey(x => x.Id);
                e.Property(x => x.Role).HasMaxLength(16);
                e.HasIndex(x => new { x.ConversationId, x.Seq });
            });
            b.Entity<ConversationNeedRow>(e =>
            {
                e.ToTable("ConversationNeeds");
                e.HasKey(x => x.Id);
                e.Property(x => x.Need).HasMaxLength(256);
            });
            b.Entity<OrderRow>(e =>
            {
                e.ToTable("Orders");
                e.HasKey(x => x.Id);
                e.Property(x => x.OrderNo).HasMaxLength(32).IsRequired();
                e.HasIndex(x => x.OrderNo).IsUnique();
                e.Property(x => x.CustName).HasMaxLength(128);
                e.Property(x => x.CustPhone).HasMaxLength(32);
                e.Property(x => x.CustEmail).HasMaxLength(256);
                e.Property(x => x.CustIdCard).HasMaxLength(32);
                e.Property(x => x.LicensePlate).HasMaxLength(32);
                e.Property(x => x.Brand).HasMaxLength(64);
                e.Property(x => x.Model).HasMaxLength(64);
                e.Property(x => x.InsuranceClass).HasMaxLength(32);  // รองรับชื่อชั้นไทย เช่น "ประเภท 2 พิเศษ" (กัน SQL 8152 truncate → order ไม่ลง)
                e.Property(x => x.InsurerName).HasMaxLength(128);
                e.Property(x => x.Status).HasMaxLength(16);
                e.Property(x => x.EmailStatus).HasMaxLength(16);
                e.Property(x => x.SumInsure).HasPrecision(18, 2);
                e.Property(x => x.Premium).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);
            });

            b.Entity<KnowledgeArticleRow>(e =>
            {
                e.ToTable("KnowledgeArticles");
                e.HasKey(x => x.Id);
                e.Property(x => x.Slug).HasMaxLength(256).IsRequired();
                e.HasIndex(x => x.Slug).IsUnique();
                e.Property(x => x.Title).HasMaxLength(256);
                e.Property(x => x.Type).HasMaxLength(32);
                e.Property(x => x.SourceFile).HasMaxLength(512);
                e.Property(x => x.ContentHash).HasMaxLength(64);
            });
        }
    }
}
