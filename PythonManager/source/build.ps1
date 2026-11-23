$ErrorActionPreference='Stop'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination=Split-Path -Parent $PSScriptRoot
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /win32icon:"$destination\assets\PythonManager.ico" /win32manifest:"$PSScriptRoot\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /out:"$destination\PythonManager.exe" "$PSScriptRoot\PythonCore.cs" "$PSScriptRoot\PythonManager.cs"
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Write-Output "Built: $destination\PythonManager.exe"
