using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Validation;

public class CreateOrderRequestValidator : IValidator<CreateOrderRequest>
{
    public ValidationResult Validate(CreateOrderRequest? instance)
    {
        var errors = new List<string>();

        if (instance == null)
        {
            errors.Add("Request cannot be null");
            return ValidationResult.Failure([.. errors]);
        }

        if (string.IsNullOrWhiteSpace(instance.HardwareId))
        {
            errors.Add("HardwareId is required");
        }
        else if (instance.HardwareId.Length < 10)
        {
            errors.Add("HardwareId must be at least 10 characters");
        }

        return errors.Count == 0 
            ? ValidationResult.Success() 
            : ValidationResult.Failure([.. errors]);
    }
}
