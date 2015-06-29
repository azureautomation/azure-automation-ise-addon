<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

$script:IseAddonPath = "\ISEaddon\AzureAutomation.dll"

$script:PowerShellToLoadAzureAutomationISEAddOnGeneric = @"
`n
# Start AzureAutomationISEAddOn snippet
if(`$PSIse) {{
        Add-Type -Path {0} | Out-Null
        `$PSIse.CurrentPowerShellTab.VerticalAddOnTools.Add('Azure Automation ISE add-on', [AzureAutomation.AzureAutomationControl], `$True) | Out-Null
}}
# End AzureAutomationISEAddOn snippet
"@

<#
    .SYNOPSIS
        Unblocks the files used by the AzureAutomationAuthoringToolkit module so they can be used in PowerShell Workflow
#>
function Unblock-AzureAutomationAuthoringToolkit {
    param(
        [string] $AzureAutomationAuthoringToolkitPath
    )

    Unblock-File $AzureAutomationAuthoringToolkitPath\AzureAutomationAuthoringToolkit.psd1
    Unblock-File $AzureAutomationAuthoringToolkitPath\AzureAutomationAuthoringToolkit.psm1
    Unblock-File $AzureAutomationAuthoringToolkitPath\AzureAutomationAuthoringToolkitInner.psm1
    Unblock-File (Join-Path $AzureAutomationAuthoringToolkitPath $script:IseAddonPath)
}

<#
    .SYNOPSIS
        Sets up the Azure Automation ISE add-on for use in the PowerShell ISE.
#>
function Install-AzureAutomationIseAddOn {
    $AzureAutomationAuthoringToolkitSubpath = "Documents\WindowsPowerShell\Modules\AzureAutomationAuthoringToolkit"
    $AzureAutomationAuthoringToolkitPath = $env:userprofile
    
    $AzureAutomationAuthoringToolkitSubpath.Split("\") | ForEach-Object {
        $AzureAutomationAuthoringToolkitPath = Join-Path $AzureAutomationAuthoringToolkitPath $_

        New-Item $AzureAutomationAuthoringToolkitPath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
    }

    try {
        Copy-Item $PSScriptRoot\AzureAutomationAuthoringToolkit\* $AzureAutomationAuthoringToolkitPath -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Error "The Azure Automation ISE add-on is already installed. If you need to reinstall it, call Uninstall-AzureAutomationIseAddOn first."
        throw $_
    }

    $PowerShellToLoadAzureAutomationISEAddOnWithPath = $script:PowerShellToLoadAzureAutomationISEAddOnGeneric -f (Join-Path $AzureAutomationAuthoringToolkitPath $script:IseAddonPath)
    
    Add-Content $Profile $PowerShellToLoadAzureAutomationISEAddOnWithPath
    
    Unblock-AzureAutomationAuthoringToolkit -AzureAutomationAuthoringToolkitPath $AzureAutomationAuthoringToolkitPath

    Invoke-Expression $PowerShellToLoadAzureAutomationISEAddOnWithPath
}

Install-AzureAutomationIseAddOn