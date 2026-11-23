$ErrorActionPreference='Stop'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination=Split-Path -Parent $PSScriptRoot
$icon=Join-Path $destination 'assets\JavaManager.ico'
if(-not(Test-Path -LiteralPath $icon)){throw 'Missing icon: assets\JavaManager.ico'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /win32icon:"$icon" /win32manifest:"$PSScriptRoot\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /out:"$destination\JavaManager.exe" "$PSScriptRoot\JavaCore.cs" "$PSScriptRoot\JavaManager.cs"
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Write-Output "Built: $destination\JavaManager.exe"
