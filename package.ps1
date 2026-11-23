$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
$artifacts=Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$hashes=@()
foreach($name in @('JavaManager','PythonManager')) {
    $root=Join-Path $PSScriptRoot $name
    $destination=Join-Path $artifacts "$name-windows-x64.zip"
    if(Test-Path -LiteralPath $destination){throw "Package already exists: $destination"}
    $stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew)
    $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try {
        $files=@(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](data|__pycache__)[\\/]' -and $_.Extension -notin @('.pyc','.pyo')})
        foreach($file in $files) {
            $relative=$file.FullName.Substring($root.Length+1).Replace('\','/')
            $entry=$archive.CreateEntry("$name/$relative",[IO.Compression.CompressionLevel]::Optimal)
            $inputStream=[IO.File]::OpenRead($file.FullName)
            $outputStream=$entry.Open()
            try {$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
        }
        foreach($notice in @('THIRD_PARTY_NOTICES.md','docs\VALIDATION.md')) {
            $file=Join-Path $PSScriptRoot $notice
            $entry=$archive.CreateEntry("$name/"+[IO.Path]::GetFileName($file))
            $inputStream=[IO.File]::OpenRead($file)
            $outputStream=$entry.Open()
            try {$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
        }
    } finally {$archive.Dispose();$stream.Dispose()}
    $hashes+=((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($destination))
    Write-Output "Packaged: $destination"
}
$hashes | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ASCII
