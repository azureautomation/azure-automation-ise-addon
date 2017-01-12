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

namespace AutomationISE.Model
{
    class RunAs
    {
        private static GraphRbacManagementClient graphClient;

        public RunAs(AuthenticationResult token)
        {
            graphClient = new GraphRbacManagementClient(new TokenCredentials(token.AccessToken))
            {
                TenantID = token.TenantId
            };
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
                        await UpdateADApplication(cert.NotBefore, cert.NotAfter, Convert.ToBase64String(cert.RawData), app.ObjectId);
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
            string selfSignedCertString,
            string applicationObjectId)
        {
            var listKeyCredential = new List<KeyCredential>();

            // Query the existing KeyCredentials
            var existingKeyCredentials = await graphClient.Applications.ListKeyCredentialsAsync(applicationObjectId);

            foreach (var existingKeyCredential in existingKeyCredentials)
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

            var keyCredential = new KeyCredential();
            keyCredential.Type = "AsymmetricX509Cert";
            keyCredential.Usage = "Verify";
            keyCredential.StartDate = selfSignedCertStartTime;
            keyCredential.EndDate = selfSignedCertEndTime;
            keyCredential.Value = selfSignedCertString;

            // Add the new KeyCredential to the list 
            listKeyCredential.Add(keyCredential);

            var keyCredentialsUpdateParameters = new KeyCredentialsUpdateParameters(listKeyCredential);

            // Update Application in AD with new cert
            try
            {
                await graphClient.Applications.UpdateKeyCredentialsAsync(
                    applicationObjectId,
                    keyCredentialsUpdateParameters);
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }
    }
}
