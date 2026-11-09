param([string]$ImageDirectory='E:/WORLDCREATOR/new/Project_ANKALUTE/artifacts/CHAR-LIGHT-01/v1.3')
# 只读PNG；不生成或修改图像。输出仅本候选目录的JSON。
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$inputHashes=@(Get-ChildItem -LiteralPath $ImageDirectory -Filter '*.png' -File | Where-Object {$_.BaseName -match '^(noon-(A1|A2|no-inner|no-outer|body-only|light|medium|strong)|(sunset|night|warm-room|cool-room|eaves)-[AB])$'} | Get-FileHash -Algorithm SHA256)
$code=@'
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;
public sealed class CharHighlightPixelsImage:IDisposable {
 public Bitmap Bitmap; public byte[] Bytes; public int Width,Height,Stride;
 public CharHighlightPixelsImage(string path){Bitmap=new Bitmap(path);Width=Bitmap.Width;Height=Bitmap.Height;var locked=Bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Stride=Math.Abs(locked.Stride);Bytes=new byte[Stride*Height];if(locked.Stride<0)throw new InvalidOperationException("Unexpected negative pixel stride");Marshal.Copy(locked.Scan0,Bytes,0,Bytes.Length);}finally{Bitmap.UnlockBits(locked);}}
 public double Luma(int x,int y){int at=y*Stride+x*4;return Bytes[at+2]*.2126+Bytes[at+1]*.7152+Bytes[at]*.0722;}
 public void Dispose(){if(Bitmap!=null){Bitmap.Dispose();Bitmap=null;}}
}
public static class CharHighlightPixelsAnalysis {
 static double P(double[] sorted,double q){return sorted[Math.Max(0,Math.Min(sorted.Length-1,(int)Math.Round(q*(sorted.Length-1))))];}
 static object Box(Rectangle r){return new{x=r.X,y=r.Y,width=r.Width,height=r.Height};}
 public static object Roi(CharHighlightPixelsImage current,CharHighlightPixelsImage baseline,string name,Rectangle r){
  if(r.X<0||r.Y<0||r.Right>current.Width||r.Bottom>current.Height)throw new ArgumentException("ROI outside image");
  var ys=new double[r.Width*r.Height];int n=0,nearWhite=0,anyEndpoint=0,changed1=0,changed3=0;double sum=0,sumSquares=0,gradient=0,absRgb=0,absY=0,signedY=0,maxDiff=0;int neighbours=0;
  for(int y=r.Y;y<r.Bottom;y++)for(int x=r.X;x<r.Right;x++){
   int at=y*current.Stride+x*4,other=y*baseline.Stride+x*4;int b=current.Bytes[at],g=current.Bytes[at+1],red=current.Bytes[at+2];double v=current.Luma(x,y),old=baseline.Luma(x,y);ys[n++]=v;sum+=v;sumSquares+=v*v;
   if(Math.Min(red,Math.Min(g,b))>=250)nearWhite++;if(Math.Max(red,Math.Max(g,b))>=254)anyEndpoint++;
   double dr=Math.Abs(red-baseline.Bytes[other+2]),dg=Math.Abs(g-baseline.Bytes[other+1]),db=Math.Abs(b-baseline.Bytes[other]);double d=Math.Max(dr,Math.Max(dg,db));if(d>1)changed1++;if(d>3)changed3++;absRgb+=(dr+dg+db)/3;absY+=Math.Abs(v-old);signedY+=v-old;maxDiff=Math.Max(maxDiff,d);
   if(x+1<r.Right){gradient+=Math.Abs(v-current.Luma(x+1,y));neighbours++;}if(y+1<r.Bottom){gradient+=Math.Abs(v-current.Luma(x,y+1));neighbours++;}
  }
  Array.Sort(ys);double mean=sum/n;return new{name,rectangle=Box(r),pixels=n,encodedLumaMean=mean,encodedLumaStdDev=Math.Sqrt(Math.Max(0,sumSquares/n-mean*mean)),encodedLumaP10=P(ys,.10),encodedLumaP50=P(ys,.5),encodedLumaP90=P(ys,.9),encodedLumaP90MinusP10=P(ys,.9)-P(ys,.1),meanAdjacentEncodedLumaDifference=gradient/Math.Max(1,neighbours),nearWhiteAllChannels250Ratio=nearWhite/(double)n,anyChannel254Ratio=anyEndpoint/(double)n,relativeToA1=new{meanAbsoluteRgbByteDifference=absRgb/n,meanAbsoluteEncodedLumaDifference=absY/n,meanSignedEncodedLumaDifference=signedY/n,maximumChannelByteDifference=maxDiff,changedMoreThan1Ratio=changed1/(double)n,changedMoreThan3Ratio=changed3/(double)n}};
 }
 static bool Excluded(int x,int y,Rectangle[] exclude){for(int n=0;n<exclude.Length;n++)if(exclude[n].Contains(x,y))return true;return false;}
 public static object Outside(CharHighlightPixelsImage current,CharHighlightPixelsImage baseline,Rectangle[] exclude){
  long n=0,changed0=0,changed1=0,changed3=0;double rgb=0,max=0;var histogram=new long[256];
  for(int y=0;y<current.Height;y++)for(int x=0;x<current.Width;x++){if(Excluded(x,y,exclude))continue;int at=y*current.Stride+x*4,other=y*baseline.Stride+x*4;int db=Math.Abs(current.Bytes[at]-baseline.Bytes[other]),dg=Math.Abs(current.Bytes[at+1]-baseline.Bytes[other+1]),dr=Math.Abs(current.Bytes[at+2]-baseline.Bytes[other+2]);int d=Math.Max(db,Math.Max(dg,dr));n++;histogram[d]++;if(d>0)changed0++;if(d>1)changed1++;if(d>3)changed3++;rgb+=(db+dg+dr)/3.0;max=Math.Max(max,d);}
  Func<double,int> q=p=>{long at=(long)Math.Ceiling(n*p),acc=0;for(int i=0;i<256;i++){acc+=histogram[i];if(acc>=at)return i;}return 255;};return new{pixels=n,exclude=exclude.Select(Box).ToArray(),meanAbsoluteRgbByteDifference=rgb/n,maxChannelByteDifference=max,p95MaxChannelByteDifference=q(.95),p99MaxChannelByteDifference=q(.99),changedAny=changed0,changedAnyRatio=changed0/(double)n,changedMoreThan1Ratio=changed1/(double)n,changedMoreThan3Ratio=changed3/(double)n};
 }
}
'@
$references=@((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File).FullName)+@([Drawing.Bitmap].Assembly.Location)
Add-Type -TypeDefinition $code -ReferencedAssemblies $references
$names=@('noon-A1','noon-A2','noon-no-inner','noon-no-outer','noon-body-only','noon-light','noon-medium','noon-strong')
# These deliberately small fixed ROIs avoid background/outer halos. Labels describe visible regions,
# not an assertion about semantic atlas slots or the existence of authored input details.
$rois=@(
 @{name='Freycia-hair-crown';rect=[Drawing.Rectangle]::new(812,328,31,17)},
 @{name='Freycia-hair-fringe';rect=[Drawing.Rectangle]::new(810,345,30,14)},
 @{name='Freycia-skin-upper';rect=[Drawing.Rectangle]::new(816,386,19,13)},
 @{name='Freycia-skin-lower';rect=[Drawing.Rectangle]::new(814,415,18,18)},
 @{name='Freycia-eye-line-reference';rect=[Drawing.Rectangle]::new(829,363,3,2)},
 @{name='Blue-light-upper-hair';rect=[Drawing.Rectangle]::new(758,411,24,16)},
 @{name='Blue-light-lower-hair';rect=[Drawing.Rectangle]::new(757,438,24,13)},
 @{name='Blue-light-lower-surface';rect=[Drawing.Rectangle]::new(750,474,6,3)},
 @{name='Blue-dark-edge-reference';rect=[Drawing.Rectangle]::new(748,415,2,5)}
)
$exclude=@([Drawing.Rectangle]::new(781,302,98,159),[Drawing.Rectangle]::new(724,374,99,132))
$images=@{};$rows=@()
try{
 foreach($name in $names){$path=Join-Path $ImageDirectory ($name+'.png');$images[$name]=[CharHighlightPixelsImage]::new($path);if($images[$name].Width -ne 1600 -or $images[$name].Height -ne 900){throw 'Expected original1600x900 images'}}
 foreach($name in $names){$image=$images[$name];$path=Join-Path $ImageDirectory ($name+'.png');$stats=@(foreach($roi in $rois){[CharHighlightPixelsAnalysis]::Roi($image,$images['noon-A1'],$roi.name,$roi.rect)});$rows+=[pscustomobject]@{name=$name;file=$path;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;rois=$stats;outsideCharacterBoxes=[CharHighlightPixelsAnalysis]::Outside($image,$images['noon-A1'],$exclude)}}
 $result=[pscustomobject]@{schemaVersion=1;imageSize=@{width=1600;height=900};inputReadOnly=$true;units='8-bit display-encoded RGB and weighted encoded-luma, not HDR or scene-linear radiance';baseline='noon-A1';repeatNoise='noon-A2';limits=@('ROI selected by original screenshot; authored source texture and slot identity not independently mapped here','Near-white and endpoint counts are display proxies, not proof of HDR clipping stage','Dark ROI may contain antialiased background; inspect original screenshot before semantic interpretation','Outside-box changes include scene animation/UI/particles and must be compared to A/A, not assumed to be a scene-color regression','No pass/fail or universal near-white percentage threshold is assigned');rows=$rows}
 $output=Join-Path $PSScriptRoot 'noon-pixel-analysis.json';$result|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $output
 [pscustomobject]@{output=$output;images=$rows.Count;rois=$rois.Count;originalImagesUnchanged=$true}|ConvertTo-Json
 # Five separately captured pairs use the same actor pose within a pair, but a different pose
 # from noon. Never transplant noon pixel coordinates as if they were source texture samples.
 $environmentRois=@(
  @{name='Freycia-hair-crown';rect=[Drawing.Rectangle]::new(820,342,23,14)},
  @{name='Freycia-hair-fringe';rect=[Drawing.Rectangle]::new(816,377,30,13)},
  @{name='Freycia-skin-upper';rect=[Drawing.Rectangle]::new(817,406,17,10)},
  @{name='Freycia-skin-lower';rect=[Drawing.Rectangle]::new(825,425,10,16)},
  @{name='Blue-light-upper-hair';rect=[Drawing.Rectangle]::new(746,396,13,13)},
  @{name='Blue-light-torso-surface';rect=[Drawing.Rectangle]::new(760,425,13,14)}
 )
 $environmentExclusions=@([Drawing.Rectangle]::new(784,315,108,160),[Drawing.Rectangle]::new(708,350,110,140))
 $pairs=@()
 foreach($environment in @('sunset','night','warm-room','cool-room','eaves')){
  $aPath=Join-Path $ImageDirectory ($environment+'-A.png');$bPath=Join-Path $ImageDirectory ($environment+'-B.png')
  if(-not(Test-Path -LiteralPath $aPath) -or -not(Test-Path -LiteralPath $bPath)){continue}
  $a=[CharHighlightPixelsImage]::new($aPath);$b=[CharHighlightPixelsImage]::new($bPath)
  try{
   if($a.Width -ne 1600 -or $a.Height -ne 900 -or $b.Width -ne 1600 -or $b.Height -ne 900){throw 'Expected original 1600x900 paired images'}
   $roiPairs=@(foreach($roi in $environmentRois){[pscustomobject]@{name=$roi.name;A=[CharHighlightPixelsAnalysis]::Roi($a,$a,$roi.name,$roi.rect);B=[CharHighlightPixelsAnalysis]::Roi($b,$a,$roi.name,$roi.rect)}})
   $pairs+=[pscustomobject]@{environment=$environment;A=@{file=$aPath;sha256=(Get-FileHash -LiteralPath $aPath -Algorithm SHA256).Hash};B=@{file=$bPath;sha256=(Get-FileHash -LiteralPath $bPath -Algorithm SHA256).Hash};rois=$roiPairs;outsideCharacterBoxes=[CharHighlightPixelsAnalysis]::Outside($b,$a,$environmentExclusions)}
  }finally{$a.Dispose();$b.Dispose()}
 }
 $paired=[pscustomobject]@{schemaVersion=1;units=$result.units;inputReadOnly=$true;BParametersReportedByRoot=@{knee=.7;ceiling=1.0};comparison='Within each environment only: B relative to paired A. The nested relativeToA1 property means that paired A, not noon-A1.';limits=$result.limits;additionalLimits=@('Five environments have no A/A repeat capture; noon A/A is contextual only, not a calibrated noise estimate for other environments','Freycia hair crown in indoor A/B is visibly under a horizontal shadow; both lit skin and shaded hair are sampled separately','Fixed small ROIs cover only the inspected pose and surfaces; do not infer all character attachments or all source texture details');pairs=$pairs}
 $pairOutput=Join-Path $PSScriptRoot 'environment-pixel-analysis.json';$paired|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $pairOutput
 [pscustomobject]@{output=$pairOutput;pairs=$pairs.Count;roisPerPair=$environmentRois.Count;originalImagesUnchanged=$true}|ConvertTo-Json
 $integrity=@(foreach($before in $inputHashes){$after=(Get-FileHash -LiteralPath $before.Path -Algorithm SHA256).Hash;[pscustomobject]@{file=$before.Path;sha256=$before.Hash;unchanged=($before.Hash -eq $after)}})
 $integrity|ConvertTo-Json -Depth 3|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'input-integrity.json')
 if($integrity.unchanged -contains $false){throw 'One or more source screenshots changed while analysis was running'}
}finally{foreach($image in $images.Values){$image.Dispose()}}
