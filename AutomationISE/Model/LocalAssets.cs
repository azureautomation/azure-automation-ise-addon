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
using System.IO;
using Microsoft.Azure.Management.Automation;
using System.Web.Script.Serialization;

namespace AutomationAzure
{
    public static class LocalAssets
    {
        public static LocalAssetsContainerJson Get(String workspacePath)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            string localAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationAzure.Constants.localAssetsFileName);
            string secureLocalAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationAzure.Constants.secureLocalAssetsFileName);

            UnsecureLocalAssetsContainerJson localAssetsJson = jss.Deserialize<UnsecureLocalAssetsContainerJson>((File.ReadAllText(localAssetsFilePath)));
            SecureLocalAssetsContainerJson secureLocalAssetsJson = jss.Deserialize<SecureLocalAssetsContainerJson>((File.ReadAllText(localAssetsFilePath)));

            LocalAssetsContainerJson localAssetsContainer = new LocalAssetsContainerJson();
            
            // add JSON variables to the container
            localAssetsContainer.Variables = new List<VariableJson>();
            if (localAssetsJson.Variable != null)
            {
                localAssetsJson.Variable.ForEach(variable => variable.Encrypted = false);
                localAssetsContainer.Variables.AddRange(localAssetsJson.Variable);
            }
            if (secureLocalAssetsJson.Variable != null)
            {
                secureLocalAssetsJson.Variable.ForEach(variable => variable.Encrypted = true);
                localAssetsContainer.Variables.AddRange(secureLocalAssetsJson.Variable);
            }

            // add JSON credentials to the container
            localAssetsContainer.PSCredentials = new List<CredentialJson>();
            if (secureLocalAssetsJson.PSCredential != null)
            {
                localAssetsContainer.PSCredentials.AddRange(secureLocalAssetsJson.PSCredential);
            } 

            return localAssetsContainer;
        }

        public class LocalAssetsContainerJson
        {
            public List<VariableJson> Variables;
            public List<CredentialJson> PSCredentials;
            //public List<ConnectionJson> Connection;
            //public List<CertificateJson> Certificate;
        }

        private class UnsecureLocalAssetsContainerJson
        {
            public List<VariableJson> Variable;
            //public List<CertificateJson> Certificate;
        }

        private class SecureLocalAssetsContainerJson
        {
            public List<VariableJson> Variable;
            public List<CredentialJson> PSCredential;
            //public List<ConnectionJson> Connection;
        }

    }
}
