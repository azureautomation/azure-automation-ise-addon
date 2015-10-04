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

namespace AutomationISE.Model
{
    public class Constants
    {
        public const String ISEUserAgent = "ISEAutomationClient";
        public const String ISEVersion = "0.5";

        public const String localAssetsFileName = "LocalAssets.json";
        public const String secureLocalAssetsFileName = "SecureLocalAssets.json";

        public const String loginAuthority = "https://login.microsoftonline.com/";
        public const String appIdURI = "https://management.core.windows.net/";
        public const String clientID = "1950a258-227b-4e31-a9cf-717495945fc2";
        public const String tenant = "f0316def-610c-40f6-abf2-f0ab2296b483";
        public const String redirectURI = "urn:ietf:wg:oauth:2.0:oob";

        // Runbook values
        public const String notExist = "N/A";

        public const String portalURL = "https://ms.portal.azure.com/#resource/subscriptions/";

        public const bool encryptedTrue = true;

        public const String assetCredential = "Credential";
        public const String assetVariable = "Variable";
        public const String assetCertificate = "Certificate";
        public const String assetConnection = "Connection";
        public const String assetModule = "Module";

        public const String feedbackURI = "http://iseautomation.azurewebsites.net/FeedbackForm.aspx";
        public const String helpURI = "http://iseautomation.azurewebsites.net/Help.html";

        public const int ExpirationLengthInDaysForSelfSignedCert = 365 * 2;

        public const String sourceControlRunbook = "Sync-MicrosoftAzureAutomationAccountFromGithubV1";
        public const String sourceControlConnectionVariable = "Microsoft.Azure.Automation.SourceControl.Connection";

        // Minutes to check for token refresh
        public const int tokenRefreshInterval = 10;

        public class RunbookType
        {
            public const String Workflow = "Script";

            public const String Graphical = "Graph";

            public const String PowerShellScript = "PowerShell";
        }

        public class AssetType
        {
            public const String Variable = "Variable";
            public const String Credential = "Credential";
        }

        public class AutomationAccountState
        {
            public const string Ready = "Ready";

            public const string Suspended = "Suspended";
        }

    }
}
