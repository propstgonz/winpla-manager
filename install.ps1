# WinPla installer. Usage:
#   irm https://raw.githubusercontent.com/propstgonz/winpla-manager/main/install.ps1 | iex
# Installs (or updates) into %APPDATA%\WinPla\app, creates the Start menu shortcut and opens the program.
# Never touches config.json or log.txt. Compatible with Windows PowerShell 5.1 and PowerShell 7.
# Test hook: $env:WINPLA_SOURCE_ZIP = local zip instead of downloading.
# Test hook: $env:WINPLA_NO_LAUNCH  = don't open the UI at the end.

function Install-WinPla {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # in 5.1 the progress bar slows the download down a lot
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $url  = 'https://github.com/propstgonz/winpla-manager/archive/refs/heads/main.zip'
    $base = Join-Path $env:APPDATA 'WinPla'
    $app  = Join-Path $base 'app'
    $tmp  = Join-Path ([IO.Path]::GetTempPath()) ('WinPla-' + [guid]::NewGuid().ToString('N'))

    # 1) pwsh is what runs the program; same logic as WinPla.ps1 (the Store alias if it exists)
    $pwsh = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'
    if (-not (Test-Path $pwsh)) {
        $cmd = Get-Command pwsh -ErrorAction SilentlyContinue
        if (-not $cmd) {
            Write-Host 'WinPla needs PowerShell 7. Install it with:' -ForegroundColor Yellow
            Write-Host '  winget install --id Microsoft.PowerShell --source winget'
            Write-Host 'then run this command again.'
            return
        }
        $pwsh = $cmd.Source
    }

    try {
        New-Item -ItemType Directory -Path $tmp | Out-Null

        # 2) download and extract
        $zip = $env:WINPLA_SOURCE_ZIP
        if (-not $zip) {
            Write-Host 'Downloading WinPla...'
            $zip = Join-Path $tmp 'winpla.zip'
            Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        }
        Write-Host 'Extracting...'
        $ext = Join-Path $tmp 'ext'
        Expand-Archive -LiteralPath $zip -DestinationPath $ext -Force

        # 3) validate before touching the existing installation (the zip root is winpla-manager-main\)
        $src = Get-ChildItem $ext -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'WinPla.ps1') } | Select-Object -First 1
        if (-not $src) { throw 'The downloaded package does not contain WinPla.ps1. Nothing was changed.' }
        $srcDir = $src.FullName.TrimEnd('\')

        # 4) prepare the new copy in a sibling folder and replace only if everything went well
        Write-Host 'Installing to' $app
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

        # 5) Start menu shortcut, no console
        $lnkArgs = '--headless "{0}" -NoProfile -STA -File "{1}\WinPla.ps1"' -f $pwsh, $app
        $lnk = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'WinPla.lnk'))
        $lnk.TargetPath = Join-Path $env:SystemRoot 'System32\conhost.exe'
        $lnk.Arguments = $lnkArgs
        $lnk.WorkingDirectory = $app
        $lnk.Save()
        Write-Host 'Shortcut created: Start menu > WinPla'

        # 7) open the UI without a console
        if (-not $env:WINPLA_NO_LAUNCH) {
            Write-Host 'Opening WinPla...'
            Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\conhost.exe') -ArgumentList $lnkArgs
        }
        Write-Host 'Done.' -ForegroundColor Green
    }
    catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red }
    finally {
        # 6) clean up temporary files
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $base 'app.new') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-WinPla
