using InsureChatBot.Api.Models.Auth;

namespace InsureChatBot.Api.Services.Interfaces
{
    public interface IJwtTokenService
    {
        string CreateToken(User user);
    }
}
