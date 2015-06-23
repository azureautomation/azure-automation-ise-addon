# Azure Automation PowerShell ISE Addon

## Description

TBD


## Installation

* Download this repository by clicking the "Download Zip" button on the right side of this page 
* Copy the "AzureAutomationAuthoringToolkit" module folder in the download to `C:\Users\USERNAME\Documents\WindowsPowerShell\Modules`, replacing `USERNAME` in the path with your local username. If the `USERNAME\Documents` folder does not exist, use the `My Documents` folder instead.
* Open the PowerShell ISE, and run `Install-AzureAutomationIseAddOn`
* The Azure Automation ISE add-on should appear on the right side of the PowerShell ISE
* From now on, opening the PowerShell ISE should automatically load the Azure Automation ISE add-on


## Uninstallation

* Open the PowerShell ISE, and run `Uninstall-AzureAutomationIseAddOn`
* Restart the Powershell ISE
* From now on, opening the PowerShell ISE will no longer cause the Azure Automation ISE add-on to be loaded
