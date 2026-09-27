# Builds the 1920x1080 Nexus / README images in docs/pics/nexus from the raw screenshots in docs/pics.
# Needs ImageMagick 7 (magick) on PATH. Usage: pwsh ./make_nexus_images.ps1
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$out = "nexus"
New-Item -ItemType Directory $out -Force | Out-Null
$font = "C:/Windows/Fonts/segoeuib.ttf"
$fontLight = "C:/Windows/Fonts/segoeui.ttf"

# Raw screenshots were taken on a 9:8 monitor; the game area sits between black bars at these offsets.
function Crop3x1($file, $top) { return @($file, "-crop", "2878x960+0+$top", "+repage") }

# A label in a translucent box in the bottom-right corner of a 1620x540 row (the HUD is top-left / top-right / bottom-left).
function LabelArgs($text, $size) {
    $w = [int]($text.Length * $size * 0.56 + 36); $h = [int]($size * 1.5)
    $x = 1620 - $w - 20; $y = 540 - $h - 18
    return @("(", "-size", "${w}x${h}", "xc:rgba(0,0,0,0.62)", ")", "-gravity", "northwest", "-geometry", "+$x+$y", "-composite",
             "-font", $font, "-pointsize", "$size", "-fill", "white", "-annotate", "+$($x + 18)+$($y + [int]($size * 0.18))", $text)
}

# ---- Cover: modded 3:1 gameplay with a title band -------------------------------------------------------------------
magick -size 1920x1080 xc:black `
    "(" (Crop3x1 "3_by_1_with_mod.png" 107) -resize 1920x640 ")" -gravity center -geometry +0+40 -composite `
    -font $font -pointsize 88 -fill white -gravity north -annotate +0+70 "Display & UI Tweaks" `
    -font $fontLight -pointsize 40 -fill "#d8c9a3" -gravity north -annotate +0+185 "UI size and any aspect ratio for No Rest for the Wicked" `
    -font $fontLight -pointsize 34 -fill "#bbbbbb" -gravity south -annotate +0+60 "Ultrawide  ·  16:10  ·  square  ·  Steam Deck" `
    -quality 92 "$out/cover.jpg"

# ---- Square screen before / after -----------------------------------------------------------------------------------
magick -size 1920x1080 xc:black `
    "(" "vanilla_16_by_9.png" -crop 2878x2490+0+0 +repage -resize 940x ")" -gravity northwest -geometry +13+150 -composite `
    "(" "modded_on_square.png" -crop 2878x2490+0+0 +repage -resize 940x ")" -gravity northwest -geometry +967+150 -composite `
    -font $font -pointsize 50 -fill white -gravity north -annotate -480+55 "Game" -annotate +480+55 "With the mod" `
    -font $fontLight -pointsize 32 -fill "#bbbbbb" -gravity south -annotate +0+30 "9:8 monitor: no letterbox, UI kept in a square box" `
    -quality 92 "$out/square_before_after.jpg"

# ---- Ultrawide: game's 16:9 box vs a custom box ---------------------------------------------------------------------
magick -size 1920x1080 xc:black `
    "(" (Crop3x1 "vanilla_uw_16_by_9_ar.png" 191) -resize 1620x540 (LabelArgs "Game: UI Aspect 16:9" 38) ")" -gravity north -geometry +0+0 -composite `
    "(" (Crop3x1 "modded_uw_lower_ar.png" 96) -resize 1620x540 (LabelArgs "Mod: Custom UI Aspect Ratio" 38) ")" -gravity north -geometry +0+540 -composite `
    -quality 92 "$out/ultrawide_game_vs_custom.jpg"

# ---- Ultrawide: two custom ratios -----------------------------------------------------------------------------------
magick -size 1920x1080 xc:black `
    "(" (Crop3x1 "uw_2_2_box_size.png" 170) -resize 1620x540 (LabelArgs "Custom UI Aspect Ratio 2.2" 38) ")" -gravity north -geometry +0+0 -composite `
    "(" (Crop3x1 "uw_1_1_box_size.png" 140) -resize 1620x540 (LabelArgs "Custom UI Aspect Ratio 1.1" 38) ")" -gravity north -geometry +0+540 -composite `
    -quality 92 "$out/ultrawide_custom_ratios.jpg"

# ---- Ultrawide menus (gallery) --------------------------------------------------------------------------------------
foreach ($m in @(@("ultrawide_with_mod_menu.png", 89, "ultrawide_menu.jpg"), @("ultrawide_with_mod_stats.png", 108, "ultrawide_stats.jpg"))) {
    magick -size 1920x1080 xc:black "(" (Crop3x1 $m[0] $m[1]) -resize 1920x640 ")" -gravity center -composite -quality 92 "$out/$($m[2])"
}

Get-ChildItem $out -Filter *.jpg | ForEach-Object { "{0,-32} {1,6:N0} KB" -f $_.Name, ($_.Length / 1KB) }
