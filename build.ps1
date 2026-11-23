$ErrorActionPreference='Stop'
foreach($name in @('JavaManager','PythonManager')) {
    & (Join-Path $PSScriptRoot "$name\source\build.ps1")
}
