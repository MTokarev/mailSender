using System;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Serilog.Core;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Mail;

namespace mailSender
{
	public class MailService
	{
        private readonly IConfiguration _config;
        private readonly Logger _logger;
        private readonly EmailAddress _mailFrom;
        private readonly bool _isSimulationModeEnabled;
        private readonly string _mailSubject,
            _mailTemplateFile,
            _ccRecipient;

        public MailService(IConfiguration config, Logger logger)
		{
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config)); ;
            
            ValidateAndAssignConfig(config);

            _mailFrom = new EmailAddress(_config.GetSection("SendGrid:SenderEmail").Value,
                _config.GetSection("SendGrid:SenderName").Value);
            _isSimulationModeEnabled = bool.Parse(
                _config.GetSection("SendGrid:SimulationModeEnabled").Value);
            _mailSubject = _config.GetSection("SendGrid:MailSubject").Value;
            _ccRecipient = _config.GetSection("SendGrid:CcRecipient").Value;
            _mailTemplateFile = _config.GetSection("SendGrid:MailTemplateHtml").Value;

        }

        public async Task SendBrthEmailsAsync(IEnumerable<UserPrincipalExtension> sendTos)
        {
            string mailTemplate;
            try
            {
                // Read file from mail template.
                mailTemplate = File.ReadAllText(_mailTemplateFile);
            }
            catch (Exception e)
            {
                _logger.Error($"Unable to read mail template file: '{_config.GetSection("SendGrid:MailTemplateHtml").Value}'. Exception: '{e.Message}'.");

                return;
            }

            if (_isSimulationModeEnabled)
            {
                _logger.Information("--- Simulation mode turned ON. No email will be sent to the users. You can turn it off in appsettings.json");
            }

            var client = new SendGridClient(_config.GetSection("SendGrid:ApiKey").Value);

            foreach (var user in sendTos)
            {
                // Replacing message subject and body with user display name.
                SendGridMessage mail = CreateMail(
                    _mailFrom,
                    _mailSubject,
                    mailTemplate,
                    user);

                // Adding user email to the 'To' field.
                mail.AddTo(user.EmailAddress);

                // If we have recipient to add to CC.
                if (!string.IsNullOrEmpty(_ccRecipient))
                {
                    mail.AddCc(_ccRecipient);
                }

                // If simulation mode is enabled just log message and continue.
                if (_isSimulationModeEnabled)
                {
                    _logger.Information($"E-mail won't be sent to '{user.EmailAddress}'.");
                    continue;
                }

                // Adding task to the list.
                // Creating client and sending messages.
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

        private SendGridMessage CreateMail(EmailAddress mailFrom,
            string mailSubject,
            string mailTemplate,
            UserPrincipalExtension user)
        {
            // Some users could have disaplay name empty or null
            // SamAccount will always exist
            string name;
            if (string.IsNullOrEmpty(user.DisplayName))
            {
                _logger.Warning("Skipping user with samAccountName {SamAccountName} because {Attribute name} is null or empty.",
                    user.SamAccountName,
                    nameof(user.DisplayName));
                name = user.SamAccountName;
            }
            else
            {
                name = user.DisplayName;
            }

            var compiledMailTemplate = mailTemplate.Replace("{{name}}", name);
            var compiledMailSubject = mailSubject.Replace("{{name}}", name);

            var mail = new SendGridMessage
            {
                From = mailFrom,
                Subject = compiledMailSubject,
                PlainTextContent = compiledMailTemplate,
                HtmlContent = compiledMailTemplate
            };

            return mail;
        }

        private void ValidateAndAssignConfig(IConfiguration config)
        {
            bool isValid = true;
            string sender = config.GetSection("SendGrid:SenderEmail").Value;
            if (string.IsNullOrEmpty(config.GetSection("SendGrid:SenderName").Value)
                && string.IsNullOrEmpty(sender))
            {
                _logger.Fatal("Please provide 'SenderEmail' and 'SenderName' in appSettings.json");
                isValid = false;
            }

            if (!string.IsNullOrEmpty(sender) && !MailAddress.TryCreate(sender, out var _))
            {
                _logger.Fatal("'SenderEmail' must be a valid email address. Value provided: '{Sender}'.",
                    sender);
                isValid = false;
            }

            if (!bool.TryParse(config.GetSection("SendGrid:SimulationModeEnabled").Value, out bool _))
            {
                _logger.Fatal("'SimulationModeEnabled' must to be set in appsetting.json as 'true' or 'false'.");
                isValid = false;
            }

            string ccRecipient = config.GetSection("SendGrid:CcRecipient").Value;
            if (!string.IsNullOrEmpty(ccRecipient)
                && !MailAddress.TryCreate(ccRecipient, out var _))
            {
                _logger.Fatal("'CcResipient' must be empty or have a valid email address. Value provided: '{CcRecipient}'.",
                    ccRecipient);
                isValid = false;
            }

            string templateFileName = config.GetSection("SendGrid:MailTemplateHtml").Value;
            if (string.IsNullOrEmpty(templateFileName))
            {
                _logger.Fatal("Please provide a 'MailTemplateHtml' parameter in appsetting.json");
                isValid = false;
            }
            else if (!File.Exists(templateFileName))
            {
               _logger.Fatal("Template file {FileName} does not exist.", templateFileName);
               isValid = false;
            }

            if (!isValid)
            {
                throw new ArgumentException($"One or more parameter provided for '{nameof(MailService)}' are incorrect. " +
                    "Please check the log for more details.");
            }
        }
    }
}
