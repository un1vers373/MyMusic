using Microsoft.EntityFrameworkCore;

namespace MyMusic.Data;

public static class DbInitializer
{
  
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Database ready ({Provider}).", db.Database.ProviderName);
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning("Database is not ready (attempt {Attempt}/10): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
