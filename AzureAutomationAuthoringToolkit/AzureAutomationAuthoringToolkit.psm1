<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

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

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static credential asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitStaticAsset -Type PSCredential -Name $Name

    if($AssetValue) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Converting '$Name' asset value to a proper PSCredential"
        
        $SecurePassword = $AssetValue.Password | ConvertTo-SecureString -AsPlainText -Force
        $Cred = New-Object -TypeName System.Management.Automation.PSCredential -ArgumentList $AssetValue.Username, $SecurePassword

        Write-Output $Cred
    }
}