namespace SwiftRide.Messaging;

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "swiftride";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
