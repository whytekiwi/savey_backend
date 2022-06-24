using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Savey.Startup))]
namespace Savey
{
    /// <summary>
    /// Configures the DI container for this application
    /// </summary>
    public class Startup : FunctionsStartup
    {
        /// <summary>
        /// Configures the dependencies globally
        /// </summary>
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Log the cloud config
            builder.Services.AddOptions<CloudStorageConfiguration>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("CloudConfiguration").Bind(settings);
                });
            
            // Inject our dependencies
            builder.Services
                .AddSingleton<IBlobContainerClientProvider, BlobContainerClientProvider>()
                .AddSingleton<ISavingsFileDataManager, SavingsFileDataManager>();
        }
    }
}