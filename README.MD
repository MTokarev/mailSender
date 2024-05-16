1. [About](#About)
2. [Prerequisites](#Prerequisites)
3. [How It works](#How-It-Works)
4. [How to set extensionAttribute3](#How-to-set-extensionAttribute3)
5. [Download app](#Download-app)

### About

Simple app that sends **Happy Birthday🎉** e-mail to your users.

### Prerequisites

- Domain joined computer.
- SendGrid account (you can register free account on https://sendgrid.com). It's required to send email to users.
- Https (TCP443) access to the sendgrid.com (API call will be used to send emails over).

### How It works

App scans domain and gets a list of ActiveDirectory mail-enabled users. Then it reads `extensionAttribute3` and deserialize it into json object.
If `extensionAttribute3` has **"DOB"** key in the format "M/d/yyyy", it will be parsed into `DateTime` object and compared with current date. 
In case of match, an email will be sent, by using html template located in the path specified in `appsettings.json` **-** `MailTemplateHtml`:

```
{
  "Logger": {
    //Optional (can be removed).
    "LogPath": "logs/log.log"
  },
  "SendGrid": {
    "ApiKey": "Do not store your keys into git repos",
    "SenderEmail": "no-reply@domain",
    "SenderName": "NoReply",
    "MailSubject": "Happy Birthday {{name}}!",
    "MailTemplateHtml": "mailTempl.html",

    //Optional (can be removed).
    "CcRecipient": "addToCc@domain",
    "SimulationModeEnabled": true
  }
}
```

You can use `SimulationModeEnabled` setting to just log events but not send the actual message.
Logging is enabled by default and logs are sent both, to the console and file. You can specify log location (optional). If not specified `logs` folder will be created in the working directory.

Html template and message subject might have a tag `{{name}}` that would be replaced with ActiveDirectory user's `DisplayName`.

### How to set extensionAttribute3

Use ActiveDirectory powershell module and run:

```
Set-ADUser userName -Add @{extensionAttribute3='{"DOB": "2/14/1985"}'}
```

### Download app

You can download compiled version for Windows x64 platform [from here]https://github.com/MTokarev/mailSender/releases/download/v1.0.5/mailSender.zip).
