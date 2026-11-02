using InsureChatBot.Api.Models.Admin;
using InsureChatBot.Api.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    /// <summary>settings สาธารณะ — ให้หน้า public ดึงธีมก่อน auth ได้ (ADR 0010/0011)</summary>
    [AllowAnonymous]
    [Route("api/Settings")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IAppSettingsStore _store;

        public SettingsController(IAppSettingsStore store) => _store = store;

        /// <summary>สีแบรนด์ระดับแอป — client เรียกตอน bootstrap แล้ว set CSS var --brand</summary>
        [HttpGet("Theme")]
        public ActionResult<ThemeDto> Theme()
            => Ok(new ThemeDto { Brand = _store.Get().Theme?.Brand ?? "#2563eb" });
    }
}
