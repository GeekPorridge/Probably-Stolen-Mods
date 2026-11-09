param([string]$GameDir = 'D:\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference = 'Stop'
$managedDirectory = Join-Path $GameDir 'Probably Stolen_Data/Managed'
$releaseDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Release/Mods/MetalWineBottles'))
$dllPath = Join-Path $releaseDirectory 'MetalWineBottles.dll'
foreach($name in @('UnityEngine.CoreModule.dll','0Harmony.dll','DseCecil.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $managedDirectory $name)) | Out-Null
}
[Reflection.Assembly]::LoadFrom($dllPath) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.SyntaxTree[]]$testTrees = @([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BottleVerification.cs'))))
[Microsoft.CodeAnalysis.MetadataReference[]]$testReferences = @(
    foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll')) {
        [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $name))
    }
    [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($dllPath)
)
$testOptions = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
$testCompilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('MetalWineBottles.Verification', $testTrees, $testReferences, $testOptions)
$testStream = [IO.MemoryStream]::new()
try {
    $testResult = $testCompilation.Emit($testStream)
    if(-not $testResult.Success){$testResult.Diagnostics | ForEach-Object {$_.ToString()};throw 'Verification compilation failed.'}
    [Reflection.Assembly]::Load($testStream.ToArray()) | Out-Null
} finally {$testStream.Dispose()}
Write-Output ([BottleVerification]::Run())
$allowedReferences = [string[]]@(Get-ChildItem -LiteralPath $managedDirectory -Filter '*.dll' -File | ForEach-Object {$_.BaseName})
$scanResult = [ModScanner]::Scan([string[]]@($dllPath), $allowedReferences, $releaseDirectory, $true)
if($scanResult.Refused -or $scanResult.Error -or $scanResult.Capabilities.Count -gt 0) {
    $scanResult | Format-List *
    throw 'Native mod scanner did not pass with empty capabilities.'
}
Write-Output "Native mod scanner passed: $($scanResult.MethodsScanned) methods, no extra capabilities."
$patchMethod = [HarmonyLib.AccessTools]::Method([MaterialDirectory], 'MetalIngot')
if($null -eq $patchMethod -or $patchMethod.ReturnType -ne [GameItem]) {throw 'Native metal ingot factory signature changed.'}
$nativeIl = $patchMethod.GetMethodBody().GetILAsByteArray()
if($nativeIl.Length -eq 0){throw 'Native metal ingot factory has no method body.'}
$releaseAssembly = [DseCecil.AssemblyDefinition]::ReadAssembly($dllPath)
try {
    $forbiddenReferences = @($releaseAssembly.MainModule.GetMemberReferences() | Where-Object {$_.DeclaringType.FullName -in @('MachineFurnace','MachineHelper','MachineryHelper','PowerHelper')})
    if($forbiddenReferences.Count -ne 0){throw 'Release still contains furnace or power calls.'}
    $patchAttributes = @($releaseAssembly.MainModule.GetType('MetalWineBottles.IngotFactoryPatch').CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'})
    if($patchAttributes.Count -ne 1 -or $patchAttributes[0].ConstructorArguments[0].Value.FullName -ne 'MaterialDirectory' -or $patchAttributes[0].ConstructorArguments[1].Value -ne 'MetalIngot'){throw 'Unexpected patch target.'}
} finally {$releaseAssembly.Dispose()}
$manifest = [xml](Get-Content -LiteralPath (Join-Path $releaseDirectory 'manifest.xml') -Raw)
if($manifest.Manifest.ID -ne 'metal_wine_bottles' -or $manifest.Manifest.ModVersion -ne '0.2.1'){throw 'Unexpected manifest identity.'}
if(@(Get-ChildItem -LiteralPath $releaseDirectory -Filter '*.dll' -File).Count -ne 1){throw 'Unexpected bundled DLL.'}
$english = @(Import-Csv -LiteralPath (Join-Path $releaseDirectory 'Localization/en.csv'))
$chinese = @(Import-Csv -LiteralPath (Join-Path $releaseDirectory 'Localization/zh.csv'))
if((Compare-Object ($english.Key | Sort-Object) ($chinese.Key | Sort-Object))){throw 'Localization keys differ.'}
if($english.Count -ne @($english.Key | Sort-Object -Unique).Count){throw 'Duplicate localization keys.'}
foreach($row in $english){
    if([string]::IsNullOrWhiteSpace($row.Value) -or $row.Value -match '[\p{IsCJKUnifiedIdeographs}]'){throw ('Invalid English translation: '+$row.Key)}
    $zhRow=$chinese | Where-Object {$_.Key -eq $row.Key}
    $englishArgs=@([regex]::Matches($row.Value,'\{\d+\}') | ForEach-Object {$_.Value})
    $chineseArgs=@([regex]::Matches($zhRow.Value,'\{\d+\}') | ForEach-Object {$_.Value})
    if(($englishArgs -join ',') -ne ($chineseArgs -join ',')){throw ('Localization placeholders differ: '+$row.Key)}
    [string]::Format($row.Value,[object[]]@('1 / 3 / 5 / 7 / 9 / 10')) | Out-Null
}
$runtimeFiles=@(Get-ChildItem -LiteralPath $releaseDirectory -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($releaseDirectory,$_.FullName).Replace('\','/')} | Sort-Object)
$expectedRuntimeFiles=@('Localization/en.csv','Localization/zh.csv','MetalWineBottles.dll','manifest.xml') | Sort-Object
if(Compare-Object $runtimeFiles $expectedRuntimeFiles){throw 'Unexpected files in the minimal runtime.'}
Write-Output 'Manifest, ingot factory patch, absence of furnace/power calls, release DLL list and bilingual localization passed.'
Write-Output 'All English strings are nonempty; language placeholders and the four-file minimal runtime passed.'
