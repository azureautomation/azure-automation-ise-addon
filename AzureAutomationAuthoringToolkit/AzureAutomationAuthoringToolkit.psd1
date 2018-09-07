#
# Module manifest for module 'AzureAutomationAuthoringToolkit'

@{

# Script module or binary module file associated with this manifest.
RootModule = 'AzureAutomationAuthoringToolkit.psm1'

# Version number of this module.
ModuleVersion = '0.2.4.2'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '3.0'

# ID used to uniquely identify this module
GUID = '2fcdb2a1-b6b8-4e7a-b48c-adcd98aba614'

# Author of this module
Author = 'AzureAutomationTeam'

# Company or vendor of this module
CompanyName = 'Microsoft'

# Copyright statement for this module
Copyright = '(c) 2015 Microsoft. All rights reserved.'

# Description of the functionality provided by this module
Description = 'Provides cmdlets to make authoring Azure Automation runbooks and DSC configurations in local PowerShell easier.'

# Modules to import as nested modules of the module specified in RootModule/ModuleToProcess
NestedModules = @('.\AzureAutomationAuthoringToolkitInner.psm1')

# Assemblies that must be loaded prior to importing this module
RequiredAssemblies = @(".\ISEAddon\AutomationISE.dll")

# HelpInfo URI of this module
#HelpInfoURI = 'http://aka.ms/azureautomationauthoringtoolkit'

# Functions to export from this module
FunctionsToExport = @("Get-AutomationVariable", "Get-AutomationCertificate", "Get-AutomationPSCredential", "Get-AutomationConnection", "Set-AutomationVariable", "Get-AzureAutomationAuthoringToolkitLocalAsset", "Get-AzureAutomationAuthoringToolkitConfiguration", "Install-AzureAutomationIseAddOn", "Uninstall-AzureAutomationIseAddOn")

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = @('AzureAutomation', 'Azure')

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/azureautomation/azure-automation-ise-addon/blob/master/license.txt'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/azureautomation/azure-automation-ise-addon'

        # A URL to an icon representing this module.
        # IconUri = 'http://joestorage123.blob.core.windows.net/public/AzureAutomationIconSquareNoBackground.png'

        # ReleaseNotes of this module
        ReleaseNotes = 'For installation instructions please see https://github.com/azureautomation/azure-automation-ise-addon#from-powershell-gallery'

    } # End of PSData hashtable

} # End of PrivateData hashtable


}

