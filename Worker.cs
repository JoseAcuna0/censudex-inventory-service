using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace inventory_service;

public class Worker : BackgroundService
{
    private IConnection _connection = null!;
    private IModel _channel = null!;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare("inventory_check", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare("inventory_response", durable: true, exclusive: false, autoDelete: false);

        Console.WriteLine("Inventory Worker started");
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += (sender, ea) =>
        {
            var orderId = Encoding.UTF8.GetString(ea.Body.ToArray());
            Console.WriteLine($"Received stock check for order: {orderId}");

            bool hasStock = new Random().Next(2) == 1;

            var response = JsonSerializer.Serialize(new
            {
                OrderId = orderId,
                HasStock = hasStock
            });

            _channel.BasicPublish("", "inventory_response", null, Encoding.UTF8.GetBytes(response));
            Console.WriteLine($"Sent stock response: {response}");

            _channel.BasicAck(ea.DeliveryTag, false);
        };

        _channel.BasicConsume("inventory_check", false, consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
