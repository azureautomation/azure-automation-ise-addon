<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

Copy-ConfigFile

if ($PSIse) {
    $null = $PSIse.CurrentPowerShellTab.VerticalAddOnTools.Add('Azure Automation ISE add-on', [AutomationISE.AutomationISEControl], $True)
}

<#
    .SYNOPSIS
        Get a credential asset from Azure Automation.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
workflow Get-AutomationPSCredential {
   [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
   [OutputType([PSCredential])]

    param(
        [Parameter(Mandatory=$True)]
        [string] $Name
    )

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local credential asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitLocalAsset -Type PSCredential -Name $Name

    if($AssetValue) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Converting '$Name' asset value to a proper PSCredential"
        
        $SecurePassword = $AssetValue.Password | ConvertTo-SecureString -AsPlainText -Force
        $Cred = New-Object -TypeName System.Management.Automation.PSCredential -ArgumentList $AssetValue.Username, $SecurePassword

        Write-Output $Cred
    }
}