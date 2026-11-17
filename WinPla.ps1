# WinPla launcher under PowerShell: compiles the SAME C# code as the exe in memory (Add-Type) and runs it.
# It exists because Smart App Control blocks unsigned exes that were compiled locally.
# Usage: pwsh -NoProfile -STA -File WinPla.ps1 [--startup | --selftest]   (no arguments: opens the window)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$logFile = Join-Path $env:APPDATA 'WinPla\log.txt'

# Runs without a console: every error ends up in the log (we can't use WinPla.Log, it may not have compiled).
function Write-Fail($text) {
    New-Item -ItemType Directory -Force (Split-Path $logFile) | Out-Null
    Add-Content -Path $logFile -Value ('{0:yyyy-MM-dd HH:mm:ss}  WinPla.ps1: {1}' -f (Get-Date), $text)
}

try {
    # With -ReferencedAssemblies, Add-Type drops its default references: pass the runtime ones from
    # $PSHOME/ref (minus its WindowsBase 4.0 stub) and the WPF/Drawing/Registry ones by full path in $PSHOME.
    $refs = Get-ChildItem (Join-Path $PSHOME 'ref/*.dll') -Exclude WindowsBase.dll | ForEach-Object FullName
    $refs += 'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Xaml', 'System.Drawing.Common',
             'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core', 'Microsoft.Win32.Registry', 'Microsoft.Win32.SystemEvents' | ForEach-Object { Join-Path $PSHOME "$_.dll" }
    $src = Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object FullName
    Add-Type -Path $src -ReferencedAssemblies $refs -CompilerOptions '/nullable:enable', '/nowarn:WPF0001'

    [WinPla.Ui]::Dir = Join-Path $root 'UI'
    $pwsh = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'   # stable alias of the Store version
    if (-not (Test-Path $pwsh)) { $pwsh = [Environment]::ProcessPath }
    [WinPla.Autostart]::Command = '"{0}\System32\conhost.exe" --headless "{1}" -NoProfile -STA -File "{2}" --startup' -f $env:SystemRoot, $pwsh, $PSCommandPath

    exit [WinPla.App]::Main([string[]]$args)
}
catch {
    Write-Fail ($_ | Out-String)
    exit 1
}
