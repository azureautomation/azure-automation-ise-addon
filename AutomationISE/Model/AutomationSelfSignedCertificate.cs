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

using CERTENROLLLib;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace AutomationISE.Model
{
    class AutomationSelfSignedCertificate
    {
        private Certificate certObj = null;

        public AutomationSelfSignedCertificate()
        {
            try
            {
                certObj = new Model.Certificate();
                certObj.FriendlyName = Properties.Settings.Default.certFriendlyName;
                certObj.ExpirationLengthInDays = Constants.ExpirationLengthInDaysForSelfSignedCert;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public X509Certificate2 CreateSelfSignedCertificate()
        {
            try
            {
                // TODO Need to read and set these values in the config file
                // Should not create a new certificate if one already exists
                certObj.CreateCertificateRequest(Properties.Settings.Default.certName);
                return certObj.InstallCertficate();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
