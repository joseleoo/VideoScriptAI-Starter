[CmdletBinding()]
param(
    [ValidateSet("Launch", "Api", "Frontend", "InstallShortcut")]
    [string]$Mode = "Launch"
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$backendDir = Join-Path $projectRoot "backend"
$frontendDir = Join-Path $projectRoot "frontend"
$scriptPath = Join-Path $PSScriptRoot "start-videoscript-ai.ps1"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "VideoScript AI.lnk"

$systemPowerShell = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
$powerShellExe = if (Test-Path -LiteralPath $systemPowerShell) {
    $systemPowerShell
} else {
    (Get-Command "powershell.exe" -ErrorAction Stop).Source
}

function Assert-RequiredFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }
}

function Assert-RequiredCommand {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Assert-ProjectLayout {
    Assert-RequiredFile (Join-Path $backendDir "src\VideoScriptAI.API\VideoScriptAI.API.csproj")
    Assert-RequiredFile (Join-Path $frontendDir "package.json")
    Assert-RequiredFile $scriptPath
}

function Set-EngineEnvironment {
    $storagePath = Join-Path $projectRoot "storage"

    $env:VIDEOSCRIPTAI_FFMPEG_PATH = Join-Path $projectRoot "engines\ffmpeg\ffmpeg-9.1-essentials_build\bin\ffmpeg.exe"
    $env:VIDEOSCRIPTAI_WHISPER_PATH = Join-Path $projectRoot "engines\whisper\whisper-cli.exe"
    $env:VIDEOSCRIPTAI_MODELS_PATH = Join-Path $projectRoot "engines\whisper\models"
    $env:VIDEOSCRIPTAI_STORAGE_PATH = $storagePath

    if (-not (Test-Path -LiteralPath $storagePath)) {
        New-Item -ItemType Directory -Path $storagePath | Out-Null
    }
}

function Assert-EngineFiles {
    foreach ($path in @(
        $env:VIDEOSCRIPTAI_FFMPEG_PATH,
        $env:VIDEOSCRIPTAI_WHISPER_PATH,
        (Join-Path $env:VIDEOSCRIPTAI_MODELS_PATH "ggml-small.bin")
    )) {
        if (-not (Test-Path -LiteralPath $path)) {
            Write-Warning "Engine file not found: $path"
        }
    }
}

function Invoke-Api {
    Assert-ProjectLayout
    Assert-RequiredCommand "dotnet"
    Set-EngineEnvironment
    Assert-EngineFiles

    Push-Location $backendDir
    try {
        dotnet restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

        dotnet build --no-restore "src\VideoScriptAI.API\VideoScriptAI.API.csproj"
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

        dotnet run --no-build --project "src\VideoScriptAI.API\VideoScriptAI.API.csproj"
    }
    finally {
        Pop-Location
    }
}

function Invoke-Frontend {
    Assert-ProjectLayout
    Assert-RequiredCommand "node"
    Assert-RequiredCommand "npm"

    Push-Location $frontendDir
    try {
        if (-not (Test-Path -LiteralPath (Join-Path $frontendDir "node_modules"))) {
            npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
        }

        npm run dev
    }
    finally {
        Pop-Location
    }
}

function Start-ProjectWindow {
    param(
        [string]$Title,
        [string]$ChildMode
    )

    $command = "& { `$Host.UI.RawUI.WindowTitle = '$Title'; & '$scriptPath' -Mode $ChildMode }"
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    Start-Process -FilePath $powerShellExe -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-NoExit",
        "-EncodedCommand", $encodedCommand
    ) -WorkingDirectory $projectRoot
}

function Open-FrontendBrowser {
    $url = "http://localhost:5173"
    $deadline = (Get-Date).AddSeconds(60)
    $previousProgressPreference = $ProgressPreference
    $ProgressPreference = "SilentlyContinue"

    try {
        while ((Get-Date) -lt $deadline) {
            try {
                $response = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 2
                if ($response.StatusCode -lt 400) {
                    Start-Process $url
                    Write-Host "Opened $url in your browser."
                    return
                }
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }

        Write-Warning "Frontend did not become ready in time; open $url manually."
    }
    finally {
        $ProgressPreference = $previousProgressPreference
    }
}

function Install-DesktopShortcut {
    Assert-ProjectLayout

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $powerShellExe
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -Mode Launch' -f $scriptPath
    $shortcut.WorkingDirectory = $projectRoot
    $shortcut.IconLocation = "$powerShellExe,0"
    $shortcut.Description = "Starts the VideoScript AI API and frontend."
    $shortcut.Save()

    Write-Host "Desktop shortcut created: $shortcutPath"
}

switch ($Mode) {
    "Api" {
        Invoke-Api
    }
    "Frontend" {
        Invoke-Frontend
    }
    "InstallShortcut" {
        Install-DesktopShortcut
    }
    default {
        Assert-ProjectLayout
        Assert-RequiredCommand "dotnet"
        Assert-RequiredCommand "node"
        Assert-RequiredCommand "npm"
        Set-EngineEnvironment

        Start-ProjectWindow -Title "VideoScript AI - API" -ChildMode "Api"
        Start-ProjectWindow -Title "VideoScript AI - Frontend" -ChildMode "Frontend"
        Open-FrontendBrowser

        Write-Host "API and frontend are running in separate windows."
        Write-Host "Frontend: http://localhost:5173"
    }
}
