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
            }
            await host.RunAsync();
        }
    }
}