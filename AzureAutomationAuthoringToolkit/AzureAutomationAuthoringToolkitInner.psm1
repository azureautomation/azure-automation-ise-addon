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

    $AssetValue = $StaticAssets.$Type.$Name

    if($AssetValue) {
        Write-Verbose "AzureAutomationAuthoringToolkit: Found static value for $Type asset '$Name.'"

        if($Type -eq "Certificate") {
            $AssetValue = Get-AzureAutomationAuthoringToolkitLocalCertificate -Name $Name -Thumbprint $AssetValue.Thumbprint
        }
    }
    else {
        $AssetValue = $SecureStaticAssets.$Type.$Name

        if($AssetValue) {
            Write-Verbose "AzureAutomationAuthoringToolkit: Found secure static value for $Type asset '$Name.'"
        }
        else {
            Write-Verbose "AzureAutomationAuthoringToolkit: Static value for $Type asset '$Name' not found."
        }
    }

    Write-Output $AssetValue
}

<#
    .SYNOPSIS
        Get the configuration for the Azure Automation Authoring Toolkit. Not meant to be called directly.
#>
function Get-AzureAutomationAuthoringToolkitConfiguration {       
    $ConfigurationError = "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit configuration defined in 
    '$script:ConfigurationPath' is incorrect. Make sure the file exists, contains valid JSON, and contains 'AutomationAccountName,'
    'StaticAssetsPath,' and 'SecureStaticAssetsPath' fields."

    Write-Verbose "AzureAutomationAuthoringToolkit: Grabbing AzureAutomationAuthoringToolkit configuration."

    try {
        $Configuration = Get-Content $script:ConfigurationPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error $ConfigurationError
        throw $_
    }

    if(!($Configuration.AutomationAccountName -and $Configuration.StaticAssetsPath -and $Configuration.SecureStaticAssetsPath)) {
        throw $ConfigurationError
    }

    Write-Output $Configuration
}

<#
    .SYNOPSIS
        Test if the Azure Automation Authoring Toolkit can talk to the proper Azure Automation account.
        Not meant to be called directly.
#>
function Test-AzureAutomationAuthoringToolkitAzureConnection {
    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration
    $AccountName = $Configuration.AutomationAccountName

    Write-Verbose "AzureAutomationAuthoringToolkit: Testing AzureAutomationAuthoringToolkit ability to connect to Azure."

    try {
        Get-AzureAutomationAccount -ErrorAction Stop | Out-Null
    }
    catch {
        Write-Error "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit could not connect to Azure.
        Make sure the Azure PowerShell module is installed and a connection from the Azure 
        PowerShell module to Azure has been set up with either Import-AzurePublishSettingsFile, 
        Set-AzureSubscription, or Add-AzureAccount. For more info see: http://azure.microsoft.com/en-us/documentation/articles/powershell-install-configure/#Connect"

        throw $_
    }
    
    try {
        Get-AzureAutomationAccount -Name $AccountName -ErrorAction Stop | Out-Null
    }
    catch {
        Write-Error "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit could not find the Azure 
        Automation account '$AccountName'. Make sure it exists in Azure Automation for the current subscription. If you 
        intended to use a different Azure Automation account, update the 'AutomationAccountName' field in $script:ConfigurationPath"

        throw $_
    }
    
    Write-Verbose "AzureAutomationAuthoringToolkit: AzureAutomationAuthoringToolkit was able to connect to Azure 
    and Automation account '$AccountName.'"  
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
    
    if(!$AssetValue) {
        $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration
        $AccountName = $Configuration.AutomationAccountName

        Write-Verbose "AzureAutomationAuthoringToolkit: Static variable asset named '$Name' not found, 
        attempting to use Azure Automation cmdlets to grab its value from '$AccountName' automation account."
        
        Test-AzureAutomationAuthoringToolkitAzureConnection

        $Variable = Get-AzureAutomationVariable -Name $Name -AutomationAccountName $AccountName

        if(!$Variable) {
            throw "AzureAutomationAuthoringToolkit: Variable asset named '$Name' does not exist in '$AccountName' automation account."
            ## TODO: check if Az Automation throws an exception if var asset not found, as we should match that behavior
        }
        else {           
            if($Variable.Encrypted) {
                throw "AzureAutomationAuthoringToolkit: Variable asset named '$Name' exists in '$AccountName' automation account, but it is encrypted."
            }
            else {
                $AssetValue = $Variable.Value
            }
        }
    }

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
    
    if(!$AssetValue) {
        throw "AzureAutomationAuthoringToolkit: Static Connection asset named '$Name' not found." 
        ## TODO: check if Az Automation throws an exception if connection asset not found, as we should match that behavior
    }
    else {
        # Convert PSCustomObject to Hashtable
        $AssetValue = $AssetValue.psobject.properties | foreach -begin {$h=@{}} -process {$h."$($_.Name)" = $_.Value} -end {$h}
    }

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
    
    if(!$AssetValue) {
        $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration
        $AccountName = $Configuration.AutomationAccountName

        Write-Verbose "AzureAutomationAuthoringToolkit: Static certificate asset named '$Name' not found, 
        attempting to use Azure Automation cmdlets to grab its thumbprint from '$AccountName' automation account."
        
        Test-AzureAutomationAuthoringToolkitAzureConnection

        $CertAsset = Get-AzureAutomationCertificate -Name $Name -AutomationAccountName $AccountName

        if(!$CertAsset) {
            throw "AzureAutomationAuthoringToolkit: Certificate asset named '$Name' does not exist in '$AccountName' automation account."
            ## TODO: check if Az Automation throws an exception if cert asset not found, as we should match that behavior
        }
        else {           
            try {
                $AssetValue = Get-AzureAutomationAuthoringToolkitLocalCertificate -Name $Name -Thumbprint $CertAsset.Thumbprint
            }
            catch {
                throw "AzureAutomationAuthoringToolkit: Certificate asset '$Name' referenced certificate with 
                thumbprint '$($CertAsset.Thumbprint)' but no certificate with that thumbprint exist on the local system." 
            }
        }
    }

    Write-Output $AssetValue
}

<#
    .SYNOPSIS
        Exports runbooks from Azure Automation to the local filesystem.
        Part of the Azure Automation Authoring Toolkit to help author runbooks locally.
#>
function Export-AzureAutomationRunbooksToLocal {
    [CmdletBinding(HelpUri='http://aka.ms/azureautomationauthoringtoolkit')]
    [OutputType([System.IO.FileInfo])]
    
    param(
        [Parameter(Mandatory=$False)]
        [string[]] $RunbookName,

        [Parameter(Mandatory=$False)]
        [string] $Path,

        [Parameter(Mandatory=$False)]
        [switch] $Draft,

        [Parameter(Mandatory=$False)]
        [switch] $Force
    )

    $Configuration = Get-AzureAutomationAuthoringToolkitConfiguration
    $AccountName = $Configuration.AutomationAccountName

    Test-AzureAutomationAuthoringToolkitAzureConnection

    if(!$RunbookName) {
        $RunbookName = (Get-AzureAutomationRunbook -AutomationAccountName $AccountName).Name
    }

    $Slot = "Published"
    if($Draft.IsPresent) {
        $Slot = "Draft"
    }

    if(!$Path) {
        $Path = (pwd).Path
    }

    $Output = @()

    $RunbookName | ForEach-Object {
        Write-Verbose "AzureAutomationAuthoringToolkit: Outputting runbook '$_' to $Path"
        
        $RbDefinition = Get-AzureAutomationRunbookDefinition -AutomationAccountName $AccountName -Name $_ -Slot $Slot
        $FilePath = "$Path\$_.ps1"
        $FilePresent = Get-Item -Path $FilePath -ErrorAction SilentlyContinue

        if($RbDefinition) {
            
            if(!$FilePresent -or ($FilePresent -and $Force.IsPresent)) {
                Remove-Item $FilePath -Force -ErrorAction SilentlyContinue
                $RbDefinition.Content >> $FilePath

                $Output += (Get-Item $FilePath)
            }
            else {
                Write-Error "AzureAutomationAuthoringToolkit: $FilePath already exists and -Force was not specified. Skipping runbook '$_'"
            }
        }
    }

    Write-Output $Output
}