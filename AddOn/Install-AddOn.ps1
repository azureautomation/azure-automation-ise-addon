<#
.SYNOPSIS 
    This script downloads the latest Azure Automation Authoring Toolkit from the PowerShell gallery and copies this 
    wrapper AddOn.exe into the directory so that it can be used outside of the PowerShell ISE with VSCode and Visual Studio.

.DESCRIPTION
    This script downloads the latest Azure Automation Authoring Toolkit from the PowerShell gallery and copies this 
    wrapper AddOn.exe into the directory so that it can be used outside of the PowerShell ISE with VSCode and Visual Studio.

    You should have the AddOn.exe and this script in the same folder before you run.
#>



# Get the latest Add-On from the PowerShell Gallery
Install-Module AzureAutomationAuthoringToolkit -Scope CurrentUser -verbose -force

# Location of the AddOn.exe. It should be in the same folder as this installation script.
$AddOnEXE = "$PSScriptRoot\AddOn.exe"

# Import the module to set up the required configuration files
Import-Module AzureAutomationAuthoringToolkit

# Get the path to the module so we can copy the AddOn wrapper to it
$AddOnPath = Split-Path -Parent (Get-Module AzureAutomationAuthoringToolkit | Select Path).Path

if (Test-Path $AddOnPath)
{
    # Copy AddOn executable to the module folder and start it
    Copy-Item $AddOnEXE (Join-Path $AddOnPath "ISEaddon") -Force
    &(Join-Path $AddOnPath "ISEaddon\AddOn.exe")

    Write-Output ("AddOn is installed in " + (Join-Path $AddOnPath "ISEaddon\AddOn.exe") + ". You can pin it to the Taskbar for easier access or create a shortcut")
}