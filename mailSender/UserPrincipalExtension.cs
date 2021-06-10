using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Text;
using System.Text.Json;

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
                var ex3 = ExtensionGet("extensionAttribute3");
                
                // If extensionAttribute3 has a value return otherwise return empty string.
                return ex3.Length == 0 ? string.Empty : ex3[0].ToString();
            }
        }
    }
}
