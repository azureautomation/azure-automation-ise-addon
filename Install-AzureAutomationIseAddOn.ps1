<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

$script:IseAddonPath = "\ISEaddon\AutomationISE.dll"

$script:PowerShellToLoadAzureAutomationIseAddOnGeneric = @"
`n
# Start AzureAutomationISEAddOn snippet
`$env:PSModulePath = `$env:PSModulePath + ";{0}"
if(`$PSIse) {{
        Add-Type -Path {1} | Out-Null
        `$PSIse.CurrentPowerShellTab.VerticalAddOnTools.Add('Azure Automation ISE add-on', [AutomationISE.AutomationISEControl], `$True) | Out-Null
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
    
    # create the path for the ISE add on to live in, for any part of the path that does not exist already
    $AzureAutomationAuthoringToolkitSubpath.Split("\") | ForEach-Object {
        $AzureAutomationAuthoringToolkitPath = Join-Path $AzureAutomationAuthoringToolkitPath $_

        New-Item $AzureAutomationAuthoringToolkitPath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
    }

    # copy the ISE add-on's contents into this path
    try {
        Copy-Item $PSScriptRoot\AzureAutomationAuthoringToolkit\* $AzureAutomationAuthoringToolkitPath -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Error "The Azure Automation ISE add-on is already installed. If you need to reinstall it, call Uninstall-AzureAutomationIseAddOn first."
        throw $_
    }

    # add loading of the ISE add-on into the PS ISE Profile so it is automatically loaded each time the ISE is opened
    $IseAddOnModulePath = Split-Path $AzureAutomationAuthoringToolkitPath -Parent
    $IseAddOnDllPath = Join-Path $AzureAutomationAuthoringToolkitPath $script:IseAddonPath
    
    $PowerShellToLoadAzureAutomationIseAddOnWithPath = $script:PowerShellToLoadAzureAutomationIseAddOnGeneric -f $IseAddOnModulePath, $IseAddOnDllPath
    
    Add-Content $Profile $PowerShellToLoadAzureAutomationISEAddOnWithPath

    # make sure the ISE add-on files are allowed to be used    
    Unblock-AzureAutomationAuthoringToolkit -AzureAutomationAuthoringToolkitPath $AzureAutomationAuthoringToolkitPath

    # load the ISE add-on into the PS ISE session already open
    Invoke-Expression $PowerShellToLoadAzureAutomationIseAddOnWithPath
}

Install-AzureAutomationIseAddOn