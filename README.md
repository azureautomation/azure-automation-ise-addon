# Azure Automation PowerShell ISE Addon

## Description

TBD


## Installation

* Download this repository by clicking the **"Download Zip"** button on the **right side** of this page 
* Extract the contents in the Zip and copy the "AzureAutomationAuthoringToolkit" module folder to `%USERPROFILE%\Documents\WindowsPowerShell\Modules`. If this folder does not exist, place it in your user PowerShell module path. You can find the path by running $env:psmodulepath from a PowerShell window.
* Open the PowerShell ISE, and run `Install-AzureAutomationIseAddOn`
* The Azure Automation ISE add-on should appear on the right side of the PowerShell ISE
![alt text](https://github.com/azureautomation/azure-automation-ise-addon/blob/master/Automation-Add-On.png " Azure Automation Add-On")
* From now on, opening the PowerShell ISE should automatically load the Azure Automation ISE add-on


## Uninstallation

* Open the PowerShell ISE, and run `Uninstall-AzureAutomationIseAddOn`
* Restart the Powershell ISE
* From now on, opening the PowerShell ISE will no longer cause the Azure Automation ISE add-on to be loaded
