$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Start-ViteIfNeeded($port, $path) {
    $running = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue

    if (-not $running) {
        Start-Process "cmd" -ArgumentList "/k", "cd /d `"$path`" && npm run dev"
        Write-Host "Vite iniciado en puerto $port" -ForegroundColor Green
    }
    else {
        Write-Host "Vite ya está ejecutándose en puerto $port" -ForegroundColor Yellow
    }
}

Start-ViteIfNeeded 5173 "$root\src\Compas.Api\ClientApp"
Start-ViteIfNeeded 5174 "$root\src\Compas.Server\ClientApp"

Write-Host "Esperando a que Vite esté disponible..." -ForegroundColor Cyan

$timeout = 30
$elapsed = 0

while ($elapsed -lt $timeout) {
    $vite5173 = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
    $vite5174 = Get-NetTCPConnection -LocalPort 5174 -State Listen -ErrorAction SilentlyContinue

    if ($vite5173 -and $vite5174) {
        Write-Host "Los dos Vite están disponibles." -ForegroundColor Green
        exit 0
    }

    Start-Sleep -Seconds 1
    $elapsed++
}

Write-Error "Vite no ha arrancado correctamente."
exit 1