using InsureChatBot.Api.Auth;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    [Route("api/Auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserStore _userStore;
        private readonly IJwtTokenService _jwt;

        public AuthController(IUserStore userStore, IJwtTokenService jwt)
        {
            _userStore = userStore;
            _jwt = jwt;
        }

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { message = "กรุณากรอก username และ password" });

            var user = _userStore.FindByUsername(request.Username);
            if (user == null || !PasswordHasher.Verify(request.Password ?? "", user.PasswordHash))
                return Unauthorized(new { message = "username หรือ password ไม่ถูกต้อง" });

            var token = _jwt.CreateToken(user);

            return Ok(new LoginResponse
            {
                Token = token,
                Role = user.Role,
                Name = user.Name,
                Email = user.Email,
                CustCode = user.CustCode
            });
        }

        // Register ถูกตัดออก (ADR 0011) — chatbot เป็น public ไม่มีบัญชีลูกค้า; admin seed เท่านั้น
    }
}
