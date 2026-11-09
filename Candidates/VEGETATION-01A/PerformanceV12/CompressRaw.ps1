param([Parameter(Mandatory)][string]$InputPath,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PWD 'LocalBuilds/VEGETATION-01A/artifacts/VEGETATION-01A'))+[IO.Path]::DirectorySeparatorChar
$source=[IO.Path]::GetFullPath($InputPath);$destination=[IO.Path]::GetFullPath($OutputPath)
$destRoot=[IO.Path]::GetFullPath((Join-Path $PWD 'artifacts/VEGETATION-01A/v12'))+[IO.Path]::DirectorySeparatorChar
if(!$source.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($destRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $destination)){throw 'Raw archive must stay in owned paths and not overwrite'}
$before=(Get-FileHash -LiteralPath $source).Hash;$bytes=(Get-Item -LiteralPath $source).Length
$src=[IO.File]::OpenRead($source);$dst=[IO.File]::Create($destination);$gzip=[IO.Compression.GZipStream]::new($dst,[IO.Compression.CompressionLevel]::Optimal)
try{$src.CopyTo($gzip)}finally{$gzip.Dispose();$dst.Dispose();$src.Dispose()}
$src=[IO.File]::OpenRead($destination);$gzip=[IO.Compression.GZipStream]::new($src,[IO.Compression.CompressionMode]::Decompress);$sha=[Security.Cryptography.SHA256]::Create()
try{$readback=[Convert]::ToHexString($sha.ComputeHash($gzip))}finally{$sha.Dispose();$gzip.Dispose();$src.Dispose()}
if($before -ne $readback){throw 'Compressed archive integrity mismatch; raw retained'}
Remove-Item -LiteralPath $source
[pscustomobject]@{archive=$destination;originalBytes=$bytes;compressedBytes=(Get-Item -LiteralPath $destination).Length;originalSha=$before;decompressedSha=$readback;rawReclaimed=$true}|ConvertTo-Json|Set-Content -LiteralPath ($destination+'.receipt.json') -Encoding utf8
