using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace mailSender
{
    class Program
    {
        private static IConfigurationRoot _config;
        private static Logger _logger;

        static async Task Main(string[] args)
        {
            // Create stopwatch to monitor execution time
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // Load config and init logs.
            _config = LoadConfiguration("appsettings.json");
            _logger = InitLogger(_config);
            _logger.Information("Starting '{AppName}'.", nameof(mailSender));

            var mailService = new MailService(_config, _logger);
            var userService = new UserService(_logger);

            // Find all active users who celebrate birthday today
            var enabledUsers = userService.GetEnabledDomainUsers(domainName: Environment.UserDomainName);
            var celebratingUser = userService.GetCelebratingUsers(enabledUsers);

            // Send a postcard
            await mailService.SendBrthEmailsAsync(celebratingUser);

            // Stop timer and log execution time
            stopWatch.Stop();
            _logger.Information("Exectution '{AppName}' has been finished. Running time: '{TotalSeconds}s'.",
                nameof(mailSender),
                stopWatch.Elapsed.TotalSeconds);
        }

        private static IConfigurationRoot LoadConfiguration(string configJsonFile)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configJsonFile, optional: false, reloadOnChange: true);

            var config = builder.Build();
            return config;
        }

        private static Logger InitLogger(IConfigurationRoot config)
        {
            // If logger info is not defined then use working dir
            string logPath = config.GetSection("Logger:LogPath").Value == null ? 
                Path.Combine(Directory.GetCurrentDirectory(), "logs/log.log") : 
                config.GetSection("Logger:LogPath").Value;
            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            return _logger;
        }
    }
}
