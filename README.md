# Azure Automation PowerShell ISE Addon

## Description

The Azure Automation PowerShell ISE add on makes it easy to author runbook in your local PowerShell ISE.

**Note: This project is currently in early development and is not ready for use yet. Save yourself some frustration -- instead of trying to use it at this time, just click the 'watch' button at the top of the page to stay updated on progress.**

### Features
* Enables use of the Automation activities (Get-AutomationVariable, Get-AutomationPSCredential, etc) in local PowerShell Workflows
* Allows creating and editing the values of Automation assets locally
* Allows easy tracking of local changes to runbooks and assets vs the state of these items in an Azure Automation account
* Allows syncing of runbook / asset changes between a local runbook authoring environment and an Azure Automation account


## Installation

* [Download](https://github.com/azureautomation/azure-automation-ise-addon/archive/master.zip) this repository (zip format)
* Extract the contents of the zip
* Open the PowerShell ISE, and run the `Install-AzureAutomationIseAddOn.ps1` PowerShell script located in the extracted folder
* The Azure Automation ISE add-on should appear on the right side of the PowerShell ISE:
![alt text](https://github.com/azureautomation/azure-automation-ise-addon/blob/master/Screenshots/Automation-Add-On.png " Azure Automation Add-On")
* From now on, opening the PowerShell ISE should automatically load the Azure Automation ISE add-on


## Uninstallation

* Open the PowerShell ISE, and run `Uninstall-AzureAutomationIseAddOn`
* Restart the Powershell ISE
* From now on, opening the PowerShell ISE will no longer cause the Azure Automation ISE add-on to be loaded
