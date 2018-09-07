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
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Management.Automation;
using System.Threading;
using Microsoft.Azure.Graph.RBAC;
using Microsoft.Rest;
using Microsoft.Azure.Graph.RBAC.Models;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.ActiveDirectory.GraphClient;

namespace AutomationISE.Model
{
    class RunAs
    {
        private static GraphRbacManagementClient graphClient;
        private static ActiveDirectoryClient graphClient2;

        private static async Task<string> GetAccessToken(AuthenticationResult token)
        {
            return token.AccessToken;
        }

        public RunAs(AuthenticationResult token)
        {
            graphClient = new GraphRbacManagementClient(new TokenCredentials(token.AccessToken))
            {
                TenantID = token.TenantId
            };
            graphClient2 = new Microsoft.Azure.ActiveDirectory.GraphClient.ActiveDirectoryClient(new Uri(Constants.graphURI + token.TenantId), () => GetAccessToken(token));
        }

        public async Task<X509Certificate2> CreateLocalRunAs(string applicationID, String certName)
        {
            X509Certificate2 cert = null;
            var runAsApplication = await graphClient.Applications.ListAsync("$filter=appId eq '" + applicationID + "'");
 
            foreach (var app in runAsApplication)
            {
                if (app.AppId == applicationID)
                {
                    var existingCredentialKeys = await graphClient.Applications.ListKeyCredentialsAsync(app.ObjectId);
                    if (existingCredentialKeys != null)
                    {
                        var thumbprint = CreateSelfSignedCertificate(certName);
                        cert = AutomationSelfSignedCertificate.GetCertificateWithThumbprint(thumbprint);
                        await UpdateADApplication(cert.NotBefore, cert.NotAfter, cert.RawData, app.ObjectId);
                    }
                }
            }
            return cert;
        }

        private String CreateSelfSignedCertificate(String FriendlyName)
        {
            try
            {
                var certObj = new Certificate();
                certObj.FriendlyName = FriendlyName;
                certObj.ExpirationLengthInDays = 365;

                certObj.CreateCertificateRequest(FriendlyName);
                var selfSignedCert = certObj.InstallCertficate();
                var thumbprint = selfSignedCert.Thumbprint;
                return thumbprint;
            }
            catch
            {
                throw;
            }
        }

        private async Task UpdateADApplication(
            DateTime selfSignedCertStartTime,
            DateTime selfSignedCertEndTime,
            byte[] selfSignedCertString,
            string applicationObjectId)
        {
            try
            {
                var listKeyCredential = new List<Microsoft.Azure.ActiveDirectory.GraphClient.KeyCredential>();
 
                // Query the existing KeyCredentials
                var application = await graphClient2.Applications.GetByObjectId(
                                            applicationObjectId).ExecuteAsync().ConfigureAwait(true);
            

                foreach (var existingKeyCredential in application.KeyCredentials)
                {
                    // using UTC date as the KeyCredentials dates are in UTC
                    var currentDate = DateTime.UtcNow.Date;

                    if (existingKeyCredential.EndDate != null)
                    {
                        if (existingKeyCredential.EndDate >= currentDate)
                        {
                            listKeyCredential.Add(existingKeyCredential);
                        }
                    }
                }

                var keyCredential = new Microsoft.Azure.ActiveDirectory.GraphClient.KeyCredential();
                keyCredential.Type = "AsymmetricX509Cert";
                keyCredential.Usage = "Verify";
                keyCredential.StartDate = selfSignedCertStartTime;
                keyCredential.EndDate = selfSignedCertEndTime;
                keyCredential.Value = selfSignedCertString;

                listKeyCredential.Add(keyCredential);

                // Clear the key credentials and add in the unexpired ones and the new one.
                application.KeyCredentials.Clear();
                listKeyCredential.ForEach((unExpiredKeyCredential) => application.KeyCredentials.Add(unExpiredKeyCredential));

                // Update Application in AD with new cert

                await application.UpdateAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }
    }
}
