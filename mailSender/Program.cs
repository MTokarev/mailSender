using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace mailSender
{
    class Program
    {
        private static IConfigurationRoot _config;
        private static Logger _logger;
        static void Main(string[] args)
        {
            // Lets create stopwatch and monitor execution time
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // Load config and init logs.
            _config = LoadConfiguration("appsettings.json");
            _logger = InitLogger(_config);

            _logger.Information($"Starting '{nameof(mailSender)}'.");

            // Getting all user enabled objects and filtering them to the list
            // who celebrate birthday today.
            var enabledUsers = GetEnabledDomainUsers(domainName: Environment.UserDomainName);
            var celebratingUser = GetCelebratingUsers(enabledUsers);

            // Send celebration message and waiting when task is done.
            var task = GenerateEmailsAsync(celebratingUser);
            task.Wait();

            // Stop timer and log time
            stopWatch.Stop();
            _logger.Information($"Exectution '{nameof(mailSender)}' has been finished. Running time: '{stopWatch.Elapsed.TotalSeconds}s'.");
        }
        private static List<UserPrincipalExtension> GetEnabledDomainUsers(string domainName)
        {
            _logger.Information($"Run user scanning in domain: '{domainName}'.");

            var myDomainUsers = new List<UserPrincipalExtension>();
            using (var ctx = new PrincipalContext(ContextType.Domain, domainName))
            {
                var userPrinciple = new UserPrincipalExtension(ctx);
                using var search = new PrincipalSearcher(userPrinciple);

                // Filter only active users
                userPrinciple.Enabled = true;
                search.QueryFilter = userPrinciple;

                foreach (var domainUser in search.FindAll())
                {
                    if (domainUser.DisplayName != null)
                    {
                        myDomainUsers.Add((UserPrincipalExtension)domainUser);
                    }
                }
            }

            _logger.Information($"User scanning finished. Total enabled users found: '{myDomainUsers.Count}'.");

            return myDomainUsers;
        }
        private static List<UserPrincipalExtension> GetCelebratingUsers(List<UserPrincipalExtension> allUsers)
        {
            _logger.Information("Filtering users whose birthday is equal today");

            var celebratingUsers = new List<UserPrincipalExtension>();

            foreach(var user in allUsers)
            {
                if(!String.IsNullOrEmpty(user.ExtensionAttribute3))
                {
                    // Trying to deserialize extensionAttribute3 and parse Date of birth (see Ex3.cs).
                    var ex3 = JsonSerializer.Deserialize<Ex3>(user.ExtensionAttribute3);
                    
                    // If DateTime was parsed from string succesfully.
                    if(ex3.Dob != DateTime.MinValue)
                    {
                        // Add user to the list if he has a birthday today.
                        // Comparing day and month would respect leap years.
                        if(ex3.Dob.Month == DateTime.Now.Month && ex3.Dob.Day == DateTime.Now.Day)
                        {
                            celebratingUsers.Add(user);
                        }
                    }
                }            
            }

            _logger.Information($"Users count who has a birthday today: '{celebratingUsers.Count}'.");

            return celebratingUsers;
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
        private static async Task GenerateEmailsAsync(List<UserPrincipalExtension> sendTos)
        {
            // List of task per user in list.
            List<Task> listOfTasks = new List<Task>();

            // Reading configuration from appsettings.json
            var mailFrom = new EmailAddress(_config.GetSection("SendGrid:SenderEmail").Value,
                _config.GetSection("SendGrid:SenderName").Value);
            string mailSubject = _config.GetSection("SendGrid:MailSubject").Value;
            string ccRecipient = _config.GetSection("SendGrid:CcRecipient").Value;
            bool SimulationModeEnabled = bool.Parse(_config.GetSection("SendGrid:SimulationModeEnabled").Value);
            string mailTemplate;

            try
            {
                // Read file from mail template.
                mailTemplate = File.ReadAllText(_config.GetSection("SendGrid:MailTemplateHtml").Value);
            }
            catch (Exception e)
            {
                _logger.Error($"Unable to read mail template file: '{_config.GetSection("SendGrid:MailTemplateHtml").Value}'. Exception: '{e.Message}'.");
                
                return;
            }

            if(SimulationModeEnabled)
            {
                _logger.Information("--- Simulation mode turned ON. No email will be sent to the users. You can turn it off in appsettings.json");
            }

            foreach(var user in sendTos)
            {
                // Replacing message subject and body with user display name.
                var compiledMailTemplate = mailTemplate.Replace("{{name}}", user.DisplayName);
                var compiledMailSubject = mailSubject.Replace("{{name}}", user.DisplayName);

                // If user has an email address.
                if (!String.IsNullOrEmpty(user.EmailAddress))
                {
                    var mail = new SendGridMessage
                    {
                        From = mailFrom,
                        Subject = compiledMailSubject,
                        PlainTextContent = compiledMailTemplate,
                        HtmlContent = compiledMailTemplate
                    };

                    // Adding user email to the 'To' field.
                    mail.AddTo(user.EmailAddress);

                    // If we have recipient to add to CC.
                    if (!string.IsNullOrEmpty(ccRecipient))
                    {
                        mail.AddCc(ccRecipient);
                    }

                    // If simulation mode is enabled just log message and continue.
                    if(SimulationModeEnabled)
                    {
                        _logger.Information($"E-mail won't sent to '{user.EmailAddress}'.");
                        continue;
                    }

                    // Adding task to the list.
                    listOfTasks.Add(SendBrthAsync(mail, user));
                }
                else
                {
                    _logger.Warning($"User '{user.UserPrincipalName}' does not have a valid email. Email: '{user.EmailAddress}'.");
                }            
            }
            // Wainting when all tasks are done.
            await Task.WhenAll(listOfTasks);
        }
        private static async Task SendBrthAsync(SendGridMessage mail, UserPrincipalExtension user)
        {
            // Creating client and sending messages.
            var client = new SendGridClient(_config.GetSection("SendGrid:ApiKey").Value);
            var result = await client.SendEmailAsync(mail);

            // Logging results
            if (result.StatusCode != HttpStatusCode.Accepted)
            {
                _logger.Error($"Unable send email to: '{user.EmailAddress}'. Status code '{result.StatusCode}'.");
            }
            else if (result.StatusCode == HttpStatusCode.Accepted)
            {
                _logger.Information($"Mail has been sent to: '{user.EmailAddress}'.");
            }
        }
    }
}
