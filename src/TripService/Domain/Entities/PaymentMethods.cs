namespace SwiftRide.TripService.Domain.Entities;

/// <summary>Payment methods a Rider can choose; names match Payment Service's PaymentMethodType.</summary>
public static class PaymentMethods
{
    public const string Wallet = "Wallet";
    public const string Card = "Card";

    public static bool IsSupported(string? value) => value is Wallet or Card;

    /// <summary>Omitted means Wallet; known names are case-insensitive; anything else returns null.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Wallet;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, Wallet, System.StringComparison.OrdinalIgnoreCase)) return Wallet;
        if (string.Equals(trimmed, Card, System.StringComparison.OrdinalIgnoreCase)) return Card;
        return null;
    }
}
