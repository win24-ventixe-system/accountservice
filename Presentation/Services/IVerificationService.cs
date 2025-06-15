using Presentation.Models;

namespace Presentation.Services;

public interface IVerificationService
{
    Task<VerificationServiceResult> SenderVerificationCodeAsync(SendVerificationCodeRequest request);
    void SaveVerificationCode(SaveVerificationCodeRequest request);

    VerificationServiceResult VerifyVerificationCode(VerifyVerificationCodeRequest request);
}
