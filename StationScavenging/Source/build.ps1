param(
    [string]$GameDir = 'D:\steam\steamapps\common\Probably Stolen'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Please run this script with PowerShell 7 (pwsh).' }
$managedDirectory = Join-Path $GameDir 'Probably Stolen_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managedDirectory 'Assembly-CSharp.dll'))) { throw "Game assembly not found: $managedDirectory" }
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Release'))
$modDirectory = Join-Path $releaseRoot 'Mods\StationScavenging'
New-Item -ItemType Directory -Path $modDirectory -Force | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees = @(
    foreach ($source in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name) {
        [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($source.FullName), $null, $source.Name)
    }
)
[Microsoft.CodeAnalysis.MetadataReference[]]$references = @(
    foreach ($name in @('mscorlib.dll', 'netstandard.dll', 'System.dll', 'System.Core.dll',
        'Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.UI.dll',
        'Unity.TextMeshPro.dll', 'UnityEngine.UIModule.dll', 'UnityEngine.TextRenderingModule.dll')) {
        [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $name))
    }
)
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
    [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary
).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('StationScavenging', $trees, $references, $options)
$stream = [IO.File]::Create((Join-Path $modDirectory 'StationScavenging.dll'))
try { $result = $compilation.Emit($stream) } finally { $stream.Dispose() }
if (-not $result.Success) {
    $result.Diagnostics | Where-Object Severity -eq 'Error' | ForEach-Object { Write-Output $_.ToString() }
    throw 'Mod compilation failed.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.xml') -Destination $modDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Localization') -Destination $modDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Textures') -Destination $modDirectory -Recurse -Force
$preview = Join-Path $PSScriptRoot 'preview.png'
if (Test-Path -LiteralPath $preview) { Copy-Item -LiteralPath $preview -Destination $modDirectory -Force }
foreach ($docName in @('说明.txt', 'README.en.txt', 'ART-PROMPTS.json')) {
    $docPath = Join-Path $PSScriptRoot $docName
    if (Test-Path -LiteralPath $docPath) { Copy-Item -LiteralPath $docPath -Destination $releaseRoot -Force }
}
Write-Output "Built: $(Join-Path $modDirectory 'StationScavenging.dll')"
Write-Output "DLL bytes: $((Get-Item -LiteralPath (Join-Path $modDirectory 'StationScavenging.dll')).Length)"
