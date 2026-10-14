namespace SwiftRide.Messaging;

// Dùng cùng tên này khi đăng ký consumer và khi gửi command.
public static class MessagingEndpoints
{
    public const string MatchingTripRequested = "swiftride-matching-trip-requested";
    public const string TripDriverMatched = "swiftride-trip-driver-matched";
    public const string TripDriverMatchFailed = "swiftride-trip-driver-match-failed";
    public const string PaymentProcessPayment = "swiftride-payment-process-payment";
    public const string TripPaymentSucceeded = "swiftride-trip-payment-succeeded";
    public const string TripPaymentFailed = "swiftride-trip-payment-failed";
}
