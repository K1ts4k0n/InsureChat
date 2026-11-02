using System.Net;
using System.Text;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Services.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace InsureChatBot.Api.Services.Email
{
    /// <summary>
    /// ส่งคำสั่งซื้อทางอีเมลถึงโบรคเกอร์ผ่าน Gmail SMTP (MailKit) — ADR 0012/0013.
    /// อ่าน settings (บัญชีผู้ส่ง/ปลายทาง/password) จาก store ตอนส่งแต่ละครั้ง → admin แก้แล้วมีผลทันที (ADR 0013).
    /// Password ว่าง → mock (log + success); ตั้งแล้ว → ส่งจริง, ล้ม → error.
    /// </summary>
    public class OrderEmailService : IOrderEmailService
    {
        private readonly IAppSettingsStore _store;
        private readonly ILogger<OrderEmailService> _log;

        public OrderEmailService(IAppSettingsStore store, ILogger<OrderEmailService> log)
        {
            _store = store;
            _log = log;
        }

        public async Task<OrderEmailResult> SendAsync(OrderEmailModel m, CancellationToken ct = default)
        {
            var s = _store.GetEffectiveEmail();
            var subject = $"คำสั่งซื้อประกันรถ — {m.CustomerName} ({m.Brand} {m.Model} {m.Year})";
            var html = BuildHtml(m);

            if (!s.IsConfigured)
            {
                // mock: ยังไม่ตั้ง app password → ไม่ส่งจริง แต่ถือว่าสำเร็จ (ดู ADR 0012)
                _log.LogInformation("[OrderEmail:MOCK] To={To} | {Subject}\n{Html}", s.To, subject, html);
                return new OrderEmailResult(true, null);
            }

            try
            {
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse(s.From));
                msg.To.Add(MailboxAddress.Parse(s.To));
                // ให้เจ้าหน้าที่กด reply ตรงถึงลูกค้าได้ ถ้ามีอีเมล
                if (!string.IsNullOrWhiteSpace(m.CustomerEmail) && MailboxAddress.TryParse(m.CustomerEmail, out var cust))
                    msg.ReplyTo.Add(cust);
                msg.Subject = subject;
                msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

                await SendViaSmtpAsync(msg, s, ct);
                return new OrderEmailResult(true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[OrderEmail] ส่งอีเมลคำสั่งซื้อล้มเหลว");
                return new OrderEmailResult(false, ex.Message);
            }
        }

        public async Task<OrderEmailResult> SendTestAsync(CancellationToken ct = default)
        {
            var s = _store.GetEffectiveEmail();
            if (!s.IsConfigured)
                return new OrderEmailResult(false, "ยังไม่ได้ตั้ง App Password — ส่งจริงไม่ได้ (ตอนนี้เป็นโหมด mock)");

            try
            {
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse(s.From));
                msg.To.Add(MailboxAddress.Parse(s.To));
                msg.Subject = "ทดสอบการตั้งค่าอีเมล — InsureChat";
                msg.Body = new BodyBuilder
                {
                    HtmlBody = @"<div style=""font-family:'Segoe UI',Tahoma,sans-serif;color:#0f172a"">
                        <h3 style=""margin:0 0 6px"">อีเมลทดสอบจาก InsureChat</h3>
                        <p>ถ้าคุณเห็นอีเมลนี้ แปลว่าการตั้งค่าบัญชีผู้ส่งและปลายทางถูกต้องแล้ว ✅</p>
                        <p style=""color:#94a3b8;font-size:12px"">ส่งจากหน้า /admin เพื่อตรวจสอบการเชื่อมต่อ Gmail SMTP</p>
                        </div>"
                }.ToMessageBody();

                await SendViaSmtpAsync(msg, s, ct);
                return new OrderEmailResult(true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[OrderEmail] ส่งอีเมลทดสอบล้มเหลว");
                return new OrderEmailResult(false, ex.Message);
            }
        }

        private static async Task SendViaSmtpAsync(MimeMessage msg, OrderEmailSettings s, CancellationToken ct)
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(s.Host, s.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(s.Username, s.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }

        private static string BuildHtml(OrderEmailModel m)
        {
            string Esc(string s) => WebUtility.HtmlEncode(s ?? "");
            string Money(decimal v) => v.ToString("N0");

            var sb = new StringBuilder();
            sb.Append(@"<div style=""font-family:'Segoe UI',Tahoma,sans-serif;color:#0f172a;max-width:560px"">");
            sb.Append(@"<h2 style=""margin:0 0 4px"">คำสั่งซื้อประกันรถยนต์</h2>");
            sb.Append(@"<p style=""color:#64748b;margin:0 0 16px"">ลูกค้าส่งคำสั่งซื้อผ่าน InsureChat — กรุณาติดต่อกลับเพื่อดำเนินการออกกรมธรรม์</p>");

            sb.Append(@"<h3 style=""margin:16px 0 6px"">ข้อมูลลูกค้า</h3>");
            sb.Append(@"<table style=""border-collapse:collapse;width:100%;font-size:14px"">");
            Row(sb, "ชื่อ", Esc(m.CustomerName));
            Row(sb, "เบอร์โทร", Esc(m.CustomerPhone));
            if (!string.IsNullOrWhiteSpace(m.CustomerEmail)) Row(sb, "อีเมล", Esc(m.CustomerEmail));
            if (!string.IsNullOrWhiteSpace(m.IdCard)) Row(sb, "เลขบัตรประชาชน", Esc(m.IdCard));
            if (!string.IsNullOrWhiteSpace(m.LicensePlate)) Row(sb, "ทะเบียนรถ", Esc(m.LicensePlate));
            sb.Append("</table>");

            sb.Append(@"<h3 style=""margin:16px 0 6px"">แพคเกจที่เลือก</h3>");
            sb.Append(@"<table style=""border-collapse:collapse;width:100%;font-size:14px"">");
            Row(sb, "บริษัทประกัน", Esc(m.InsurerName));
            Row(sb, "ชั้นประกัน", Esc(m.InsuranceClass));
            Row(sb, "รถ", $"{Esc(m.Brand)} {Esc(m.Model)} ปี {m.Year}");
            Row(sb, "ทุนประกัน", Money(m.SumInsure) + " บาท");
            Row(sb, "เบี้ยสุทธิ/ปี", Money(m.PremiumNet) + " บาท");
            Row(sb, "เบี้ยรวมอากร/ปี", Money(m.PremiumTotal) + " บาท");
            sb.Append("</table>");

            sb.Append(@"<p style=""color:#94a3b8;font-size:12px;margin-top:16px"">เบี้ยเป็นเรตแพคเกจ ต้องยืนยันกับตัวแทนก่อนออกกรมธรรม์จริง</p>");
            sb.Append("</div>");
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, string label, string value)
        {
            sb.Append(@"<tr><td style=""padding:6px 10px;border:1px solid #e4e9f2;background:#f8fafc;color:#64748b;width:40%"">");
            sb.Append(WebUtility.HtmlEncode(label));
            sb.Append(@"</td><td style=""padding:6px 10px;border:1px solid #e4e9f2;font-weight:600"">");
            sb.Append(value);
            sb.Append("</td></tr>");
        }
    }
}
