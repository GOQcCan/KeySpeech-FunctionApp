namespace Keyspeech.FunctionApp.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<string> Errors { get; init; } = [];

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(params string[] errors) =>
            new() { IsValid = false, Errors = [.. errors] };
    }
}
