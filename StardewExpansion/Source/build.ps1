param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen')
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'Use PowerShell 7 (pwsh).'}
$managedDirectory=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$releaseDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Release/Mods/PelicanCultivation'))
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name | ForEach-Object {
    [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($_.FullName),$null,$_.Name)
})
[Microsoft.CodeAnalysis.MetadataReference[]]$references=@(
    foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','UnityEngine.TextRenderingModule.dll','Unity.TextMeshPro.dll','0Harmony.dll')) {
        [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $name))
    }
)
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('PelicanCultivation',$trees,$references,$options)
$buildFile=Join-Path $releaseDirectory 'PelicanCultivation.dll.building'
$stream=[IO.File]::Create($buildFile)
try {$result=$compilation.Emit($stream)} finally {$stream.Dispose()}
if(-not $result.Success){$result.Diagnostics | Where-Object Severity -eq 'Error' | ForEach-Object {$_.ToString()};Remove-Item -LiteralPath $buildFile;throw 'Compilation failed.'}
Move-Item -LiteralPath $buildFile -Destination (Join-Path $releaseDirectory 'PelicanCultivation.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.xml') -Destination $releaseDirectory -Force
foreach($folder in @('Localization')) {
    $source=Join-Path $PSScriptRoot $folder
    if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination $releaseDirectory -Recurse -Force}
}
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Textures')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Textures') -Destination $releaseDirectory -Recurse -Force}
$textureDirectory=Join-Path $releaseDirectory 'Textures/Items'
New-Item -ItemType Directory -Path $textureDirectory -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Items') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $textureDirectory -Force}
$sourceTextureNames=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Items') -File | ForEach-Object {$_.Name})
Get-ChildItem -LiteralPath $textureDirectory -File | Where-Object {$_.Name -notin $sourceTextureNames} | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
$legacyItems=[IO.Path]::GetFullPath((Join-Path $releaseDirectory 'Items'))
if(-not $legacyItems.StartsWith($releaseDirectory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Legacy asset path escapes the release directory.'}
if(Test-Path -LiteralPath $legacyItems){Remove-Item -LiteralPath $legacyItems -Recurse -Force}
Write-Output ('Built: '+$releaseDirectory)
$result.Diagnostics | Where-Object Severity -eq 'Warning' | ForEach-Object {$_.ToString()} | Sort-Object -Unique
