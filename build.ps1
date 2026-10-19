# Builds the Little Big Halloween Racing Adventure installer: out\LBRA-Setup.exe, one self-contained file (no .NET needed to run it).
#   1. LBA Assembler's engine (lba2cc.exe, statically linked), from the LBA Assembler repo beside this one -- the installer carries it in;
#   2. the game's pictures, if they aren't drawn yet (tools\make_art.ps1);
#   3. the installer, published as a single file, its version the build's date and time (yyyy.M.d.HHmm): an installed game's updater
#      takes a setup from the site only if it is no older than itself (Installer\Updater.cs).
# Windows PowerShell 5.1 or later; MSYS2's UCRT64 toolchain for the engine (C:\msys64), the .NET 10 SDK.
#   -Lba <folder>   another LBA Assembler checkout than the one beside this repo (a worktree, for a test build)
#   -Out <folder>   where the setup goes (out\)
#   -Version <v>    the setup's version (the build's date and time)
param([switch]$SkipEngine, [string]$Lba, [string]$Out, [string]$Version)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$lba = if ($Lba) { (Resolve-Path $Lba).Path } else { Join-Path (Split-Path -Parent $root) 'LBAAssembler' }
if (-not (Test-Path (Join-Path $lba 'LBAAssembler.csproj'))) { throw "The LBA Assembler repo isn't beside this one ($lba)." }

if (-not $SkipEngine) {
    $env:PATH = 'C:\msys64\ucrt64\bin;C:\msys64\usr\bin;' + $env:PATH
    Push-Location (Join-Path $lba 'native\lba2-classic-community')
    try {
        cmake --build out/build/windows_ucrt64_static --target lba2cc
        if ($LASTEXITCODE -ne 0) { throw 'The engine did not build.' }
    } finally { Pop-Location }
}

if (-not (Test-Path (Join-Path $root 'Game\menu.png')) -or -not (Test-Path (Join-Path $root 'Installer\pumpkin.ico'))) {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\make_art.ps1')
}

$out = if ($Out) { $Out } else { Join-Path $root 'out' }
if (-not $Version) { $Version = (Get-Date).ToString('yyyy.M.d.HHmm') }
dotnet publish (Join-Path $root 'Installer\LBRAInstaller.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true "-p:Version=$Version" "-p:Lba=$lba" -o $out
if ($LASTEXITCODE -ne 0) { throw 'The installer did not build.' }
$exe = Join-Path $out 'LBRA-Setup.exe'
"{0} ({1:N0} MB), version {2}" -f $exe, ((Get-Item $exe).Length / 1MB), $Version
