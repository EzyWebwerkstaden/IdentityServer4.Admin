using System;
using System.IO;
using EzyNet.Gcp.SecretManager.SerilogSupport;
using EzyNet.Serilog.Bootstrap;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Skoruba.IdentityServer4.STS.Identity.Configuration;

namespace Skoruba.IdentityServer4.STS.Identity
{
    public class Program
    {
        private static IConfiguration _bootstrapperConfig;
        private static string? _subEnvironment;
        private static string _currentDir;
        private static PhysicalFileProvider _fp;
        private static string? _environment;

        public static void Main(string[] args)
        {
            _currentDir = Directory.GetCurrentDirectory();
            _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _subEnvironment = Environment.GetEnvironmentVariable("SUB_ENVIRONMENT");
            _fp = GetConsumerProjectSettingsFileProvider(_currentDir);
            SerilogBootstrapper.Bootstrap("serilog", "ezy.id.sts", null, _fp);
            try
            {
                _bootstrapperConfig = GetBootstrapperConfig(args);
                
                // EZYC-2851-modification: we're not using default way of dockerizing.
                //DockerHelpers.ApplyDockerConfiguration(configuration);
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IConfiguration GetBootstrapperConfig(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(_currentDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(_fp, "appsettings._Shared.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"appsettings.{_environment}.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"appsettings.{_environment}.{_subEnvironment}.json", optional: true, reloadOnChange: true);

            var isDevelopment = _environment == Environments.Development;
            if (isDevelopment)
            {
                configurationBuilder.AddUserSecrets<Startup>();
            }
            
            // TODO: To be able to generate & migrate databases in AWS, migrate AWS SM also here.
            // EZY-modification (EZYC-4328): GCP Secret Manager support
            configurationBuilder.AddGoogleSecretManagerIfEnabled("appsettings");

            // EZY-modification (EZYC-3029) - disabled Azure key Vault for consistency with the Admin project. Here it however looks to be correctly applied
            // var configuration = configurationBuilder.Build();
            //
            // configuration.AddAzureKeyVaultConfiguration(configurationBuilder);
            configurationBuilder.AddEnvironmentVariables();
            configurationBuilder.AddCommandLine(args);

            return configurationBuilder.Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                 .ConfigureAppConfiguration((hostContext, configApp) =>
                 {
                     // EZYC-2851-modification: allow more robust configuration
                     var bootstrapperAdminConfig = _bootstrapperConfig.GetSection(nameof(AdminConfiguration)).Get<AdminConfiguration>();
                     configApp.AddJsonFile(_fp, "appsettings._Shared.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"appsettings.{_environment}.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"appsettings.{_environment}.{_subEnvironment}.json", optional: true,
                         reloadOnChange: true);

                     bool.TryParse(Environment.GetEnvironmentVariable("SKIP_AWS_SECRETS_MANAGER"), out var skipAwsSecretsManager);
                     if (!hostContext.HostingEnvironment.IsDevelopment() && !skipAwsSecretsManager)
                     {
                         configApp.AddSecretsManager(configurator: options =>
                         {
                             var prefix = $"{bootstrapperAdminConfig.ApplicationName}/{_environment}/";
                             options.SecretFilter = entry => entry.Name.StartsWith(prefix);
                             options.KeyGenerator = (entry, key) =>
                             {
                                 var transformedKey = key.Substring(prefix.Length).Replace("__", ":");
                                 Console.WriteLine($"Reading secret key {key} transformed as {transformedKey}");

                                 return transformedKey;
                             };
                         });
                     }
                     
                     // EZY-modification (EZYC-4328): GCP Secret Manager support
                     configApp.AddGoogleSecretManagerIfEnabled("appsettings");
                     if (hostContext.HostingEnvironment.IsDevelopment())
                     {
                         configApp.AddUserSecrets<Startup>();
                     }

                     // EZY-modification (EZYC-3029): disabling Azure key vault - as per above comments
                     // configurationRoot.AddAzureKeyVaultConfiguration(configApp);
                     configApp.AddEnvironmentVariables();
                     configApp.AddCommandLine(args);
                 })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options => options.AddServerHeader = false);
                    webBuilder.UseStartup<Startup>();
                })
                .UseSerilog();

        private static PhysicalFileProvider GetConsumerProjectSettingsFileProvider(string currentDir)
        {
            var customSettingsPath = Environment.GetEnvironmentVariable("CUSTOM_SETTINGS_PATH");
            if (customSettingsPath == null)
                return null; // no Custom settings path provided, so no file provider.

            // PhysicalFileProvider requires absolute path. Path.Combine doesn't do it, we need to put it into GetFUllPath.
            var combinedPath = Path.Combine(currentDir, "../../../", customSettingsPath);
            var absolutePath = Path.GetFullPath(combinedPath);
            return new PhysicalFileProvider(absolutePath);
        }
    }
}
