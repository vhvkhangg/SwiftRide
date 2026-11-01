namespace SwiftRide.Messaging;

// Dùng cùng tên này khi đăng ký consumer và khi gửi command.
public static class MessagingEndpoints
{
    public const string MatchingReleaseDriver = "swiftride-matching-release-driver";
    public const string TripPaymentFault = "swiftride-trip-payment-fault";
    public const string MatchingTripRequested = "swiftride-matching-trip-requested";
    public const string MatchingFindDriverForTrip = "swiftride-matching-find-driver-for-trip";
    public const string MatchingReservationConfirmed = "swiftride-matching-reservation-confirmed";
    public const string MatchingReservationReleased = "swiftride-matching-reservation-released";
    public const string TripDriverMatched = "swiftride-trip-driver-matched";
    public const string TripDriverMatchFailed = "swiftride-trip-driver-match-failed";
    public const string TripDriverFoundForTrip = "swiftride-trip-driver-found-for-trip";
    public const string TripNoDriverFoundForTrip = "swiftride-trip-no-driver-found-for-trip";
    public const string PaymentProcessPayment = "swiftride-payment-process-payment";
    public const string TripPaymentSucceeded = "swiftride-trip-payment-succeeded";
    public const string TripPaymentFailed = "swiftride-trip-payment-failed";
}
