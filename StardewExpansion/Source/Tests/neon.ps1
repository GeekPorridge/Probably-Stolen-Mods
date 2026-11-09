param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen',[string]$PelicanDll='', [string]$NeonDir='', [switch]$ExpectBaselineFailure)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$peer=if($NeonDir){[IO.Path]::GetFullPath($NeonDir)}else{[IO.Path]::GetFullPath((Join-Path $GameDir '../../workshop/content/4348910/3833095213'))}
$modDll=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Release/Mods/PelicanCultivation/PelicanCultivation.dll'))
if($PelicanDll){$modDll=[IO.Path]::GetFullPath($PelicanDll)}
if(-not(Test-Path -LiteralPath (Join-Path $peer 'WaterSellerCollection.dll'))){throw 'Install Neon Undertow 0.5.3 before running this optional peer suite.'}
& (Join-Path $PSScriptRoot 'prepare-engine-fixture.ps1') -GameDir $GameDir
$out=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../verification'))
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
$tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'NeonIntegration.cs')))
[Microsoft.CodeAnalysis.MetadataReference[]]$references=@(
 foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','0Harmony.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managed $name))}
 [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($modDll)
 foreach($name in @('WaterSellerCollection.dll','WaterSellerBridge.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $peer $name))}
)
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('PelicanCultivation.NeonIntegration',[Microsoft.CodeAnalysis.SyntaxTree[]]@($tree),$references,$options)
$dll=Join-Path $out 'PelicanCultivation.NeonIntegration.dll';$stream=[IO.File]::Create($dll)
try{$result=$compilation.Emit($stream)}finally{$stream.Dispose()}
if(-not $result.Success){throw (($result.Diagnostics | Where-Object Severity -eq 'Error') -join "`n")}
$config=@{runtimeOptions=@{tfm='net6.0';framework=@{name='Microsoft.NETCore.App';version='6.0.0'};configProperties=@{'System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization'=$true}}}
$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'PelicanCultivation.NeonIntegration.runtimeconfig.json') -Encoding utf8
$runArgs=@($dll,$GameDir,$modDll,$peer);if($ExpectBaselineFailure){$runArgs+='expect-baseline-failure'}
& dotnet @runArgs
if($LASTEXITCODE -ne 0){throw 'Neon Undertow peer integration failed.'}
