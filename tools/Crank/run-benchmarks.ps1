param(
    [Parameter(Mandatory = $true)]
    [string]
    $CrankAgentVm,

    [string]
    $BranchOrCommit = 'dev',

    [string]
    $Scenario = 'http',

    [string]
    $FunctionApp = 'HelloApp',

    [string]
    $InvokeCrankCommand,

    [switch]
    $WriteResultsToDatabase,

    [switch]
    $RefreshCrankContoller,

    [string]
    $UserName = 'Functions',

    [bool]
    $UseHttps = $true
)

$ErrorActionPreference = 'Stop'

#region Utilities

function InstallCrankController {
    dotnet tool install -g Microsoft.Crank.Controller --version "0.1.0-*"
}

function UninstallCrankController {
    dotnet tool uninstall -g microsoft.crank.controller
}

#endregion

#region Main

if (-not $InvokeCrankCommand) {
    if (Get-Command crank -ErrorAction SilentlyContinue) {
        if ($RefreshCrankContoller) {
            Write-Warning 'Reinstalling crank controller...'
            UninstallCrankController
            InstallCrankController
        }
    } else {
        Write-Warning 'Crank controller is not found, installing...'
        InstallCrankController
    }
    $InvokeCrankCommand = 'crank'
}

$crankConfigPath = Join-Path `
                    -Path (Split-Path $PSCommandPath -Parent) `
                    -ChildPath 'benchmarks.yml'

$isLinuxApp = $CrankAgentVm -match '\blinux\b'

$functionAppRootPath = if ($isLinuxApp) { "/home/$UserName/FunctionApps" } else { 'C:\FunctionApps' }
$functionAppPath = Join-Path `
                    -Path $functionAppRootPath `
                    -ChildPath $FunctionApp

$tmpPath = if ($isLinuxApp) { "/tmp" } else { 'C:\Temp' }
$tmpLogPath = if ($isLinuxApp) { "/tmp/functions/log" } else { 'C:\Temp\Functions\Log' }

if ($UseHttps) {
    $aspNetUrls = "http://localhost:5000;https://localhost:5001"
    $profile = "localHttps"
}
else {
    $aspNetUrls = "http://localhost:5000"
    $profile = "local"
}

$crankArgs =
    '--config', $crankConfigPath,
    '--scenario', $Scenario,
    '--profile', $profile,
    '--variable', "CrankAgentVm=$CrankAgentVm",
    '--variable', "FunctionAppPath=`"$functionAppPath`"",
    '--variable', "TempPath=`"$tmpPath`"",
    '--variable', "TempLogPath=`"$tmpLogPath`"",
    '--variable', "BranchOrCommit=$BranchOrCommit",
    '--variable', "AspNetUrls=$aspNetUrls"

if ($WriteResultsToDatabase) {
    Set-AzContext -Subscription 'Antares-Demo' > $null
    $sqlPassword = (Get-AzKeyVaultSecret -vaultName 'functions-crank-kv' -name 'SqlAdminPassword').SecretValueText

    $sqlConnectionString = "Server=tcp:functions-crank-sql.database.windows.net,1433;Initial Catalog=functions-crank-db;Persist Security Info=False;User ID=Functions;Password=$sqlPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    $crankArgs += '--sql', $sqlConnectionString
    $crankArgs += '--table', 'FunctionsPerf'
}

& $InvokeCrankCommand $crankArgs

#endregion
