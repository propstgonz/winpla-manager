# Lanzador de WinPla bajo PowerShell: compila en memoria el MISMO código C# del exe (Add-Type) y lo ejecuta.
# Existe porque Smart App Control bloquea los exe sin firmar compilados localmente.
# Uso: pwsh -NoProfile -STA -File WinPla.ps1 [--startup | --selftest]   (sin argumentos: abre la ventana)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$logFile = Join-Path $env:APPDATA 'WinPla\log.txt'

# Corre sin consola: todo error acaba en el log (no podemos usar WinPla.Log, puede no haber compilado).
function Write-Fail($text) {
    New-Item -ItemType Directory -Force (Split-Path $logFile) | Out-Null
    Add-Content -Path $logFile -Value ('{0:yyyy-MM-dd HH:mm:ss}  WinPla.ps1: {1}' -f (Get-Date), $text)
}

try {
    # Con -ReferencedAssemblies Add-Type descarta sus referencias por defecto: se pasan las de runtime de
    # $PSHOME/ref (menos su stub de WindowsBase 4.0) y las de WPF/Drawing/Registro por ruta completa en $PSHOME.
    $refs = Get-ChildItem (Join-Path $PSHOME 'ref/*.dll') -Exclude WindowsBase.dll | ForEach-Object FullName
    $refs += 'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Xaml', 'System.Drawing.Common',
             'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core', 'Microsoft.Win32.Registry', 'Microsoft.Win32.SystemEvents' | ForEach-Object { Join-Path $PSHOME "$_.dll" }
    $src = Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object FullName
    Add-Type -Path $src -ReferencedAssemblies $refs -CompilerOptions '/nullable:enable', '/nowarn:WPF0001'

    [WinPla.Ui]::Dir = Join-Path $root 'UI'
    $pwsh = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'   # alias estable de la versión Store
    if (-not (Test-Path $pwsh)) { $pwsh = [Environment]::ProcessPath }
    [WinPla.Autostart]::Command = '"{0}\System32\conhost.exe" --headless "{1}" -NoProfile -STA -File "{2}" --startup' -f $env:SystemRoot, $pwsh, $PSCommandPath

    exit [WinPla.App]::Main([string[]]$args)
}
catch {
    Write-Fail ($_ | Out-String)
    exit 1
}
