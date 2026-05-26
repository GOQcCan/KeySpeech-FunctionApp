namespace Keyspeech.FunctionApp.Models
{
    public class OrderDetails
    {
        public string OrderId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
