
# Azure Automation PowerShell ISE Add-On

## Description

The Azure Automation PowerShell ISE add-on makes it easy to author and test your runbooks in your local PowerShell ISE.

**Note: This project is currently in development. Please open issues found or provide feedback in the feedback section of the add-on.**

## Features
* Use Automation activities (Get-AutomationVariable, Get-AutomationPSCredential, etc) in local PowerShell Workflows and scripts
* Create and edit Automation assets locally
* Easily track local changes to runbooks and assets vs the state of these items in an Azure Automation account
* Sync runbook / asset changes between a local runbook authoring environment and an Azure Automation account
* Test PowerShell workflows and scripts locally in the ISE and in the automation service


## Installation

### From PowerShell Gallery (recommended)
To install from the [PowerShell Gallery](https://www.powershellgallery.com/packages/AzureAutomationAuthoringToolkit/):
* Open the PowerShell console
* Run `Install-Module AzureAutomationAuthoringToolkit -Scope CurrentUser`

If you want the PowerShell ISE to always automatically load the Azure Automation ISE add-on:
* Run `Install-AzureAutomationIseAddOn`

If not:
* Any time you want to use the Azure Automation ISE add-on in the PowerShell ISE, run `Import-Module AzureAutomationAuthoringToolkit` in the PowerShell ISE

### From GitHub Releases
Follow the instructions for our latest [release](https://github.com/azureautomation/azure-automation-ise-addon/releases)

### From Source
To build from source:
* [Download](https://github.com/azureautomation/azure-automation-ise-addon/archive/master.zip) or clone this repository, and extract from zip if necessary
* Ensure the Azure SDK is installed (required to build in Visual Studio)
* Open AutomationISE/AutomationISE.sln in Visual Studio
* Build the solution. NuGet will pull the required packages.
* Copy the resulting binaries (all files/folders in AutomationISE/bin/Debug) to the existing [AzureAutomationAuthoringToolkit/ISEaddon](https://github.com/azureautomation/azure-automation-ise-addon/tree/master/AzureAutomationAuthoringToolkit/ISEaddon) directory 
* Place the AzureAutomationAuthoringToolkit folder somewhere in your PSModulePath, ex: `C:\Users\<USERNAME>\Documents\WindowsPowerShell\Modules`

If you want to build the standalone AddOn for use with Visual Studio or VSCode, perform the following:
* Open AddOn/AddOn.sln in Visual Studio
* Build the AutomationISE solution cleanly from source, as above.
* Select x64 as the target platform.
* Build the AddOn solution. Nuget will pull any required packages.
* Copy the AddOn.exe that is built into the existing [AzureAutomationAuthoringToolkit/ISEaddon](https://github.com/azureautomation/azure-automation-ise-addon/tree/master/AzureAutomationAuthoringToolkit/ISEaddon) directory where the AutomationISE files were copied previously. 
* From PowerShell, run Import-Module AzureAutomationAuthoringToolkit to get the required configuration files created.
* You can then run AddOn.exe and use outside of the PowerShell ISE with Visual Studio or VS Code.
![alt text](https://github.com/azureautomation/azure-automation-ise-addon/blob/master/Screenshots/VSCodeAddOn.png " Azure Automation Add-On")

If you want the PowerShell ISE to always automatically load the Azure Automation ISE add-on:
* Open the PowerShell ISE, and run `Install-AzureAutomationIseAddOn`
* The Azure Automation ISE add-on should appear on the right side of the PowerShell ISE:
![alt text](https://github.com/azureautomation/azure-automation-ise-addon/blob/master/Screenshots/Automation-Add-On.png " Azure Automation Add-On")
If not:
* Any time you want to use the Azure Automation ISE add-on in the PowerShell ISE, run `Import-Module AzureAutomationAuthoringToolkit` in the PowerShell ISE


## Uninstallation

* Open the PowerShell console
* Run `Uninstall-AzureAutomationIseAddOn`
* Run `Uninstall-Module AzureAutomationAuthoringToolkit`
* If the PowerShell ISE was open, reopen it
* From now on, opening the PowerShell ISE will no longer cause the Azure Automation ISE add-on to be loaded

## Known Issues
* The `AzureAutomationAuthoringToolkit` cmdlets are currently incompatible with SMA's `EmulatedAutomationActivities` module due to name conflicts
* The `AzureAutomationAuthoringToolkit` cmdlets are currently incompatible with SMA runbook workers and Azure Automation Hybrid runbook workers due to name conflicts

