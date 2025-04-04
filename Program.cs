namespace LapTrinhWindows
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = HostBuilderConfig.CreateHostBuilder(args).Build();
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                // try
                // {
                //     var context = services.GetRequiredService<ApplicationDbContext>();
                //     Console.WriteLine("DbContext resolved successfully.");
                //     SeedData.InitializeData(context);
                // }
                // catch (Exception ex)
                // {
                //     Console.WriteLine($"Lỗi rồi: {ex.Message}");
                // }
            }
            await host.RunAsync();
        }
    }
}