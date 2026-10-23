using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwiftRide.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSwiftRideMessaging(
    builder.Configuration,
    "setup",
    configureBus: bus =>
    {
        bus.DeployTopologyOnly = true;
        MessagingTopology.ConfigureQueues(bus);
    });

using var host = builder.Build();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var bus = host.Services.GetRequiredService<IBusControl>();
await bus.DeployAsync(timeout.Token);

Console.WriteLine("Đã tạo các exchange, queue và binding của SwiftRide.");
