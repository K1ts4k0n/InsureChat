using InsureChatBot.Api.Models.Motor;

namespace InsureChatBot.Api.Services.Interfaces
{
    public interface IProposalService
    {
        Task<ProposalSubmissionResponse> SubmitMotorProposalAsync(
            string userCode,
            string userName,
            string userEmail,
            MotorProposalData proposalData);
    }
}
