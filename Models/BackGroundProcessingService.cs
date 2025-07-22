using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System;

public class BackgroundProcessingService : BackgroundService
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundProcessingService(Channel<Func<CancellationToken, Task>> queue)
    {
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Background task error: {ex.Message}");
            }
        }
    }
}
