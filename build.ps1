# Builds StandingStonesOfValheim.dll with the .NET Framework C# compiler against the game's own assemblies,
# checks the translation files, then packs it with package/ into dist/StandingStonesOfValheim-<version>.zip for Thunderstore.
# The old compiler can't handle long paths, so it compiles from a short temp folder.
param(
    [string]$Managed = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed",
    [string]$BepInExCore = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\core",
    [string]$Jotunn = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll"
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ---- translations: every language has the same keys as English, nothing empty, the same placeholders,
# tags and line breaks, UTF-8 without BOM; every key the code uses is in the English file.
$langRoot = Join-Path $root "package\Translations"
function Read-Translation([string]$file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$file has a BOM" }
    $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $map = @{}
    foreach ($p in $json.PSObject.Properties) { $map[$p.Name] = [string]$p.Value }
    return $map
}
function Marks([string]$text) {
    $found = [regex]::Matches($text, '\{\d+\}|<[^>]+>|\$[A-Za-z_][A-Za-z0-9_]*|\n') | ForEach-Object { $_.Value }
    return (($found | Sort-Object) -join ' ')
}
$english = Read-Translation (Join-Path $langRoot "English\StandingStonesOfValheim.json")
$problems = @()
foreach ($dir in Get-ChildItem $langRoot -Directory) {
    if ($dir.Name -eq "English") { continue }
    $other = Read-Translation (Join-Path $dir.FullName "StandingStonesOfValheim.json")
    foreach ($key in $english.Keys) {
        if (-not $other.ContainsKey($key)) { $problems += "$($dir.Name): missing $key"; continue }
        if ([string]::IsNullOrWhiteSpace($other[$key])) { $problems += "$($dir.Name): empty $key" }
        if ((Marks $english[$key]) -ne (Marks $other[$key])) { $problems += "$($dir.Name): placeholders differ in $key" }
    }
    foreach ($key in $other.Keys) { if (-not $english.ContainsKey($key)) { $problems += "$($dir.Name): $key is not in English" } }
}
foreach ($key in $english.Keys) { if ([string]::IsNullOrWhiteSpace($english[$key])) { $problems += "English: empty $key" } }
$used = @("ssov_kind_path", "ssov_kind_always", "ssov_kind_cost", "ssov_kind_daily", "ssov_far_0", "ssov_far_1", "ssov_far_2", "ssov_altar_desc",
    "ssov_read", "ssov_choose", "ssov_is_chosen", "ssov_altar_open") + (@("n", "ne", "e", "se", "s", "sw", "w", "nw") | ForEach-Object { "ssov_dir_" + $_ })
foreach ($file in Get-ChildItem (Join-Path $root "src") -Filter *.cs) {
    $code = [IO.File]::ReadAllText($file.FullName)
    $used += [regex]::Matches($code, '"\$?(ssov_[a-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notin @("ssov_info", "ssov_goto", "ssov_reset", "ssov_findall") -and -not $_.EndsWith("_") }
    # Every stone has a name, its god's name (the HUD icon), a lore line and an effect text.
    foreach ($m in [regex]::Matches($code, 'new Stone\("([A-Za-z]+)"')) {
        $id = "ssov_" + $m.Groups[1].Value.ToLowerInvariant()
        $used += @($id, ($id + "_god"), ($id + "_lore"), ($id + "_effect"))
    }
}
foreach ($key in ($used | Sort-Object -Unique)) { if (-not $english.ContainsKey($key)) { $problems += "English: the code uses $key, which isn't translated" } }
if ($problems.Count -gt 0) { $problems | ForEach-Object { Write-Output $_ }; throw "Translation check failed" }
Write-Output "Translations OK: $($english.Count) keys"

# Thunderstore refuses a package whose README (or other text file) starts with a UTF-8 BOM.
foreach ($file in Get-ChildItem (Join-Path $root "package") -Recurse -File -Include *.md, *.json) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$($file.FullName) starts with a UTF-8 BOM; save it without one" }
}

# ---- build ----
$tmp = Join-Path $env:TEMP "vhm\build_ssov"
New-Item -ItemType Directory -Force $tmp | Out-Null
Remove-Item "$tmp\*" -Recurse -Force
Copy-Item (Join-Path $root "src\*.cs") $tmp -Force

$refs = @(
    "$Managed\mscorlib.dll", "$Managed\netstandard.dll", "$Managed\System.dll", "$Managed\System.Core.dll",
    "$Managed\UnityEngine.dll", "$Managed\UnityEngine.CoreModule.dll", "$Managed\assembly_valheim.dll", "$Managed\assembly_utils.dll",
    "$BepInExCore\BepInEx.dll", "$BepInExCore\0Harmony.dll", $Jotunn,
    "$Managed\UnityEngine.PhysicsModule.dll", "$Managed\UnityEngine.ParticleSystemModule.dll", "$Managed\SoftReferenceableAssets.dll",
    "$Managed\assembly_guiutils.dll", "$Managed\UnityEngine.UI.dll", "$Managed\UnityEngine.AnimationModule.dll", "$Managed\UnityEngine.AudioModule.dll",
    "$Managed\UnityEngine.TextRenderingModule.dll", "$Managed\UnityEngine.UIModule.dll", "$Managed\UnityEngine.InputLegacyModule.dll",
    "$Managed\UnityEngine.AssetBundleModule.dll", "$Managed\Splatform.dll", "$Managed\Unity.TextMeshPro.dll"
) | ForEach-Object { "/r:$_" }

# The translations go into the dll too (Texts.LoadOwn), so the texts are there however the mod is installed.
$resources = Get-ChildItem $langRoot -Directory | ForEach-Object { "/resource:" + (Join-Path $_.FullName "StandingStonesOfValheim.json") + ",SSoV.Translations." + $_.Name + ".json" }

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nostdlib /noconfig /target:library /optimize+ `
    /codepage:65001 $refs $resources /out:"$tmp\StandingStonesOfValheim.dll" "$tmp\*.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root "bin"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item "$tmp\StandingStonesOfValheim.dll" $out -Force
Write-Output "Built: $out\StandingStonesOfValheim.dll"

# ---- package ----
# Zipped by hand: Compress-Archive in Windows PowerShell writes backslashes into the paths of files in
# folders (Translations\English\...), which other tools don't read as folders.
$version = (Get-Content (Join-Path $root "package\manifest.json") -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $tmp "package"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root "package\*") $stage -Recurse -Force
Copy-Item "$out\StandingStonesOfValheim.dll" $stage -Force
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "StandingStonesOfValheim-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Output "Packaged: $zip"
