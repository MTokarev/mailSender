using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Text;

namespace mailSender
{
    [DirectoryRdnPrefix("CN")]
    [DirectoryObjectClass("Person")]
    class UserPrincipalExtension : UserPrincipal
    {
        public UserPrincipalExtension(PrincipalContext context) : base(context)
        {
        }

        public UserPrincipalExtension(PrincipalContext context, string samAccountName, string password, bool enabled) : base(context, samAccountName, password, enabled)
        {
        }

        [DirectoryProperty("extensionAttribute3")]
        public string ExtensionAttribute3
        {
            get
            {
                if (ExtensionGet("extensionAttribute3").Length == 0)
                    return string.Empty;

                return ExtensionGet("extensionAttribute3")[0].ToString();
            }
            set { ExtensionSet("extensionAttribute3", value); }
        }
    }
}
