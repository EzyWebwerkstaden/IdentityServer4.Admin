using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EzyNet.Gcp.SecretManager.SerilogSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Filters;
using Skoruba.IdentityServer4.Admin.Configuration;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.DbContexts;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using Skoruba.IdentityServer4.Admin.Helpers;
using Skoruba.IdentityServer4.Shared.Configuration.Common;
using Skoruba.IdentityServer4.Shared.Helpers;

namespace Skoruba.IdentityServer4.Admin
{
    public class Program
    {
        private static IConfiguration _bootstrapperConfig;
        private static string? _subEnvironment;
        private static string _currentDir;
        private static PhysicalFileProvider _fp;
        private static string? _environment;
        private const string SeedArgs = "/seed";

        public static async Task Main(string[] args)
        {
            _currentDir = Directory.GetCurrentDirectory();
            _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _subEnvironment = Environment.GetEnvironmentVariable("SUB_ENVIRONMENT");
            _fp = GetConsumerProjectSettingsFileProvider(_currentDir);
            _bootstrapperConfig = GetBootstrapperConfig(args);
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_bootstrapperConfig)
                .CreateLogger();

            try
            {
                //  EZY-modification (EZYC-3029): we're not using default way of dockerizing.
                //DockerHelpers.ApplyDockerConfiguration(configuration);

                var host = CreateHostBuilder(args).Build();

                await ApplyDbMigrationsWithDataSeedAsync(args, _bootstrapperConfig, host);

                host.Run();
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

        private static async Task ApplyDbMigrationsWithDataSeedAsync(string[] args, IConfiguration configuration, IHost host)
        {
            var applyDbMigrationWithDataSeedFromProgramArguments = args.Any(x => x == SeedArgs);
            if (applyDbMigrationWithDataSeedFromProgramArguments) args = args.Except(new[] {SeedArgs}).ToArray();

            var seedConfiguration = configuration.GetSection(nameof(SeedConfiguration)).Get<SeedConfiguration>();
            var databaseMigrationsConfiguration = configuration.GetSection(nameof(DatabaseMigrationsConfiguration))
                .Get<DatabaseMigrationsConfiguration>();

            await DbMigrationHelpers
                .ApplyDbMigrationsWithDataSeedAsync<IdentityServerConfigurationDbContext, AdminIdentityDbContext,
                    IdentityServerPersistedGrantDbContext, AdminLogDbContext, AdminAuditLogDbContext,
                    IdentityServerDataProtectionDbContext, UserIdentity, UserIdentityRole>(host,
                    applyDbMigrationWithDataSeedFromProgramArguments, seedConfiguration, databaseMigrationsConfiguration);
        }

        private static IConfiguration GetBootstrapperConfig(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(_currentDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/appsettings._Shared.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/appsettings.{_environment}.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/appsettings.{_environment}.{_subEnvironment}.json", optional: true, reloadOnChange: true)
                .AddJsonFile("serilog.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/serilog._Shared.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/serilog.{_environment}.json", optional: true, reloadOnChange: true)
                .AddJsonFile(_fp, $"{"CustomSettings"}/serilog.{_environment}.{_subEnvironment}.json", optional: true, reloadOnChange: true);

            var isDevelopment = _environment == Environments.Development;
            if (isDevelopment)
            {
                configurationBuilder.AddUserSecrets<Startup>();
            }

            // EZY-modification (EZYC-3029): disable this, as new code tries to build configuration before adding command line and env vars
            // I'm not sure whether it is a bug, so better comment this out.
            // var configuration = configurationBuilder.Build();
            //
            // configuration.AddAzureKeyVaultConfiguration(configurationBuilder);
            configurationBuilder.AddCommandLine(args);
            configurationBuilder.AddEnvironmentVariables();

            return configurationBuilder.Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                 .ConfigureAppConfiguration((hostContext, configApp) =>
                 {
                     //  EZY-modification (EZYC-3029): allow more robust configuration
                     var bootstrapperAdminConfig = _bootstrapperConfig.GetSection(nameof(AdminConfiguration)).Get<AdminConfiguration>();
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/appsettings._Shared.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/appsettings.{_environment}.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/appsettings.{_environment}.{_subEnvironment}.json", optional: true, reloadOnChange: true);

                     configApp.AddJsonFile("serilog.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/serilog._Shared.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/serilog.{_environment}.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/serilog.{_environment}.{_subEnvironment}.json", optional: true, reloadOnChange: true);

                     configApp.AddJsonFile("identitydata.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/identitydata.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/identitydata.{_environment}.json", optional: true, reloadOnChange: true);

                     configApp.AddJsonFile("identityserverdata.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/identityserverdata.json", optional: true, reloadOnChange: true);
                     configApp.AddJsonFile(_fp, $"{"CustomSettings"}/identityserverdata.{_environment}.json", optional: true, reloadOnChange: true);

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
                .UseSerilog((hostContext, loggerConfig) =>
                {
                    loggerConfig
                        .ReadFrom.Configuration(hostContext.Configuration)
                        .Enrich.WithProperty("ApplicationName", hostContext.HostingEnvironment.ApplicationName);

                    //  EZY-modification (EZYC-3029) below
                    loggerConfig.WriteTo.Logger(lc =>
                    {
                        // Serilog doesn't allow to override certain logger levels on per-logger basis. Also, it would be good to have at least some
                        // decent console logging on non-dev environments, like few initial lifetime (startup) events. That's why, let's include console
                        // logger for every environment and only in Development mode, let's not restrict it to Microsoft.Hosting.Lifetime
                        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                        if (environment != "Development")
                            lc.Filter.ByIncludingOnly(Matching.FromSource("Microsoft.Hosting.Lifetime"));

                        lc.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}");
                    });
                });

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
