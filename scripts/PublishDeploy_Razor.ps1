#!/usr/bin/env pwsh

# PRODUKTIV-Publish des Razor-Plugins nach <DeployRoot>/Plugins/SagasRazor.
# Default-DeployRoot ist das nativelib_deploy des Client-Repos (Schwester-Repo).
# Raeumt NUR den SagasRazor-Ordner (der Data-Unterordner mit Profilen/Scripts
# der User bleibt erhalten) - das Gesamt-Clean macht PublishDeploy_Client.ps1.
# Reihenfolge: Client -> Bootstrap -> Razor (dieses Script).
#
# UOSagas.AssistantApi.dll wird NICHT mitkopiert - die Assembly-Identitaet
# kommt vom Bootstrap-Host (siehe RazorDocs/SETUP.md).

param(
    [string]$DeployRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
$plugin_project = Join-Path -Path $scriptDir -ChildPath "../src/Razor.Plugin"
$temp_directory = Join-Path -Path $scriptDir -ChildPath "../bin/obj_razor_publish"

if ([string]::IsNullOrEmpty($DeployRoot)) {
    $DeployRoot = Join-Path -Path $scriptDir -ChildPath "../../ModernUO-Client/bin/nativelib_deploy"
}

if (-Not (Test-Path -Path $plugin_project)) {
    Write-Host "Plugin project not found: $plugin_project"
    exit 1
}

if (-Not (Test-Path -Path $DeployRoot)) {
    Write-Host "Deploy root not found: $DeployRoot"
    Write-Host "Zuerst PublishDeploy_Client.ps1 im Client-Repo laufen lassen (oder -DeployRoot angeben)."
    exit 1
}

$target_directory = Join-Path -Path $DeployRoot -ChildPath "Plugins/SagasRazor"

# ---- Release-Publish in ein Temp-Verzeichnis --------------------------------
if (Test-Path -Path $temp_directory) {
    Remove-Item -Path $temp_directory -Recurse -Force -Confirm:$false
}

# RID-spezifisch (-r win-x64): dotnet legt die nativen Deps (libSkiaSharp,
# libHarfBuzzSharp, av_libglesv2, ...) FLACH neben die Managed-DLLs statt
# unter runtimes/<rid>/. Das ist Pflicht: der Bootstrap laedt das Plugin per
# Assembly.LoadFrom, und dieser Kontext wertet deps.json/runtimes NICHT aus -
# native Aufloesung findet nur Dateien direkt neben der Assembly.
dotnet publish $plugin_project -c Release -r win-x64 --self-contained false -o $temp_directory
if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet publish failed."
    exit 1
}

# ---- SagasRazor-Ordner leeren (Data bleibt) ---------------------------------
if (Test-Path -Path $target_directory) {
    Get-ChildItem -Path $target_directory -Force |
        Where-Object { $_.Name -ne "Data" } |
        Remove-Item -Recurse -Force -Confirm:$false
}
else {
    New-Item -ItemType Directory -Path $target_directory | Out-Null
}

# ---- Kopieren ---------------------------------------------------------------
# Ausgeschlossen: AssistantApi.dll (Identitaet kommt vom Host), Debug-Dateien
# und die Plugin-eigenen deps.json/runtimeconfig.json (Assembly.LoadFrom liest
# beide nicht - die Runtime-Konfiguration kommt vom Bootstrap-Host).
# ACHTUNG: die Avalonia-Plattform-Assemblies (X11/Native/Metal/DesignerSupport/
# Remote.Protocol/...) NICHT ausschliessen - ein Pruning-Versuch (2026-08-04)
# liess das Razor-Fenster transparent zurueck (nur Titelleiste): Avalonias
# Initialisierung fasst sie an und der Renderer stirbt still.
robocopy $temp_directory $target_directory /E /XF UOSagas.AssistantApi.dll *.pdb *.xml UOSagas.Razor.deps.json UOSagas.Razor.runtimeconfig.json /XD runtimes /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -gt 7) {
    Write-Host "robocopy failed with exit code $LASTEXITCODE."
    exit 1
}

Remove-Item -Path $temp_directory -Recurse -Force -Confirm:$false

Write-Host "Razor-Deploy fertig: $target_directory"
