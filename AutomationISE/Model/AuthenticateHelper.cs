// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Win32;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;
using Microsoft.Azure.Common;
using System.Threading;
using Microsoft.IdentityModel.Clients;
using System.Security;

namespace AutomationISE.Model
{
    static class AuthenticateHelper
    {
        public static async Task<AuthenticationResult> GetAuthorizationHeader(String Username, SecureString Password, String authority = "common")
        {
            var Creds = new Microsoft.IdentityModel.Clients.ActiveDirectory.UserCredential(Username, Password);
            var AuthContext = new AuthenticationContext(Properties.Settings.Default.loginAuthority + authority);
            return await AuthContext.AcquireTokenAsync(Properties.Settings.Default.appIdURI, Constants.clientID, Creds);
        }

        public static AuthenticationResult GetInteractiveLogin(String Username = null, String authority = "common", Boolean prompt=false)
        {
            var ctx = new AuthenticationContext(string.Format(Properties.Settings.Default.loginAuthority + authority, Constants.tenant));
            if (prompt)
                return ctx.AcquireToken(Properties.Settings.Default.appIdURI, Constants.clientID, new Uri(Constants.redirectURI), PromptBehavior.Always);
            else
                return ctx.AcquireToken(Properties.Settings.Default.appIdURI, Constants.clientID, new Uri(Constants.redirectURI), PromptBehavior.Auto);
        }

        public static AuthenticationResult RefreshTokenByAuthority(String authority,String appIdURI)
        {
            var ctx = new AuthenticationContext(string.Format(Properties.Settings.Default.loginAuthority + authority, Constants.tenant));
            // Refresh the token for the logged in user only.
            UserIdentifier userName = new UserIdentifier(Properties.Settings.Default["ADUserName"].ToString(), UserIdentifierType.OptionalDisplayableId);
            try
            {
                return ctx.AcquireToken(appIdURI, Constants.clientID, new Uri(Constants.redirectURI), PromptBehavior.Never, userName);
            }
            catch
            {
                return ctx.AcquireToken(appIdURI, Constants.clientID, new Uri(Constants.redirectURI), PromptBehavior.Auto, userName);
            }
        }
    }
}
