using Presentation.Models;

namespace Presentation.Services;

public interface IVerificationService
{
    Task<VerificationServiceResult> SenderVerificationCodeAsync(SendVerificationCodeRequest request);
    void SaveVerificationCode(SaveVerificationCodeRequest request);

    Task<VerificationServiceResult> VerifyVerificationCodeAsync(VerifyVerificationCodeRequest request);
}
