[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppVersion = "1.0.0",
    [string]$IsccPath = "",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($RuntimeIdentifier -ne "win-x64") {
    throw "This installer script currently supports only win-x64. Please use -RuntimeIdentifier win-x64."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "DMSJ_Blood Alcohol.csproj"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$stageRoot = Join-Path $env:TEMP "DMSJ_Blood_Alcohol_setup_stage"
$stageRepo = Join-Path $stageRoot "repo"
$stageProjectPath = Join-Path $stageRepo "DMSJ_Blood Alcohol.csproj"
$stageIssPath = Join-Path $stageRepo "installer\setup.iss"
$stagePublishDir = Join-Path $stageRepo "artifacts\publish\win-x64"
$stageInstallerDir = Join-Path $stageRepo "artifacts\installer"
$publishDirRelative = "artifacts\publish\win-x64"

if (-not (Test-Path $projectPath)) {
    throw "Project file was not found: $projectPath"
}

if (-not (Test-Path (Join-Path $PSScriptRoot "setup.iss"))) {
    throw "Inno Setup script was not found: $(Join-Path $PSScriptRoot 'setup.iss')"
}

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

if (Test-Path $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stageRepo | Out-Null

Write-Host "==> Preparing clean staging workspace"
$stageCopyCommand = "robocopy ""$repoRoot"" ""$stageRepo"" /MIR /R:1 /W:1 /XD "".git"" "".svn"" "".vs"" "".vscode"" ""bin"" ""obj"" ""obj_codex"" ""artifacts"" /XF ""*_wpftmp.csproj"" ""*.user"""
$robocopyProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $stageCopyCommand -Wait -NoNewWindow -PassThru
if ($robocopyProcess.ExitCode -gt 7) {
    throw "Failed to stage a clean workspace via robocopy."
}

if (-not (Test-Path $stageProjectPath)) {
    throw "Staged project file was not found: $stageProjectPath"
}

Write-Host "==> Publishing application"
$publishCommand = "dotnet publish ""DMSJ_Blood Alcohol.csproj"" -c ""$Configuration"" -r ""$RuntimeIdentifier"" --self-contained $selfContained -p:PublishSingleFile=false -p:DebugType=None -o ""$publishDirRelative"""
$publishProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $publishCommand -WorkingDirectory $stageRepo -Wait -NoNewWindow -PassThru
if ($publishProcess.ExitCode -ne 0) {
    throw "dotnet publish failed."
}

$resolvedIsccPath = $null
if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    if (Test-Path $IsccPath) {
        $resolvedIsccPath = (Resolve-Path $IsccPath).Path
    }
    else {
        throw "The specified ISCC.exe path does not exist: $IsccPath"
    }
}

if (-not $resolvedIsccPath) {
    $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $resolvedIsccPath = if ($isccCommand) { $isccCommand.Source } else { $null }
}

if (-not $resolvedIsccPath) {
    $fallbackPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $fallbackPaths) {
        if (Test-Path $candidate) {
            $resolvedIsccPath = $candidate
            break
        }
    }
}

if (-not $resolvedIsccPath) {
    $registryCandidates = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1"
    )

    foreach ($key in $registryCandidates) {
        try {
            $installLocation = (Get-ItemProperty -Path $key -ErrorAction Stop).InstallLocation
            if (-not [string]::IsNullOrWhiteSpace($installLocation)) {
                $candidate = Join-Path $installLocation "ISCC.exe"
                if (Test-Path $candidate) {
                    $resolvedIsccPath = $candidate
                    break
                }
            }
        }
        catch {
        }
    }
}

if (-not $resolvedIsccPath) {
    throw "Publish completed, but ISCC.exe was not found. Please install Inno Setup 6 and run installer\\build-installer.ps1 again."
}

Write-Host "==> Building Inno Setup package"
$isccArgs = @(
    "/DMyAppVersion=$AppVersion",
    $stageIssPath
)

$isccProcess = Start-Process -FilePath $resolvedIsccPath -ArgumentList $isccArgs -Wait -NoNewWindow -PassThru
if ($isccProcess.ExitCode -ne 0) {
    throw "Inno Setup compilation failed."
}

Write-Host "==> Copying installer back to repository"
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
$installerCopyCommand = "robocopy ""$stageInstallerDir"" ""$installerDir"" /MIR /R:1 /W:1"
$installerCopyProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $installerCopyCommand -Wait -NoNewWindow -PassThru
if ($installerCopyProcess.ExitCode -gt 7) {
    throw "Failed to copy installer output back to the repository."
}

Write-Host "==> Done"
Write-Host "Staged publish directory: $stagePublishDir"
Write-Host "Installer directory: $installerDir"
