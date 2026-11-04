# Publishes Roblox MultiClient as a self-contained, single-file Windows executable.
#
# The result runs on a machine with no .NET runtime installed and needs no installer or network
# access. Output: artifacts\publish\win-x64\RobloxMultiClient.exe
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $Output) { $Output = Join-Path $repo "artifacts\publish\$Runtime" }

Write-Output "publishing to $Output"
dotnet publish (Join-Path $repo 'src\RobloxMultiClient.App\RobloxMultiClient.App.csproj') `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:GenerateDocumentationFile=false `
    -o $Output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $Output 'RobloxMultiClient.exe'
if (-not (Test-Path $exe)) { throw "expected $exe after publish" }

$file = Get-Item $exe
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
Write-Output ''
Write-Output ("built  : {0}" -f $file.FullName)
Write-Output ("size   : {0:N0} bytes" -f $file.Length)
Write-Output ("sha256 : {0}" -f $hash)
