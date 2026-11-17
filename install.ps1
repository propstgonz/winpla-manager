# Instalador de WinPla. Uso:
#   irm https://raw.githubusercontent.com/propstgonz/winpla-manager/main/install.ps1 | iex
# Instala (o actualiza) en %APPDATA%\WinPla\app, crea el acceso directo del menú Inicio y abre el programa.
# No toca nunca config.json ni log.txt. Compatible con Windows PowerShell 5.1 y PowerShell 7.
# Gancho de pruebas: $env:WINPLA_SOURCE_ZIP = zip local en lugar de descargar.
# Gancho de pruebas: $env:WINPLA_NO_LAUNCH  = no abrir la interfaz al final.

function Install-WinPla {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # en 5.1 la barra de progreso ralentiza mucho la descarga
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $url  = 'https://github.com/propstgonz/winpla-manager/archive/refs/heads/main.zip'
    $base = Join-Path $env:APPDATA 'WinPla'
    $app  = Join-Path $base 'app'
    $tmp  = Join-Path ([IO.Path]::GetTempPath()) ('WinPla-' + [guid]::NewGuid().ToString('N'))

    # 1) pwsh es quien ejecuta el programa; misma lógica que WinPla.ps1 (alias de la Store si existe)
    $pwsh = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'
    if (-not (Test-Path $pwsh)) {
        $cmd = Get-Command pwsh -ErrorAction SilentlyContinue
        if (-not $cmd) {
            Write-Host 'WinPla necesita PowerShell 7. Instálalo con:' -ForegroundColor Yellow
            Write-Host '  winget install --id Microsoft.PowerShell --source winget'
            Write-Host 'y vuelve a ejecutar este comando.'
            return
        }
        $pwsh = $cmd.Source
    }

    try {
        New-Item -ItemType Directory -Path $tmp | Out-Null

        # 2) descargar y extraer
        $zip = $env:WINPLA_SOURCE_ZIP
        if (-not $zip) {
            Write-Host 'Descargando WinPla...'
            $zip = Join-Path $tmp 'winpla.zip'
            Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        }
        Write-Host 'Extrayendo...'
        $ext = Join-Path $tmp 'ext'
        Expand-Archive -LiteralPath $zip -DestinationPath $ext -Force

        # 3) validar antes de tocar la instalación existente (la raíz del zip es winpla-manager-main\)
        $src = Get-ChildItem $ext -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'WinPla.ps1') } | Select-Object -First 1
        if (-not $src) { throw 'El paquete descargado no contiene WinPla.ps1. No se ha modificado nada.' }
        $srcDir = $src.FullName.TrimEnd('\')

        # 4) preparar la copia nueva en una carpeta hermana y reemplazar solo si todo ha ido bien
        Write-Host 'Instalando en' $app
        $new = Join-Path $base 'app.new'
        $old = Join-Path $base 'app.old'
        Remove-Item $new, $old -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $new -Force | Out-Null
        $files = Get-ChildItem $srcDir -Recurse -File | Where-Object {
            $rel = $_.FullName.Substring($srcDir.Length + 1)
            $rel -notmatch '^(bin|obj|dist)\\' -and
            ($rel -in 'WinPla.ps1', 'WinPla.cmd' -or $rel -like '*.cs' -or $rel -like 'UI\*.xaml')
        }
        foreach ($f in $files) {
            $dest = Join-Path $new $f.FullName.Substring($srcDir.Length + 1)
            New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
            Copy-Item $f.FullName $dest
        }
        if (Test-Path $app) { Move-Item $app $old }
        try { Move-Item $new $app }
        catch { if (Test-Path $old) { Move-Item $old $app }; throw }
        Remove-Item $old -Recurse -Force -ErrorAction SilentlyContinue

        # 5) acceso directo del menú Inicio, sin consola
        $lnkArgs = '--headless "{0}" -NoProfile -STA -File "{1}\WinPla.ps1"' -f $pwsh, $app
        $lnk = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'WinPla.lnk'))
        $lnk.TargetPath = Join-Path $env:SystemRoot 'System32\conhost.exe'
        $lnk.Arguments = $lnkArgs
        $lnk.WorkingDirectory = $app
        $lnk.Save()
        Write-Host 'Acceso directo creado: menú Inicio > WinPla'

        # 7) abrir la interfaz sin consola
        if (-not $env:WINPLA_NO_LAUNCH) {
            Write-Host 'Abriendo WinPla...'
            Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\conhost.exe') -ArgumentList $lnkArgs
        }
        Write-Host 'Listo.' -ForegroundColor Green
    }
    catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red }
    finally {
        # 6) limpiar temporales
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $base 'app.new') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-WinPla
