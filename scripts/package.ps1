# Builds the release executable and packages it into a ZIP archive.
#
#   scripts/package.ps1                 -> dist\RobloxMultiClient-<version>-win-x64.zip
#                                          executable, README, licence, docs, branding
#   scripts/package.ps1 -WithSource     -> dist\RobloxMultiClient-<version>-full.zip
#                                          the same, plus every tracked source file, in a folder
#
# Entries are written with the .NET ZIP writer so they always use forward slashes: the archive
# then extracts identically with PowerShell's Expand-Archive, Info-ZIP unzip, 7-Zip and Explorer.
param(
    [string]$Version = '1.0.0',
    [string]$Runtime = 'win-x64',
    [switch]$SkipPublish,
    [switch]$WithSource
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $repo "artifacts\publish\$Runtime"
$dist = Join-Path $repo 'dist'
$name = if ($WithSource) { "RobloxMultiClient-$Version-full" } else { "RobloxMultiClient-$Version-$Runtime" }
$stage = Join-Path $repo "artifacts\package\$name"
$zip = Join-Path $dist "$name.zip"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'publish.ps1') -Runtime $Runtime
    if ($LASTEXITCODE -ne 0) { throw "publish failed with exit code $LASTEXITCODE" }
}

$exe = Join-Path $publish 'RobloxMultiClient.exe'
if (-not (Test-Path $exe)) { throw "no published executable at $exe (run scripts/publish.ps1)" }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage, $dist | Out-Null

if ($WithSource) {
    # Exactly what the repository tracks at HEAD, so the package can be rebuilt and audited, and no
    # build output, secrets or machine state rides along.
    $tracked = & git -C $repo ls-files -z
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed: the source package must be built in a checkout' }
    foreach ($path in ($tracked -split "`0" | Where-Object { $_ })) {
        $target = Join-Path $stage $path
        New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
        Copy-Item (Join-Path $repo $path) $target -Force
    }
}
else {
    Copy-Item (Join-Path $repo 'README.md') $stage
    Copy-Item (Join-Path $repo 'LICENSE') $stage
    Copy-Item (Join-Path $repo 'THIRD-PARTY-NOTICES.md') $stage
    Copy-Item (Join-Path $repo 'docs') $stage -Recurse
    Copy-Item (Join-Path $repo 'assets\branding') (Join-Path $stage 'branding') -Recurse
}
Copy-Item $exe $stage

if (Test-Path $zip) { Remove-Item $zip -Force }

# ZipFile.CreateFromDirectory on Windows PowerShell (any .NET Framework host) stores the relative
# path with a backslash, which POSIX unzip turns into a literal file name. Entries are therefore
# written explicitly with forward slashes, which every extractor understands.
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $prefix = $stage.TrimEnd('\') + '\'
    $folder = if ($WithSource) { "RobloxMultiClient-$Version/" } else { '' }
    foreach ($file in Get-ChildItem -Path $stage -Recurse -File | Sort-Object FullName) {
        $entryName = $folder + $file.FullName.Substring($prefix.Length).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

$exeHash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$exeSize = (Get-Item $exe).Length
$zipSize = (Get-Item $zip).Length

# The sidecar is the published record of what was built; it is not inside the archive.
$checksums = Join-Path $dist "$name.sha256"
@(
    "$exeHash  RobloxMultiClient.exe  ($exeSize bytes)"
    "$zipHash  $name.zip  ($zipSize bytes)"
) | Set-Content -Path $checksums -Encoding ASCII

Write-Output ''
Write-Output ("exe    : {0}" -f $exe)
Write-Output ("size   : {0:N0} bytes" -f $exeSize)
Write-Output ("sha256 : {0}" -f $exeHash)
Write-Output ("archive: {0}" -f $zip)
Write-Output ("size   : {0:N0} bytes" -f $zipSize)
Write-Output ("sha256 : {0}" -f $zipHash)
Write-Output ("sums   : {0}" -f $checksums)
