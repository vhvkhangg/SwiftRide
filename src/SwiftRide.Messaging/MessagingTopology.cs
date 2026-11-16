using MassTransit;
using SwiftRide.Contracts.Matching;
using SwiftRide.Contracts.Payments;
using SwiftRide.Contracts.Trips;

namespace SwiftRide.Messaging;

public static class MessagingTopology
{
    public static void Configure(IRabbitMqBusFactoryConfigurator bus)
    {
        // Mỗi loại message có một exchange riêng, dùng chung giữa các service.
        bus.Message<TripRequestedV1>(x => x.SetEntityName("swiftride-trip-requested-v1"));
        bus.Message<DriverMatchedV1>(x => x.SetEntityName("swiftride-driver-matched-v1"));
        bus.Message<DriverMatchFailedV1>(x => x.SetEntityName("swiftride-driver-match-failed-v1"));
        bus.Message<FindDriverForTripV1>(x => x.SetEntityName("swiftride-find-driver-for-trip-v1"));
        bus.Message<ReleaseDriverV1>(x => x.SetEntityName("swiftride-release-driver-v1"));
        bus.Message<Fault<ProcessPaymentV1>>(x => x.SetEntityName("swiftride-payment-fault-v1"));
        bus.Message<DriverFoundForTripV1>(x => x.SetEntityName("swiftride-driver-found-for-trip-v1"));
        bus.Message<NoDriverFoundForTripV1>(x => x.SetEntityName("swiftride-no-driver-found-for-trip-v1"));
        bus.Message<DriverReservationConfirmedV1>(x => x.SetEntityName("swiftride-driver-reservation-confirmed-v1"));
        bus.Message<DriverReservationReleasedV1>(x => x.SetEntityName("swiftride-driver-reservation-released-v1"));
        bus.Message<ProcessPaymentV1>(x => x.SetEntityName("swiftride-process-payment-v1"));
        bus.Message<PaymentSucceededV1>(x => x.SetEntityName("swiftride-payment-succeeded-v1"));
        bus.Message<PaymentFailedV1>(x => x.SetEntityName("swiftride-payment-failed-v1"));
        // Saga thanh toán
        bus.Message<TripDroppedOffV1>(x => x.SetEntityName("swiftride-trip-dropped-off-v1"));
        bus.Message<TripPaymentTimedOutV1>(x => x.SetEntityName("swiftride-trip-payment-timed-out-v1"));
        bus.Message<RefundPaymentV1>(x => x.SetEntityName("swiftride-refund-payment-v1"));
        bus.Message<PaymentRefundedV1>(x => x.SetEntityName("swiftride-payment-refunded-v1"));
    }

    public static void ConfigureQueues(IRabbitMqBusFactoryConfigurator bus)
    {
        // Chỉ gọi từ chương trình Setup với DeployTopologyOnly = true.
        // Tạo queue/binding nhưng không lấy message ra khỏi queue.
        bus.ReceiveEndpoint(MessagingEndpoints.MatchingFindDriverForTrip,
            endpoint => endpoint.ConfigureConsumeTopology = false);
        bus.ReceiveEndpoint(MessagingEndpoints.MatchingReleaseDriver,
            endpoint => endpoint.ConfigureConsumeTopology = false);
        bus.ReceiveEndpoint(MessagingEndpoints.TripPaymentFault,
            endpoint => endpoint.Bind<Fault<ProcessPaymentV1>>());
        bus.ReceiveEndpoint(MessagingEndpoints.MatchingReservationConfirmed,
            endpoint => endpoint.Bind<DriverReservationConfirmedV1>());
        bus.ReceiveEndpoint(MessagingEndpoints.MatchingReservationReleased,
            endpoint => endpoint.Bind<DriverReservationReleasedV1>());

        bus.ReceiveEndpoint(MessagingEndpoints.TripDriverMatched,
            endpoint => endpoint.Bind<DriverMatchedV1>());

        bus.ReceiveEndpoint(MessagingEndpoints.TripDriverMatchFailed,
            endpoint => endpoint.Bind<DriverMatchFailedV1>());
        bus.ReceiveEndpoint(MessagingEndpoints.TripDriverFoundForTrip,
            endpoint => endpoint.Bind<DriverFoundForTripV1>());
        bus.ReceiveEndpoint(MessagingEndpoints.TripNoDriverFoundForTrip,
            endpoint => endpoint.Bind<NoDriverFoundForTripV1>());

        bus.ReceiveEndpoint(MessagingEndpoints.PaymentProcessPayment, endpoint =>
        {
            endpoint.ConfigureConsumeTopology = false;
        });

        bus.ReceiveEndpoint(MessagingEndpoints.TripPaymentSucceeded,
            endpoint => endpoint.Bind<PaymentSucceededV1>());

        bus.ReceiveEndpoint(MessagingEndpoints.TripPaymentFailed,
            endpoint => endpoint.Bind<PaymentFailedV1>());

        // Saga thanh toán nhận cả event của Trip và kết quả từ Payment; tạo trước để không mất message.
        bus.ReceiveEndpoint(MessagingEndpoints.TripPaymentSaga, endpoint =>
        {
            endpoint.ConfigureConsumeTopology = false;
            endpoint.Bind<TripDroppedOffV1>();
            endpoint.Bind<TripPaymentTimedOutV1>();
            endpoint.Bind<PaymentSucceededV1>();
            endpoint.Bind<PaymentFailedV1>();
            endpoint.Bind<Fault<ProcessPaymentV1>>();
            endpoint.Bind<PaymentRefundedV1>();
        });

        bus.ReceiveEndpoint(MessagingEndpoints.PaymentRefundPayment,
            endpoint => endpoint.ConfigureConsumeTopology = false);
    }
}
