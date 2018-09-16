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
using System.Management.Automation.Runspaces;

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
            String galleryVersion = GetGalleryVersionISEToolkit();

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

            using (Runspace runSpace = RunspaceFactory.CreateRunspace())
            {

                runSpace.Open();
                using (Pipeline pipeline = runSpace.CreatePipeline())
                {
                    Command getModuleCommand = new Command("Import-Module");
                    getModuleCommand.Parameters.Add("Name", "AzureAutomationAuthoringToolkit");
                    getModuleCommand.Parameters.Add("PassThru");
                    Command getVersionCommand = new Command("Select");
                    getVersionCommand.Parameters.Add("Property", "Version");

                    // Add commands to the pipeline
                    pipeline.Commands.Add(getModuleCommand);
                    pipeline.Commands.Add(getVersionCommand);

                    Collection<PSObject> output = pipeline.Invoke();

                    if (pipeline.Error.Count > 0)
                    {
                        StringBuilder errors = new StringBuilder();
                        while (!pipeline.Error.EndOfPipeline)
                        {
                            var errorValue = pipeline.Error.Read() as PSObject;
                            if (errorValue != null)
                            {
                                var errorRecord = errorValue.BaseObject as ErrorRecord;
                                if (errorRecord != null)
                                {
                                    errors.AppendLine(errorRecord.Exception.Message);
                                }
                            }
                        }
                        runSpace.Close();
                        throw new Exception(errors.ToString());
                    }
                    foreach (PSObject result in output)
                    {
                        if (result != null)
                        {
                            foreach (var prop in result.Properties)
                            {
                                version = prop.Value.ToString();
                            }
                        }
                    }
                }
                runSpace.Close();
            }
            return version;
        }


        public static String GetGalleryModuleUri(String moduleName, String Version)
        {
            var address = new Uri("https://www.powershellgallery.com/api/v2/package/" + moduleName + "/" + Version);

            try
            {
                var request = WebRequest.Create(address) as HttpWebRequest;
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    return response.ResponseUri.AbsoluteUri;
                }
            }
            catch (Exception Ex)
            {
                return null;
            }
        }
        public class GalleryInfo
        {
            public GalleryInfo() { }
            public String moduleName;
            public String moduleVersion;
            public String URI;
        }

        /// <summary>
        /// Gets the module depdendencies from the PowerShell Gallery
        /// </summary>
        /// <returns></returns>
        public static List<GalleryInfo> GetGalleryModuleDependencies(String moduleName, String Version)
        {

            List<GalleryInfo> dependencyList = new List<GalleryInfo>();
            Uri address = new Uri("https://www.powershellgallery.com/api/v2/FindPackagesById()?id='" + moduleName + "'");

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
            doc.XmlResolver = null;
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(new StringReader(requestContent), settings))
            {
                doc.Load(reader);
            }
            // Add the namespaces for the gallery xml content
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ps", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
            nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            // Find the version information
            XmlNode root = doc.DocumentElement;
            var props = root.SelectNodes("//m:properties/d:Version", nsmgr);

            // Find the dependencies
            foreach (XmlNode node in props)
            {
                if (String.Compare(node.FirstChild.Value, Version, StringComparison.CurrentCulture) == 0)
                {
                    // Get the dependency list
                    var dependencies = "";
                    foreach (var childitem in node.ParentNode.ChildNodes)
                    {
                        if ((((System.Xml.XmlElement)childitem).Name) == "d:Dependencies")
                            {
                                dependencies = (((System.Xml.XmlElement)childitem).InnerText);
                                break;
                            }
                    }
                    if (!(String.IsNullOrEmpty(dependencies)))
                        {
                        var splitDependencies = dependencies.Split('|');
                        foreach (var dependent in splitDependencies)
                        {
                            var Parts = dependent.Split(':');
                            var DependentmoduleName = Parts[0];
                            var DependencyVersion = Parts[1].Replace("[", "").Replace("]", "");
                            address = new Uri("https://www.powershellgallery.com/api/v2/package/" + DependentmoduleName + "/" + DependencyVersion);
                            request = WebRequest.Create(address) as HttpWebRequest;
                            // Get response  
                            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                            {
                                var dependency = response.ResponseUri.AbsoluteUri;
                                // Set gallery properties and add to dependency list
                                var galleryInfo = new GalleryInfo();
                                galleryInfo.URI = dependency;
                                galleryInfo.moduleVersion = DependencyVersion;
                                galleryInfo.moduleName = DependentmoduleName;
                                dependencyList.Add(galleryInfo);
                            }
                        }
                    }
                }
            }

            return dependencyList;
        }
        /// <summary>
        /// Gets the version of the authoring toolkit from the PowerShell Gallery
        /// </summary>
        /// <returns></returns>
        public static string GetGalleryVersionISEToolkit()
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
            doc.XmlResolver = null;
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(new StringReader(requestContent), settings))
            {
                doc.Load(reader);
            }

            // Add the namespaces for the gallery xml content
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ps", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
            nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

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
