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
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace AutomationAzure
{
    public class StaticAssets
    {
        public StaticAssets(string staticAssetsFile, string secureStaticAssetsFile, string workspace)
        {
            this.staticAssetsFile = staticAssetsFile;
            this.secureStaticAssetsFile = secureStaticAssetsFile;
            this.workspace = workspace;
            DirectoryInfo dir = Directory.CreateDirectory(workspace);
            if (File.Exists(System.IO.Path.Combine(workspace, staticAssetsFile)) == false)
            {
                CreateStaticAssetFile();
            }
            if (File.Exists(System.IO.Path.Combine(workspace, secureStaticAssetsFile)) == false)
            {
                CreateSecureStaticAssetFile();
            }
        }

        public string workspace { get; set; }

        public string staticAssetsFile { get; set; }

        public string secureStaticAssetsFile { get; set; }

        private StaticAssetsJson ParseAssetsFile(string assetsFile)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Deserialize<StaticAssetsJson>((File.ReadAllText(assetsFile)));
        }

        public List<AutomationVariable> GetVariableAssets()
        {
            List<AutomationVariable> automationVariableList = new List<AutomationVariable>();
            var staticAssets = ParseAssetsFile(System.IO.Path.Combine(workspace, staticAssetsFile));

            foreach (var variable in staticAssets.Variable)
            {
                var automationVariable = new AutomationVariable(variable);
                automationVariableList.Add(automationVariable);
            }

            var secureStaticAssets = ParseAssetsFile(System.IO.Path.Combine(workspace, secureStaticAssetsFile));
            foreach (var variable in secureStaticAssets.Variable)
            {
                var automationVariable = new AutomationVariable(variable,Constants.encryptedTrue);
                automationVariableList.Add(automationVariable);
            }

            return automationVariableList;

        }

        public void CreateVariable(VariableJson variable)
        {
            List<AutomationVariable> variableList = GetVariableAssets();
            var variableAsset = variableList.FirstOrDefault(x => x.Name == variable.Name);
            if (variableAsset != null)
            {
                // variable already exists, throw exception

            }
            else
            {

            }
        }

        private void CreateStaticAssetFile()
        {
            StaticAssetsJson staticAssets = new StaticAssetsJson();

            // Add Certificate structure
            staticAssets.Certificate = new List<CertificateJson>();
            CertificateJson certs = new CertificateJson();
            staticAssets.Certificate.Add(certs);

            // Add Variables structure
            staticAssets.Variable = new List<VariableJson>();
            VariableJson variables = new VariableJson();
            staticAssets.Variable.Add(variables);

            JavaScriptSerializer jss = new JavaScriptSerializer();
            var assetsSerialized = jss.Serialize(staticAssets);
            var staticAssetFile = System.IO.Path.Combine(workspace, staticAssetsFile);
            File.WriteAllText(staticAssetFile, assetsSerialized);
        }

        private void CreateSecureStaticAssetFile()
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();

            SecureStaticAssetsJson secureAssets = new SecureStaticAssetsJson();

            secureAssets.PSCredential = new List<PSCredentialJson>();
            secureAssets.Variable = new List<VariableJson>();
            secureAssets.Connection = new List<ConnectionJson>();

            PSCredentialJson creds = new PSCredentialJson();
            secureAssets.PSCredential.Add(creds);

            // Add secure variables structure
            VariableJson secureVariables = new VariableJson();
            secureAssets.Variable.Add(secureVariables);

            // Add connection values
            ConnectionJson connection = new ConnectionJson();
            var connectionDict = new Dictionary<String, String>();
            connection.dict = connectionDict;
            secureAssets.Connection.Add(connection);

            var secureAssetsSerialized = jss.Serialize(secureAssets);
            var secureAssetFile = System.IO.Path.Combine(workspace, secureStaticAssetsFile);
            File.WriteAllText(secureAssetFile, secureAssetsSerialized);
        }

        public class VariableJson
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public DateTime LastModified { get; set; }

        }

        public class CertificateJson
        {
            public string Name { get; set; }
            public string Thumbprint { get; set; }
            public DateTime LastModified { get; set; }

        }

        public class PSCredentialJson
        {
            public string Name { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public DateTime LastModified { get; set; }

        }

        public class ConnectionJson
        {
            public string Name { get; set; }
            public Dictionary<String, String> dict { get; set; }
            public DateTime LastModified { get; set; }
        }

        public class StaticAssetsJson
        {
            public List<VariableJson> Variable;
            public List<CertificateJson> Certificate;
            public List<PSCredentialJson> PSCredential;
            public List<ConnectionJson> Connection;
        }

        public class SecureStaticAssetsJson
        {
            public List<VariableJson> Variable;
            public List<PSCredentialJson> PSCredential;
            public List<ConnectionJson> Connection;
        }
    }
}
