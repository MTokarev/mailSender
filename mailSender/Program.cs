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

            // Send celebration message
            ExecuteMailSender(celebratingUser);

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
                        if(ex3.Dob.DayOfYear == DateTime.Now.DayOfYear)
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
        private static void ExecuteMailSender(List<UserPrincipalExtension> sendTos)
        {
            // Reading configuration from appsettings.json
            var mailFrom = new EmailAddress(_config.GetSection("SendGrid:SenderEmail").Value,
                _config.GetSection("SendGrid:SenderName").Value);
            string mailSubject = _config.GetSection("SendGrid:MailSubject").Value;
            var client = new SendGridClient(_config.GetSection("SendGrid:ApiKey").Value);
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

            foreach(var user in sendTos)
            {
                // Replacing display name.
                mailTemplate = mailTemplate.Replace("{{name}}", user.DisplayName);
                
                // If user has an email address.
                if(!String.IsNullOrEmpty(user.EmailAddress))
                {
                    var mail = new SendGridMessage
                    {
                        From = mailFrom,
                        Subject = mailSubject,
                        PlainTextContent = mailTemplate,
                        HtmlContent = mailTemplate
                    };

                    // Adding user email to the 'To' field.
                    mail.AddTo(user.EmailAddress);

                    var result = client.SendEmailAsync(mail);

                    // Logging results
                    if ( result.Result.StatusCode != HttpStatusCode.Accepted)
                    {
                        _logger.Error($"Unable send email to: '{user.EmailAddress}'. Status code '{result.Result.StatusCode}'.");
                    }
                    else if (result.Result.StatusCode == HttpStatusCode.Accepted)
                    {
                        _logger.Information($"Mail has been sent to: '{user.EmailAddress}'.");
                    }
                }
                else
                {
                    _logger.Warning($"User '{user.UserPrincipalName}' does not have a valid email. Email: '{user.EmailAddress}'.");
                }            
            }
           
            return;
        }
    }
}
