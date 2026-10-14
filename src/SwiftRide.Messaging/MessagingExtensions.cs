using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SwiftRide.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddSwiftRideMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? registerConsumers = null,
        Action<IRabbitMqBusFactoryConfigurator>? configureBus = null)
    {
        var settings = configuration.GetSection("RabbitMq").Get<RabbitMqSettings>();

        if (settings == null ||
            string.IsNullOrWhiteSpace(settings.Host) ||
            settings.Port == 0 ||
            string.IsNullOrWhiteSpace(settings.VirtualHost) ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException(
                "RabbitMq cần Host, Port, VirtualHost, Username và Password hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Cần tên service.", nameof(serviceName));
        }

        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });

        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(35);
        });

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(
                "swiftride-" + serviceName, false));

            // NV1–NV3 đăng ký consumer nghiệp vụ của mình qua tham số này.
            if (registerConsumers != null)
            {
                registerConsumers(x);
            }

            x.AddConfigureEndpointsCallback((name, endpoint) =>
            {
                endpoint.UseMessageRetry(retry =>
                {
                    // Dữ liệu không hợp lệ không được thử lại.
                    retry.Ignore<ArgumentException>();
                    retry.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(5));
                });

                if (endpoint is IRabbitMqReceiveEndpointConfigurator rabbitEndpoint)
                {
                    rabbitEndpoint.Durable = true;
                    rabbitEndpoint.AutoDelete = false;
                    rabbitEndpoint.PrefetchCount = 16;
                    rabbitEndpoint.ConcurrentMessageLimit = 8;

                    // Command thanh toán chỉ nhận qua Send đến endpoint này.
                    if (name == MessagingEndpoints.PaymentProcessPayment)
                    {
                        rabbitEndpoint.ConfigureConsumeTopology = false;
                    }
                }

                // MassTransit tự chuyển lỗi vào _error, sai loại message vào _skipped.
            });

            x.UsingRabbitMq((context, bus) =>
            {
                bus.Host(settings.Host, settings.Port, settings.VirtualHost, host =>
                {
                    host.Username(settings.Username);
                    host.Password(settings.Password);
                });

                MessagingTopology.Configure(bus);
                if (configureBus != null)
                {
                    configureBus(bus);
                }

                bus.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<MessagePublisher>();

        return services;
    }
}
