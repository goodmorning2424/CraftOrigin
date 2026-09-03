param([switch]$NoBrowser)

$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $rootDirectory

function Test-CraftLiveServer {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200 -and
            $response.Content.Contains("CraftOrigin 3 Pad Firebase Simulator")
    }
    catch {
        return $false
    }
}

function Test-PortAvailable {
    param([int]$Port)

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new(
            [System.Net.IPAddress]::Loopback,
            $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function Get-AvailablePort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

$port = 8765
$url = "http://127.0.0.1:$port/simulator.html"

if (-not (Test-CraftLiveServer -Url $url)) {
    if (-not (Test-PortAvailable -Port $port)) {
        $port = Get-AvailablePort
        $url = "http://127.0.0.1:$port/simulator.html"
    }

    $pythonCommand = Get-Command py.exe -ErrorAction SilentlyContinue
    $serverArguments = @("-3", "-m", "http.server", "$port", "--bind", "127.0.0.1")
    if ($null -eq $pythonCommand) {
        $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
        $serverArguments = @("-m", "http.server", "$port", "--bind", "127.0.0.1")
    }

    if ($null -eq $pythonCommand) {
        throw "Python 3 was not found. Install Python or serve this WebGL folder over HTTP."
    }

    $serverProcess = Start-Process `
        -FilePath $pythonCommand.Source `
        -ArgumentList $serverArguments `
        -WorkingDirectory $rootDirectory `
        -WindowStyle Hidden `
        -PassThru

    $serverReady = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        if (Test-CraftLiveServer -Url $url) {
            $serverReady = $true
            break
        }
        if ($serverProcess.HasExited) {
            break
        }
    }

    if (-not $serverReady) {
        if (-not $serverProcess.HasExited) {
            Stop-Process -Id $serverProcess.Id -Force
        }
        throw "The local CraftOrigin HTTP server did not start."
    }
}

if ($NoBrowser) {
    Write-Output $url
}
else {
    Start-Process $url
}
