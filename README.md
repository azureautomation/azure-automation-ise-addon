# Azure Automation PowerShell ISE Addon

## Description

TBD


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
