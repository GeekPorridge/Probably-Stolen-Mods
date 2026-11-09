param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen')
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$modDll=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Release/Mods/StationScavenging/StationScavenging.dll'))
$output=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Verification'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
$tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CountBonusIntegration.cs')))
[Microsoft.CodeAnalysis.MetadataReference[]]$refs=@(foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','0Harmony.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managed $name))})
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('StationScavenging.CountBonusIntegration',[Microsoft.CodeAnalysis.SyntaxTree[]]@($tree),$refs,$options)
$dll=Join-Path $output 'StationScavenging.CountBonusIntegration.dll'
$stream=[IO.File]::Create($dll)
try{$result=$compilation.Emit($stream)}finally{$stream.Dispose()}
if(-not $result.Success){throw ($result.Diagnostics -join "`n")}
$config=@{runtimeOptions=@{tfm='net6.0';framework=@{name='Microsoft.NETCore.App';version='6.0.0'};configProperties=@{'System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization'=$true}}}
$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'StationScavenging.CountBonusIntegration.runtimeconfig.json') -Encoding utf8
& dotnet $dll $GameDir $modDll
if($LASTEXITCODE -ne 0){throw 'Native count-bonus integration failed'}
