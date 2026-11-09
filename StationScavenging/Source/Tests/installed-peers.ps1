param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen',[string]$StationDll='', [switch]$ReentryOnly)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$modDll=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Release/Mods/StationScavenging/StationScavenging.dll'))
if($StationDll){$modDll=[IO.Path]::GetFullPath($StationDll)}
$output=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Verification'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
& (Join-Path $PSScriptRoot 'prepare-engine-fixture.ps1') -GameDir $GameDir
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
$tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'InstalledPeerIntegration.cs')))
[Microsoft.CodeAnalysis.MetadataReference[]]$refs=@(foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','UnityEngine.AudioModule.dll','0Harmony.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managed $name))})
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('StationScavenging.InstalledPeerIntegration',[Microsoft.CodeAnalysis.SyntaxTree[]]@($tree),$refs,$options)
$dll=Join-Path $output 'StationScavenging.InstalledPeerIntegration.dll'
$stream=[IO.File]::Create($dll)
try{$result=$compilation.Emit($stream)}finally{$stream.Dispose()}
if(-not $result.Success){throw (($result.Diagnostics | Where-Object Severity -eq 'Error') -join "`n")}
$config=@{runtimeOptions=@{tfm='net6.0';framework=@{name='Microsoft.NETCore.App';version='6.0.0'};configProperties=@{'System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization'=$true}}}
$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'StationScavenging.InstalledPeerIntegration.runtimeconfig.json') -Encoding utf8
$arguments=@($dll,$GameDir,$modDll,(Join-Path $GameDir 'Mods/3832421605/BetterScavenging.dll'),(Join-Path $GameDir 'Mods/3832768562/ScavengerSkills.dll'))
if($ReentryOnly){$arguments+='reentry-only'}
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'Installed third-party DLL integration failed'}
