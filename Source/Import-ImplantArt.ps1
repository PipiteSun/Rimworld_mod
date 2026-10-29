$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$artMod=Split-Path $PSScriptRoot -Parent
$artManifest=Get-Content (Join-Path $artMod 'ArtSource/ImplantStyle-1.6.12.json') -Raw | ConvertFrom-Json
$artSource=Join-Path $artMod 'ArtSource/ImplantStyle-1.6.12'
New-Item -ItemType Directory -Path $artSource -Force | Out-Null
$artStats=@()
foreach($asset in $artManifest.assets){
 $leaf=$asset.key.Replace('/','-')+'.png'
 $preserved=Join-Path $artSource $leaf
 if(Test-Path -LiteralPath $asset.source){Copy-Item -LiteralPath $asset.source -Destination $preserved -Force}
 if(!(Test-Path -LiteralPath $preserved)){throw "Missing generated source: $leaf"}
 $source=[Drawing.Bitmap]::new($preserved)
 $output=[Drawing.Bitmap]::new([int]$asset.size,[int]$asset.size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 try{
  $canvas=[Drawing.Graphics]::FromImage($output)
  try{
   $canvas.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
   $canvas.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $canvas.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $canvas.Clear([Drawing.Color]::Transparent)
   $canvas.DrawImage($source,[Drawing.Rectangle]::new(0,0,$output.Width,$output.Height),0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel)
  }finally{$canvas.Dispose()}
  $visible=0;$transparent=0
  for($y=0;$y -lt $output.Height;$y++){for($x=0;$x -lt $output.Width;$x++){$a=$output.GetPixel($x,$y).A;if($a -eq 0){$transparent++};if($a -gt 128){$visible++}}}
  if($transparent -lt $output.Width*$output.Height/4 -or $visible -lt 20){throw "Invalid transparent sprite: $leaf"}
  if($output.GetPixel(0,0).A -ne 0 -or $output.GetPixel($output.Width-1,$output.Height-1).A -ne 0){throw "Opaque corner: $leaf"}
  $destination=Join-Path $artMod ('Textures/Necromancy/'+$asset.key+'.png')
  $output.Save($destination,[Drawing.Imaging.ImageFormat]::Png)
  $artStats+=[pscustomobject]@{Asset=$asset.key;Size=$output.Width;VisiblePixels=$visible;TransparentPixels=$transparent;Bytes=(Get-Item $destination).Length}
 }finally{$source.Dispose();$output.Dispose()}
}
$artStats | Format-Table -AutoSize
# Presentation only: a contact sheet of the imported assets at game-friendly sizes.
$sheet=[Drawing.Bitmap]::new(1120,680,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics=[Drawing.Graphics]::FromImage($sheet)
$font=[Drawing.Font]::new('Microsoft YaHei',16)
$small=[Drawing.Font]::new('Microsoft YaHei',12)
try{
 $graphics.Clear([Drawing.Color]::FromArgb(255,48,51,50))
 $graphics.DrawString('死灵植入体 · 简洁块面 / 低饱和 / 深色描边',$font,[Drawing.Brushes]::White,20,16)
 $labels=@('盲目之视','凋亡之手','死灵副人格','阴影随行')
 for($i=0;$i -lt $artManifest.assets.Count;$i++){
  $asset=$artManifest.assets[$i];$col=$i%4;$row=[int][Math]::Floor($i/4)
  $bitmap=[Drawing.Bitmap]::new((Join-Path $artMod ('Textures/Necromancy/'+$asset.key+'.png')))
  try{$size=if($row -eq 0){256}else{128};$x=$col*280+(280-$size)/2;$y=if($row -eq 0){62}else{430};$graphics.DrawImage($bitmap,[int]$x,[int]$y,[int]$size,[int]$size)}finally{$bitmap.Dispose()}
  $label=if($row -eq 0){$labels[$col]+' · 物品'}else{@('眼部特征','臂部纹路','额头印记','足边阴影')[$col]}
  $graphics.DrawString($label,$small,[Drawing.Brushes]::Gainsboro,($col*280+65),$(if($row -eq 0){330}else{582}))
 }
 $sheet.Save((Join-Path $artMod 'ArtSource/Preview/ImplantStyle-1.6.12.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$graphics.Dispose();$sheet.Dispose();$font.Dispose();$small.Dispose()}
