# Read-only PNG audit. No Unity calls, source-image edits, re-encoding or library conversion.
param([string]$ProjectRoot='E:/WORLDCREATOR/new/Project_ANKALUTE')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$paths=@(
 'Assets/OSSASTRA/GroundWorkshop/Originals/Decorations/GrassTuft.png',
 'Assets/OSSASTRA/GroundWorkshop/Originals/Textures/Grass.png',
 'Assets/OSSASTRA/Art/WorldPropsReferenceLocal/DS_Grass2D_Adaptation.png'
)
$rows=foreach($relative in $paths){
 $path=Join-Path $ProjectRoot $relative
 $before=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
 $bitmap=[Drawing.Bitmap]::new($path)
 try{
  $alphaZero=0;$alphaPartial=0;$alphaOpaque=0;$clipHalf=0
  $minX=$bitmap.Width;$minY=$bitmap.Height;$maxX=-1;$maxY=-1
  for($y=0;$y -lt $bitmap.Height;$y++){
   for($x=0;$x -lt $bitmap.Width;$x++){
    $color=$bitmap.GetPixel($x,$y)
    if($color.A -eq 0){$alphaZero++}elseif($color.A -lt 243){$alphaPartial++}else{$alphaOpaque++}
    if($color.A -ge 128){$clipHalf++}
    if($color.A -ge 16){$minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x);$minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)}
   }
  }
  $count=$bitmap.Width*$bitmap.Height
  [pscustomobject]@{
   path=$relative;width=$bitmap.Width;height=$bitmap.Height;sha256=$before
   alphaDefinition='0 transparent; 1..242 partial; 243..255 opaque; bbox uses alpha>=16'
   alphaZero=$alphaZero;alphaZeroRatio=$alphaZero/$count
   alphaPartial=$alphaPartial;alphaPartialRatio=$alphaPartial/$count
   alphaOpaque=$alphaOpaque;alphaOpaqueRatio=$alphaOpaque/$count
   alphaAtLeast128=$clipHalf;alphaAtLeast128Ratio=$clipHalf/$count
   bboxTopLeft=@{xMin=$minX;yMin=$minY;xMax=$maxX;yMax=$maxY;width=$maxX-$minX+1;height=$maxY-$minY+1}
   sourceUnchanged=$before -eq (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
  }
 }finally{$bitmap.Dispose()}
}
$destination=Join-Path $ProjectRoot 'Candidates/VEGETATION-01A/AssetAudit/image-audit.json'
[IO.File]::WriteAllText($destination,($rows|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$rows|Select-Object path,width,height,alphaZeroRatio,alphaPartialRatio,alphaOpaqueRatio,sourceUnchanged|ConvertTo-Json -Compress
