﻿using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Text.Json;
using Serilog.Core;

namespace mailSender
{
	public class UserService
	{
        private readonly Logger _logger;

        public UserService(Logger logger)
	    {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<UserPrincipalExtension> GetMailEnabledActiveUsers(string domainName)
        {
            _logger.Information("Fetching users from the domain: '{DomainName}'.", domainName);
            int totalAdUsers = 0;

            using (var ctx = new PrincipalContext(ContextType.Domain, domainName))
            {
                var userPrinciple = new UserPrincipalExtension(ctx);
                
                // Fetch users with email attribute
                userPrinciple.EmailAddress = "*";

                using var search = new PrincipalSearcher(userPrinciple);

                // Filter only active users
                userPrinciple.Enabled = true;
                search.QueryFilter = userPrinciple;

                foreach (var domainUser in search.FindAll())
                {
                    totalAdUsers++;
                    yield return (UserPrincipalExtension)domainUser;
                }
            }

            _logger.Information("User scan has beenfinished. Total users found: {TotalAdUsers}",
                totalAdUsers);
            yield break;
        }

        public IEnumerable<UserPrincipalExtension> GetCelebratingUsers(IEnumerable<UserPrincipalExtension> allUsers)
        {
            _logger.Information("Filtering users whose birthday is equal today");
            int usersFound = 0;

            foreach (var user in allUsers)
            {
                if (!string.IsNullOrEmpty(user.ExtensionAttribute3))
                {
                    // Trying to deserialize extensionAttribute3 and parse Date of birth (see Ex3.cs).
                    var ex3 = JsonSerializer.Deserialize<Ex3>(user.ExtensionAttribute3);

                    // If DateTime was parsed from string succesfully.
                    if (ex3.Dob != DateTime.MinValue)
                    {
                        // Add user to the list if he has a birthday today.
                        // Comparing day and month would respect leap years.
                        if (ex3.Dob.Month == DateTime.Now.Month && ex3.Dob.Day == DateTime.Now.Day)
                        {
                            usersFound++;
                            yield return user;
                        }
                    }
                }
            }

            _logger.Information("Users celebrating birthday today: {UsersCount}.", usersFound);
            yield break;
        }
    }
}

