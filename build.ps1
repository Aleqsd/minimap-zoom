param(
    [string]$DalamudHome = "$env:APPDATA\XIVLauncher\addon\Hooks\15.0.3.2",
    [string]$Dotnet,
    [string]$SourceCommit,
    [switch]$Check,
    [string]$GameExecutable = 'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\ffxiv_dx11.exe'
)
$ErrorActionPreference = 'Stop'
if (!$Dotnet) {
    $miniSharedSdk = Join-Path $PSScriptRoot '..\.tools\dotnet\dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $miniSharedSdk) { $miniSharedSdk } else { 'dotnet' }
}
$miniDalamudPath = (Resolve-Path -LiteralPath $DalamudHome).Path
& $Dotnet build (Join-Path $PSScriptRoot 'src\MinimapZoom.csproj') -c Release "-p:DalamudHome=$miniDalamudPath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
if ($Check) {
    $miniCheckArgs = @()
    if (Test-Path -LiteralPath $GameExecutable) { $miniCheckArgs = @('--game-exe', $GameExecutable) }
    & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\MinimapZoom.Checks.csproj') -c Release "-p:DalamudHome=$miniDalamudPath" -- @miniCheckArgs
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
}
$miniBuildOutput = Join-Path $PSScriptRoot 'src\bin\Release\net10.0-windows'
$miniBuiltDll = Join-Path $miniBuildOutput 'MinimapZoom.dll'
$miniPdbText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $miniBuildOutput 'MinimapZoom.pdb')))
if ($miniPdbText -match '[A-Za-z]:[\\/]+Users[\\/]+|[/\\]home[/\\]') {
    throw 'Portable PDB contains a user profile path. Verify PathMap and SourceLink before distribution.'
}
$miniAssemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($miniBuiltDll).Version
$miniManifest = Get-Content -LiteralPath (Join-Path $miniBuildOutput 'MinimapZoom.json') -Raw | ConvertFrom-Json
if ($miniManifest.AssemblyVersion -ne $miniAssemblyVersion.ToString()) {
    throw "Manifest version $($miniManifest.AssemblyVersion) differs from DLL version $miniAssemblyVersion."
}
$miniVersion = $miniAssemblyVersion.ToString(3)
$miniHash = (Get-FileHash -LiteralPath $miniBuiltDll -Algorithm SHA256).Hash
$miniBuildInfo = [ordered]@{
    Version = $miniVersion
    AssemblyVersion = $miniAssemblyVersion.ToString()
    Sha256 = $miniHash
    BuiltAtUtc = [DateTime]::UtcNow.ToString('o')
    SourceCommit = $SourceCommit
} | ConvertTo-Json
$miniReleaseOutput = Join-Path $PSScriptRoot "releases\$miniVersion"
$miniStableOutput = Join-Path $PSScriptRoot 'plugin'
foreach ($miniOutput in @($miniReleaseOutput, $miniStableOutput)) {
    New-Item -ItemType Directory -Path $miniOutput -Force | Out-Null
    # Keep the manifest beside the DLL; copy the DLL last for Dalamud's file watcher.
    foreach ($miniName in @('MinimapZoom.json', 'MinimapZoom.pdb', 'MinimapZoom.dll')) {
        Copy-Item -LiteralPath (Join-Path $miniBuildOutput $miniName) -Destination (Join-Path $miniOutput $miniName) -Force
    }
    $miniCopiedDll = Join-Path $miniOutput 'MinimapZoom.dll'
    if ((Get-FileHash -LiteralPath $miniCopiedDll -Algorithm SHA256).Hash -ne $miniHash) {
        throw "Copied DLL failed SHA-256 verification: $miniCopiedDll"
    }
    Set-Content -LiteralPath (Join-Path $miniOutput 'build-info.json') -Value $miniBuildInfo -Encoding utf8
    [pscustomobject]@{ Version = $miniVersion; Dll = $miniCopiedDll; Sha256 = $miniHash }
}
