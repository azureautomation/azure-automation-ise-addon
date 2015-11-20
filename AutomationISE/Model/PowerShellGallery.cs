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
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Net;
using System.IO;
using System.Xml;

namespace AutomationISE.Model
{
    static class PowerShellGallery
    {
        /// <summary>
        /// Checks the version of the local authoring toolkit against the one on PowerShell Gallery
        /// and returns true if the gallery version is higher or else false if the local one is higher
        /// </summary>
        /// <returns></returns>
        public static bool CheckGalleryVersion()
        {
            String localVersion = GetLocalVersion();
            String galleryVersion = GetGalleryVersion();

            if (String.Compare(galleryVersion, localVersion, StringComparison.CurrentCulture) > 0)
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Get the local version of the authoring toolkit
        /// </summary>
        /// <returns></returns>
        public static string GetLocalVersion()
        {

            String version = null;
            using (PowerShell PowerShellInstance = PowerShell.Create())
            {
                PowerShellInstance.AddScript("Import-Module AzureAutomationAuthoringToolkit; Get-Module AzureAutomationAuthoringToolkit | Select Version");
                Collection<PSObject> PSOutput = PowerShellInstance.Invoke();

                foreach (PSObject outputItem in PSOutput)
                {
                    if (outputItem != null)
                    {
                        foreach (var prop in outputItem.Properties)
                        {
                            version = prop.Value.ToString();
                        }
                    }
                }
            }
            return version;
        }
        
        /// <summary>
        /// Gets the version of the authoring toolkit from the PowerShell Gallery
        /// </summary>
        /// <returns></returns>
        public static string GetGalleryVersion()
        {
            Uri address = new Uri("https://www.powershellgallery.com/api/v2/FindPackagesById()?id='AzureAutomationAuthoringToolkit'");
 
            HttpWebRequest request = WebRequest.Create(address) as HttpWebRequest;
            String requestContent = null;

            // Get response  
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(response.GetResponseStream()); 
                requestContent = reader.ReadToEnd();
            }

            // Load up the XML response
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(requestContent);

            // Add the namespaces for the gallery xml content
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ps", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");            nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            // Find the version information
            XmlNode root = doc.DocumentElement;
            var props = root.SelectNodes("//m:properties/d:Version", nsmgr);

            // Find the latest version
            var version = "0.0";
            foreach (XmlNode node in props)
            {
                if (String.Compare(node.FirstChild.Value, version, StringComparison.CurrentCulture) > 0)
                {
                    version = node.FirstChild.Value;
                }
            }
            return version;
        }
    }
}
