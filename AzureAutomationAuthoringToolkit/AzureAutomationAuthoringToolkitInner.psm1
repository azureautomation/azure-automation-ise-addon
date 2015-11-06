<#
        Learn more here: http://aka.ms/azureautomationauthoringtoolkit
#>

$script:ConfigurationFileName = "AzureAutomationAuthoringToolkitConfig.json"
$script:ConfigurationPath = "$env:USERPROFILE\$script:ConfigurationFileName"
$script:LocalAssetsPath = "$PSScriptRoot\LocalAssets.json"
$script:SecureLocalAssetsPath = "$PSScriptRoot\SecureLocalAssets.json"

$script:StartProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn = "# Start AzureAutomationISEAddOn snippet"
$script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn = "# End AzureAutomationISEAddOn snippet"

$script:IseAddonPath = "\ISEaddon\AutomationISE.dll"

$script:PowerShellToLoadAzureAutomationIseAddOnGeneric = @"
`n
# Start AzureAutomationISEAddOn snippet
Import-Module AzureAutomationAuthoringToolkit
# End AzureAutomationISEAddOn snippet
"@

$script:IseProfileFileName = "Microsoft.PowerShellISE_profile.ps1"

function _findObjectByName {
    param(
        [object] $ObjectArray,
        [string] $Name
    )
    
    $ObjectArray | Where-Object {
        return $_.Name -eq $Name
    }
}

function _DecryptValue {
    param(
        [object] $Value,
        [switch] $SupressCouldNotDecryptWarning
    )

    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration

    if($Value -isnot [string]) {
        ## the local assets files store all encrypted values as strings, so if value is not a string, it is not an encrypted value.
        ## in this case, return the raw value
    
        Write-Verbose "AzureAutomationAuthoringToolkit: Value is not encrypted. Returning raw value without decrypting"
        return $Value
    }
    elseif($Configuration.EncryptionCertificateThumbprint -eq "none") {
        Write-Verbose "AzureAutomationAuthoringToolkit: No encryption certificate specified. Returning raw value without decrypting"
        return $Value
    }
    else {
        $Thumbprint = $Configuration.EncryptionCertificateThumbprint
        
        Write-Verbose "AzureAutomationAuthoringToolkit: Decrypting encrypted value '$Value' using encryption certificate with thumbprint '$Thumbprint'"

        if (Test-Path -Path Cert:\CurrentUser\My\$Thumbprint) {
            $Cert = Get-Item -Path Cert:\CurrentUser\My\$Thumbprint
            
            try {
                $Encrypted = [Convert]::FromBase64String($Value)
                $Bytes = $Cert.PrivateKey.Decrypt($Encrypted, $True)
                $EncryptedValue = [Text.Encoding]::UTF8.GetString($Bytes)

                # the encrypted value is a JSON string (so that we can encrypt non-string types), so convert it back to a proper object
                $EncryptedValue = ConvertFrom-Json -InputObject $EncryptedValue

                return $EncryptedValue
            }
            catch {
                
                if(!$SupressCouldNotDecryptWarning.IsPresent) {
                    Write-Warning "AzureAutomationAuthoringToolkit: Warning - Could not decrypt value '$Value' using encryption certificate with thumbprint '$Thumbprint'.
                    Returning raw value instead. Are you sure the value was encrypted with this certificate?"
                }

                return $Value 
            }
        }
        else { 
            throw "Encryption certificate with thumbprint '$Thumbprint' is not installed in the user cert store"
        }
    }
}

function _EncryptValue {
    param(
        [object] $Value
    )

    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration

    if($Configuration.EncryptionCertificateThumbprint -eq "none") {
        Write-Verbose "AzureAutomationAuthoringToolkit: No encryption certificate specified. Not encrypting value"
        return $Value
    }
    else {
        $Thumbprint = $Configuration.EncryptionCertificateThumbprint
        
        Write-Verbose "AzureAutomationAuthoringToolkit: Encrypting value using encryption certificate with thumbprint '$Thumbprint'"

        if (Test-Path -Path Cert:\CurrentUser\My\$Thumbprint) {
                       
            # convert the value to a JSON string so that we don't lost type info when decrypting
            $JsonValue = ConvertTo-Json -InputObject $Value -Depth 999

            $Cert = Get-Item -Path Cert:\CurrentUser\My\$Thumbprint
            $Bytes = [Text.Encoding]::UTF8.GetBytes($JsonValue)
            $Encrypted = $Cert.PublicKey.Key.Encrypt($Bytes, $True)
            $Value = [Convert]::ToBase64String($Encrypted)

            return $Value
        }
        else { 
            throw "Encryption certificate with thumbprint '$Thumbprint' is not installed in the user cert store"
        }
    }
}

<#
    .SYNOPSIS
    Copies configuration file for AzureAutomationAuthoringToolkit to user's profile directory
#>
function Copy-ConfigFile
{
    Write-Verbose "AzureAutomationAuthoringToolkit: Copying '$script:ConfigurationFileName' configuration file to '$script:ConfigurationPath'"

    $ConfigurationFile = Get-Item -Path $script:ConfigurationPath -ErrorAction SilentlyContinue

    if(!$ConfigurationFile) {
        Copy-Item -Path (Join-Path $PSScriptRoot $script:ConfigurationFileName) -Destination $script:ConfigurationPath
    }
    else {
        Write-Verbose "AzureAutomationAuthoringToolkit: '$script:ConfigurationPath' already present. Not copying file."
    }
}

<#
        .SYNOPSIS
        Sets up the Azure Automation ISE add-on for use in the PowerShell ISE.
#>
function Install-AzureAutomationIseAddOn {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    
    $IseProfilePath = Join-Path (Split-Path $Profile) $script:IseProfileFileName

    if(!(Test-Path $IseProfilePath)) {
        Write-Verbose "AzureAutomationAuthoringToolkit: '$IseProfilePath' does not exist, creating it"
        New-Item -Path $IseProfilePath -ItemType File -Force | Out-Null
    }

    $ProfileContent = Get-Content $IseProfilePath -Raw
    if(!$ProfileContent) { $ProfileContent = "" }
    $StartProfileSnippetIndex = $ProfileContent.IndexOf($script:StartProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn)

    # add content to PS ISE profile to load ISe add on on start up
    Write-Verbose "AzureAutomationAuthoringToolkit: Adding content to '$IseProfilePath' to cause ISE add-on to load on PowerShell ISE start up"

    if($StartProfileSnippetIndex -eq -1) {
        $IsRunningInISE = (Split-Path $Profile -Leaf) -eq $script:IseProfileFileName
    
        # add loading of the ISE add-on into the PS ISE Profile so it is automatically loaded each time the ISE is opened
        $IseAddOnModuleFolderPath = Split-Path $PSScriptRoot -Parent
    
        $PowerShellToLoadAzureAutomationIseAddOnWithPath = $script:PowerShellToLoadAzureAutomationIseAddOnGeneric -f $IseAddOnModuleFolderPath, $script:IseAddonPath
    
        Add-Content $IseProfilePath $PowerShellToLoadAzureAutomationISEAddOnWithPath

        if($IsRunningInISE) {
            # load the ISE add-on into the PS ISE session already open
            Write-Verbose "AzureAutomationAuthoringToolkit: Loading ISE add-on into this PowerShell ISE session"
            Invoke-Expression $PowerShellToLoadAzureAutomationIseAddOnWithPath
        }
    }
    else {
         Write-Verbose "AzureAutomationAuthoringToolkit: Content already present. Not adding any content."
    }
}

<#
        .SYNOPSIS
        Removes the Azure Automation ISE add-on from the PowerShell ISE.
#>
function Uninstall-AzureAutomationIseAddOn {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    
    $IseProfilePath = Join-Path (Split-Path $Profile) $script:IseProfileFileName
    $ProfileContent = Get-Content $IseProfilePath -Raw
    
    $StartProfileSnippetIndex = $ProfileContent.IndexOf($script:StartProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn)
    $EndProfileSnippetIndex = $ProfileContent.IndexOf($script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn)

    if($StartProfileSnippetIndex -gt -1 -and $EndProfileSnippetIndex -gt -1) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Removing content from '$IseProfilePath' to cause ISE add-on to no longer load on PowerShell ISE start up"
        
        $NewProfileContent = $ProfileContent.Substring(0, $StartProfileSnippetIndex)
        $NewProfileContent += $ProfileContent.Substring($EndProfileSnippetIndex + $script:EndProfileSnippetForPowerShellToLoadAzureAutomationISEAddOn.Length)

        $NewProfileContent | Set-Content $IseProfilePath
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
    $AssetWasInSecureLocalAssets = $False

    if($Asset) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Found local value for $Type asset '$Name.'"
        $AssetWasInSecureLocalAssets = $False
    }
    else {
        $Asset = _findObjectByName -ObjectArray $SecureLocalAssets.$Type -Name $Name

        if($Asset) {
            Write-Verbose "AzureAutomationAuthoringToolkit: Found secure local value for $Type asset '$Name.'"
            $AssetWasInSecureLocalAssets = $True
        }
    }

    if($Asset) {
        if($Type -eq "Certificate") {
            $AssetValue = Get-AzureAutomationAuthoringToolkitLocalCertificate -Name $Name -Thumbprint $Asset.Thumbprint
        }
        elseif($Type -eq "Variable") {
            
            if($Asset.Value -eq $Null) {
                Write-Warning "AzureAutomationAuthoringToolkit: Warning - Local Variable asset '$Name' has a value of null.
                If this was not intended, update its value in your local assets. "
            }
            
            if($AssetWasInSecureLocalAssets) {
                $AssetValue = (_DecryptValue -Value $Asset.Value)
            }
            else {
                $AssetValue = $Asset.Value
            }

        }
        elseif($Type -eq "Connection") {
            # Convert PSCustomObject to Hashtable
            $Temp = @{}

            $Asset.ValueFields.psobject.properties | ForEach-Object {
                
                if($_.Value -eq $Null) {
                    Write-Warning ("AzureAutomationAuthoringToolkit: Warning - Local Connection asset '$Name' has a null value for field '" + $_.Name + "'.
                    If this was not intended, update this connection field value in your local assets.")
                }
                
                # even though all connection fields may not be encrypted, try to decrypt them all since we don't know which are encrypted and which are not.
                # if decryption fails (because field was not encrypted), supress the warning that decryption failed, since could be perfectly fine in this case.
                # when decryption fails, _DecryptValue returns the raw value, which is what we want
                $Temp."$($_.Name)" = (_DecryptValue -Value $_.Value -SupressCouldNotDecryptWarning)
            }

            $AssetValue = $Temp
        }
        elseif($Type -eq "PSCredential") {
            
            if($Asset.Password -eq $Null) {
                Write-Warning "AzureAutomationAuthoringToolkit: Warning - Local PSCredential asset '$Name' has a password value of null.
                If this was not intended, update its password value in your local assets. "
            }
            
            $AssetValue = @{
                Username = $Asset.Username
                Password = (_DecryptValue -Value $Asset.Password)
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
        '$script:ConfigurationPath' is incorrect. Make sure the file exists, contains valid JSON, and contains 'LocalAssetsPath', 
    'SecureLocalAssetsPath', and 'EncryptionCertificateThumbprint' settings."

    Write-Verbose "AzureAutomationAuthoringToolkit: Grabbing AzureAutomationAuthoringToolkit configuration from '$script:ConfigurationPath'"

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

    if(!($Configuration.LocalAssetsPath -and $Configuration.SecureLocalAssetsPath -and $Configuration.EncryptionCertificateThumbprint)) {
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
                $_.Value = (_EncryptValue -Value $Value)
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
