<#
    Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

$script:ConfigurationPath = "$PSScriptRoot\Config.json"
$script:StaticAssetsPath = "$PSScriptRoot\StaticAssets.json"
$script:SecureStaticAssetsPath = "$PSScriptRoot\SecureStaticAssets.json"

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
        Get static assets defined for the Azure Automation Authoring Toolkit. Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitStaticAsset {
    param(
        [Parameter(Mandatory=$True)]
        [ValidateSet('Variable', 'Certificate', 'PSCredential', 'Connection')]
        [string] $Type,

        [Parameter(Mandatory=$True)]
        [string]$Name
    )

    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration

    if($Configuration.StaticAssetsPath -eq "default") {
        Write-Verbose "Grabbing static assets from default location '$script:StaticAssetsPath'"
    }
    else {
        $script:StaticAssetsPath = $Configuration.StaticAssetsPath
        Write-Verbose "Grabbing static assets from user-specified location '$script:StaticAssetsPath'"
    }

    if($Configuration.SecureStaticAssetsPath -eq "default") {
        Write-Verbose "Grabbing secure static assets from default location '$script:SecureStaticAssetsPath'"
    }
    else {
        $script:SecureStaticAssetsPath = $Configuration.SecureStaticAssetsPath
        Write-Verbose "Grabbing secure static assets from user-specified location '$script:SecureStaticAssetsPath'"
    }
    
    $StaticAssetsError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit static assets defined in 
    '$script:StaticAssetsPath' is incorrect. Make sure the file exists, and it contains valid JSON."

    $SecureStaticAssetsError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit secure static assets defined in 
    '$script:SecureStaticAssetsPath' is incorrect. Make sure the file exists, and it contains valid JSON."
    
    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static value for $Type asset '$Name.'"
      
    try {
        $StaticAssets = Get-Content $script:StaticAssetsPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $StaticAssetsError
        throw $_
    }

    try {
        $SecureStaticAssets = Get-Content $script:SecureStaticAssetsPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $SecureStaticAssetsError
        throw $_
    }

    $Asset = $StaticAssets.$Type | Where-Object -FilterScript {
        $_.Name -eq $Name
    }

    if($Asset) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Found static value for $Type asset '$Name.'"
    }
    else {
        $Asset = $SecureStaticAssets.$Type | Where-Object -FilterScript {
            $_.Name -eq $Name
        }

        if($Asset) {
            Write-Verbose "AzureAutomationAuthoringToolkit: Found secure static value for $Type asset '$Name.'"
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
                if($_.Name -ne "Name") {
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
        Write-Verbose "AzureAutomationAuthoringToolkit: Static value for $Type asset '$Name' not found." 
        Write-Warning "AzureAutomationAuthoringToolkit: Warning - Static value for $Type asset '$Name' not found."
    }
}

<#
    .SYNOPSIS
        Get the configuration for the Azure Automation Authoring Toolkit. Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitConfiguration {       
    $ConfigurationError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit configuration defined in 
    '$script:ConfigurationPath' is incorrect. Make sure the file exists, contains valid JSON, and contains 'StaticAssetsPath'
    and 'SecureStaticAssetsPath' fields."

    Write-Verbose "AzureAutomationAuthoringToolkit: Grabbing AzureAutomationAuthoringToolkit configuration."

    try {
        $Configuration = Get-Content $script:ConfigurationPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $ConfigurationError
        throw $_
    }

    if(!($Configuration.StaticAssetsPath -and $Configuration.SecureStaticAssetsPath)) {
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

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static variable asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitStaticAsset -Type Variable -Name $Name

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

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static connection asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitStaticAsset -Type Connection -Name $Name

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

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static variable asset with name '$Name'"

    $StaticAssetValue = Get-AzureAutomationAuthoringToolkitStaticAsset -Type Variable -Name $Name

    if($StaticAssetValue) {
        Write-Warning "AzureAutomationAuthoringToolkit: Warning - Variable asset '$Name' has a static value defined locally. 
        Since the toolkit's Get-AutomationVariable activity will return that static value, this call of the 
        Set-AutomationVariable activity will not attempt to update the real value in Azure Automation. If you truely wish to
        update the variable asset in Azure Automation, remove the '$Name' variable asset from '$script:StaticAssetsPath'. This 
        way, both AzureAutomationAuthoringToolkit Get-AutomationVariable and Set-AutomationVariable will use / affect the 
        value in Azure Automation."
    }
    else {
        $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration
        $AccountName = $Configuration.AutomationAccountName

        Write-Verbose "AzureAutomationAuthoringToolkit: Static variable asset with name '$Name' not found, looking for real 
        asset in Azure Automation account '$AccountName.'"

        Test-AzureAutomationAuthoringToolkitAzureConnection
    
        $Variable = Get-AzureAutomationVariable -Name $Name -AutomationAccountName $AccountName

        if($Variable) {
            Write-Verbose "AzureAutomationAuthoringToolkit: Variable asset '$Name' found. Updating it."

            Set-AzureAutomationVariable -Name $Name -Value $Value -Encrypted $Variable.Encrypted -AutomationAccountName $AccountName | Out-Null
        }
        else {
            throw "AzureAutomationAuthoringToolkit: Cannot update variable asset '$Name.' It does not exist."
            ## TODO: check if Az Automation throws an exception if var asset not found, as we should match that behavior
            ## test showed there is an exception if not found
        }
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

    Write-Verbose "AzureAutomationAuthoringToolkit: Looking for static certificate asset with name '$Name'"

    $AssetValue = Get-AzureAutomationAuthoringToolkitStaticAsset -Type Certificate -Name $Name

    Write-Output $AssetValue
}