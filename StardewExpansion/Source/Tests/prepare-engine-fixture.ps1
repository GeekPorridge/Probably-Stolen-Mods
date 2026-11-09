param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen')
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Probably Stolen_Data/Managed'
[Reflection.Assembly]::LoadFrom((Join-Path $managed 'DseCecil.dll')) | Out-Null
$destination=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../verification/EngineFixture'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$assembly=[DseCecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'UnityEngine.CoreModule.dll'))
$module=$assembly.MainModule
function Stub-EngineCalls($engineModule){
  function Walk($type){$type; foreach($nested in $type.NestedTypes){Walk $nested}}
  foreach($type in $engineModule.Types | ForEach-Object {Walk $_}){
    foreach($method in $type.Methods | Where-Object IsInternalCall){
      $method.ImplAttributes=[DseCecil.MethodImplAttributes]0
      $method.Body=[DseCecil.Cil.MethodBody]::new($method)
      $method.Body.InitLocals=$true
      $il=$method.Body.GetILProcessor()
      if($method.ReturnType.FullName -ne 'System.Void'){
        $variable=[DseCecil.Cil.VariableDefinition]::new($method.ReturnType)
        $method.Body.Variables.Add($variable)
        $il.Emit([DseCecil.Cil.OpCodes]::Ldloc,$variable)
      }
      $il.Emit([DseCecil.Cil.OpCodes]::Ret)
    }
  }
}
Stub-EngineCalls $module
function Add-Field($type,[string]$name,$fieldType){
  $field=[DseCecil.FieldDefinition]::new($name,[DseCecil.FieldAttributes]'Public,Static',$fieldType)
  $type.Fields.Add($field)
  return $field
}
function Body($method){
  $method.ImplAttributes=[DseCecil.MethodImplAttributes]0
  $method.Body=[DseCecil.Cil.MethodBody]::new($method)
  return $method.Body.GetILProcessor()
}
$random=$module.GetType('UnityEngine.Random')
$samples=Add-Field $random 'CodexSamples' ([DseCecil.ArrayType]::new($module.TypeSystem.Single))
$index=Add-Field $random 'CodexSampleIndex' $module.TypeSystem.Int32
$range=Add-Field $random 'CodexRangePercent' $module.TypeSystem.Single
$method=@($random.Methods | Where-Object Name -eq 'get_value')[0]
$il=Body $method
$fallback=[DseCecil.Cil.Instruction]::Create([DseCecil.Cil.OpCodes]::Ldc_R4,[single]0.5)
$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$samples)
$il.Emit([DseCecil.Cil.OpCodes]::Brfalse,$fallback)
$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$samples)
$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$index)
$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$samples)
$il.Emit([DseCecil.Cil.OpCodes]::Ldlen)
$il.Emit([DseCecil.Cil.OpCodes]::Conv_I4)
$il.Emit([DseCecil.Cil.OpCodes]::Rem)
$il.Emit([DseCecil.Cil.OpCodes]::Ldelem_R4)
$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$index)
$il.Emit([DseCecil.Cil.OpCodes]::Ldc_I4_1)
$il.Emit([DseCecil.Cil.OpCodes]::Add)
$il.Emit([DseCecil.Cil.OpCodes]::Stsfld,$index)
$il.Emit([DseCecil.Cil.OpCodes]::Ret)
$il.Append($fallback)
$il.Emit([DseCecil.Cil.OpCodes]::Ret)
foreach($method in $random.Methods | Where-Object {($_.Name -eq 'Range' -and $_.ReturnType.FullName -eq 'System.Single') -or $_.Name -eq 'RandomRangeInt'}){
  $il=Body $method
  if($method.ReturnType.FullName -eq 'System.Single'){$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$range)}else{$il.Emit([DseCecil.Cil.OpCodes]::Ldarg_0)}
  $il.Emit([DseCecil.Cil.OpCodes]::Ret)
}
$time=$module.GetType('UnityEngine.Time')
$frame=Add-Field $time 'CodexFrame' $module.TypeSystem.Int32
$clock=Add-Field $time 'CodexTime' $module.TypeSystem.Single
foreach($name in @('get_frameCount','get_unscaledTime','get_timeScale')){
  $method=@($time.Methods | Where-Object Name -eq $name)[0]
  $il=Body $method
  if($name -eq 'get_frameCount'){$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$frame)}
  elseif($name -eq 'get_unscaledTime'){$il.Emit([DseCecil.Cil.OpCodes]::Ldsfld,$clock)}
  else{$il.Emit([DseCecil.Cil.OpCodes]::Ldc_R4,[single]1)}
  $il.Emit([DseCecil.Cil.OpCodes]::Ret)
}
$assembly.Write((Join-Path $destination 'UnityEngine.CoreModule.dll'))
$assembly.Dispose()
$audio=[DseCecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'UnityEngine.AudioModule.dll'))
Stub-EngineCalls $audio.MainModule
$audio.Write((Join-Path $destination 'UnityEngine.AudioModule.dll'))
$audio.Dispose()
Write-Output 'Created isolated managed RNG/time boundary fixture; installed game files are untouched.'
