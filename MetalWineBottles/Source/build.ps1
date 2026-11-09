param([string]$GameDir = 'D:\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7 (pwsh).' }
$managedDirectory = Join-Path $GameDir 'Probably Stolen_Data/Managed'
$modDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Release/Mods/MetalWineBottles'))
New-Item -ItemType Directory -Path $modDirectory -Force | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name | ForEach-Object {
    [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($_.FullName), $null, $_.Name)
})
[Microsoft.CodeAnalysis.MetadataReference[]]$references = @(
    foreach ($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','0Harmony.dll')) {
        [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $name))
    }
)
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('MetalWineBottles', $trees, $references, $options)
$stream = [IO.File]::Create((Join-Path $modDirectory 'MetalWineBottles.dll'))
try { $result = $compilation.Emit($stream) } finally { $stream.Dispose() }
if (-not $result.Success) {
    $result.Diagnostics | Where-Object Severity -eq 'Error' | ForEach-Object { $_.ToString() }
    throw 'Compilation failed.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.xml') -Destination $modDirectory -Force
$localizationDirectory = Join-Path $modDirectory 'Localization'
New-Item -ItemType Directory -Path $localizationDirectory -Force | Out-Null
foreach($locale in @('en','zh')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Localization/' + $locale + '.csv')) -Destination $localizationDirectory -Force
}
# Documents live beside the distributable, not inside the subscribed content.
$legacyReadme = Join-Path $modDirectory '说明.txt'
if(Test-Path -LiteralPath $legacyReadme -PathType Leaf) { Remove-Item -LiteralPath $legacyReadme }
Write-Output "Built: $modDirectory"
