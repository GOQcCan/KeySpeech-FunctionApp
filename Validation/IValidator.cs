using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Validation
{
    public interface IValidator<T>
    {
        ValidationResult Validate(T? instance);
    }
}
