<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

$script:ConfigurationPath = "$PSScriptRoot\Config.json"
$script:LocalAssetsPath = "$PSScriptRoot\LocalAssets.json"
$script:SecureLocalAssetsPath = "$PSScriptRoot\SecureLocalAssets.json"

$script:StartProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn = "# Start AzureAutomationISEAddOn snippet"
$script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn = "# End AzureAutomationISEAddOn snippet"

function _findObjectByName {
    param(
        [object] $ObjectArray,
        [string] $Name
    )
    
    $ObjectArray | Where-Object {
        return $_.Name -eq $Name
    }
}

<#
    .SYNOPSIS
        Removes the Azure Automation ISE add-on from the PowerShell ISE.
#>
function Uninstall-AzureAutomationIseAddOn {
    $ProfileContent = Get-Content $Profile -Raw
    
    $StartProfileSnippetIndex = $ProfileContent.IndexOf($script:StartProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn)
    $EndProfileSnippetIndex = $ProfileContent.IndexOf($script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn)

    if($StartProfileSnippetIndex -gt -1 -and $EndProfileSnippetIndex -gt -1) {
        $NewProfileContent = $ProfileContent.Substring(0, $StartProfileSnippetIndex)
        $NewProfileContent += $ProfileContent.Substring($EndProfileSnippetIndex + $script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn.Length)

        $NewProfileContent | Set-Content $Profile
    }
}

<#
    .SYNOPSIS
        Get a local certificate based on its thumbprint, as part of the Azure Automation Authoring Toolkit.
        Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitLocalCertificate {
    param(
        [Parameter(Mandatory=$True)]
        [string] $Name,
        
        [Parameter(Mandatory=$True)]
        [string] $Thumbprint
    )
    
    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local certificate with thumbprint '$Thumbprint'"
            
    try {
        $Certificate = Get-Item ("Cert:\CurrentUser\My\" + $Thumbprint) -ErrorAction Stop
        Write-Output $Certificate
    }
    catch {
        Write-Error "AzureAutomationAuthoringToolkit: Certificate asset '$Name' referenced certificate with thumbprint 
        '$Thumbprint' but no certificate with that thumbprint exist on the local system."
                
        throw $_
    }
}

<#
    .SYNOPSIS
        Get local assets defined for the Azure Automation Authoring Toolkit. Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitLocalAsset {
    param(
        [Parameter(Mandatory=$True)]
        [ValidateSet('Variable', 'Certificate', 'PSCredential', 'Connection')]
        [string] $Type,

        [Parameter(Mandatory=$True)]
        [string]$Name
    )

    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration

    if($Configuration.LocalAssetsPath -eq "default") {
        Write-Verbose "Grabbing local assets from default location '$script:LocalAssetsPath'"
    }
    else {
        $script:LocalAssetsPath = $Configuration.LocalAssetsPath
        Write-Verbose "Grabbing local assets from user-specified location '$script:LocalAssetsPath'"
    }

    if($Configuration.SecureLocalAssetsPath -eq "default") {
        Write-Verbose "Grabbing secure local assets from default location '$script:SecureLocalAssetsPath'"
    }
    else {
        $script:SecureLocalAssetsPath = $Configuration.SecureLocalAssetsPath
        Write-Verbose "Grabbing secure local assets from user-specified location '$script:SecureLocalAssetsPath'"
    }
    
    $LocalAssetsError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit local assets defined in 
    '$script:LocalAssetsPath' is incorrect. Make sure the file exists, and it contains valid JSON."

    $SecureLocalAssetsError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit secure local assets defined in 
    '$script:SecureLocalAssetsPath' is incorrect. Make sure the file exists, and it contains valid JSON."
    
    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local value for $Type asset '$Name.'"
      
    try {
        $LocalAssets = Get-Content $script:LocalAssetsPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $LocalAssetsError
        throw $_
    }

    try {
        $SecureLocalAssets = Get-Content $script:SecureLocalAssetsPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $SecureLocalAssetsError
        throw $_
    }

    $Asset = _findObjectByName -ObjectArray $LocalAssets.$Type -Name $Name

    if($Asset) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Found local value for $Type asset '$Name.'"
    }
    else {
        $Asset = _findObjectByName -ObjectArray $SecureLocalAssets.$Type -Name $Name

        if($Asset) {
            Write-Verbose "AzureAutomationAuthoringToolkit: Found secure local value for $Type asset '$Name.'"
        }
    }

    if($Asset) {
        if($Type -eq "Certificate") {
            $AssetValue = Get-AzureAutomationAuthoringToolkitLocalCertificate -Name $Name -Thumbprint $Asset.Thumbprint
        }
        elseif($Type -eq "Variable") {
            $AssetValue = $Asset.Value
        }
        elseif($Type -eq "Connection") {
             # Convert PSCustomObject to Hashtable
            $Temp = @{}

            $Asset.psobject.properties | ForEach-Object {
                if($_.Name -ne "Name" -and $_.Name -ne "LastModified") {
                    $Temp."$($_.Name)" = $_.Value
                }
            }

            $AssetValue = $Temp
        }
        elseif($Type -eq "PSCredential") {
            $AssetValue = @{
                Username = $Asset.Username
                Password = $Asset.Password
            }
        }

        Write-Output $AssetValue
    }
    else {
        Write-Verbose "AzureAutomationAuthoringToolkit: Local value for $Type asset '$Name' not found." 
        Write-Warning "AzureAutomationAuthoringToolkit: Warning - Local value for $Type asset '$Name' not found."
    }
}

<#
    .SYNOPSIS
        Get the configuration for the Azure Automation Authoring Toolkit. Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitConfiguration {       
    $ConfigurationError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit configuration defined in 
    '$script:ConfigurationPath' is incorrect. Make sure the file exists, contains valid JSON, and contains 'LocalAssetsPath'
    and 'SecureLocalAssetsPath' settings."

    Write-Verbose "AzureAutomationAuthoringToolkit: Grabbing AzureAutomationAuthoringToolkit configuration."

    try {
        $ConfigurationTemp = Get-Content $script:ConfigurationPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $ConfigurationError
        throw $_
    }

    $Configuration = @{}
    $ConfigurationTemp | ForEach-Object {
        $Key = $_.Name
        $Value = $_.Value

        $Configuration.$Key = $Value
    }

    if(!($Configuration.LocalAssetsPath -and $Configuration.SecureLocalAssetsPath)) {
        throw $ConfigurationError
    }

    Write-Output $Configuration
}

<#
    .SYNOPSIS
        Get a variable asset from Azure Automation.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
function Get-AutomationVariable {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    [OutputType([Object])]
    
    param(
        [Parameter(Mandatory=$true)]
        [string] $Name
    )

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local variable asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitLocalAsset -Type Variable -Name $Name

    Write-Output $AssetValue
}

<#
    .SYNOPSIS
        Get a connection asset from Azure Automation.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
function Get-AutomationConnection {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    [OutputType([Hashtable])]
    
    param(
        [Parameter(Mandatory=$true)]
        [string] $Name
    )

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local connection asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitLocalAsset -Type Connection -Name $Name

    Write-Output $AssetValue
}

<#
    .SYNOPSIS
        Set the value of a variable asset in Azure Automation.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
function Set-AutomationVariable {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    
    param(
        [Parameter(Mandatory=$true)]
        [string] $Name,

        [Parameter(Mandatory=$true)]
        [object] $Value
    )

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local variable asset with name '$Name'"

    $LocalAssetValue = Get-AzureAutomationAuthoringToolkitLocalAsset -Type Variable -Name $Name

    if($LocalAssetValue) {
        $LocalAssets = Get-Content $script:LocalAssetsPath -Raw | ConvertFrom-Json
        $SecureLocalAssets = Get-Content $script:SecureLocalAssetsPath -Raw | ConvertFrom-Json

        $LocalAssets.Variable | ForEach-Object {
            if($_.Name -eq $Name) {
                $_.Value = $Value
                $_.LastModified = Get-Date -Format u
            }
        }

        $SecureLocalAssets.Variable | ForEach-Object {
            if($_.Name -eq $Name) {
                $_.Value = $Value
                $_.LastModified = Get-Date -Format u
            }
        }

        Write-Verbose "AzureAutomationAuthoringToolkit: Setting value of local variable asset with name '$Name'"

        Set-Content $script:LocalAssetsPath -Value (ConvertTo-Json -InputObject $LocalAssets -Depth 999)
        Set-Content $script:SecureLocalAssetsPath -Value (ConvertTo-Json -InputObject $SecureLocalAssets -Depth 999)
    }
    else {
        throw "Variable '$Name' not found for account 'AuthoringToolkit'"
    }
}

<#
    .SYNOPSIS
        Get a certificate asset from Azure Automation.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
function Get-AutomationCertificate {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    
    param(
        [Parameter(Mandatory=$true)]
        [string] $Name
    )

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for local certificate asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitLocalAsset -Type Certificate -Name $Name

    Write-Output $AssetValue
}
