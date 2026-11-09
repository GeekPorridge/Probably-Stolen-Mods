param([string]$GameDir = 'D:\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'Please run with PowerShell 7 (pwsh).'}
$managed=Join-Path $GameDir 'Probably Stolen_Data\Managed'
$release=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Release'))
$mod=Join-Path $release 'Mods\NightWalker'
New-Item -ItemType Directory -Path $mod -Force | Out-Null
foreach($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME $name)) | Out-Null}
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees=@(foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name){[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName),$null,$file.Name)})
[Microsoft.CodeAnalysis.MetadataReference[]]$refs=@(foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','0Harmony.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managed $name))})
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('NightWalker',$trees,$refs,$options)
$stream=[IO.File]::Create((Join-Path $mod 'NightWalker.dll'))
try{$result=$compilation.Emit($stream)}finally{$stream.Dispose()}
if(-not $result.Success){$result.Diagnostics | Where-Object Severity -eq 'Error' | ForEach-Object ToString;throw 'Compilation failed.'}
foreach($name in @('manifest.xml','Localization','Textures','preview.png')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $mod -Recurse -Force}
foreach($name in @('说明.txt','README.en.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $release -Force}
Write-Output "Built: $(Join-Path $mod 'NightWalker.dll')"
