$ErrorActionPreference='Stop'
# Preserve the accepted 1.2.1 text and build both languages from the same keys.
$entries=[ordered]@{}
foreach($file in @('localization-base.json','expansion-locales.json')){
    $data=Get-Content -LiteralPath (Join-Path $PSScriptRoot $file) -Raw | ConvertFrom-Json -AsHashtable
    foreach($key in $data.Keys){
        if($key -cnotmatch '^[a-z][a-z0-9_]*$' -or $entries.Contains($key)){throw ('Invalid or duplicate key: '+$key)}
        foreach($locale in @('zh','en')){if([string]::IsNullOrWhiteSpace($data[$key][$locale])){throw ('Missing '+$locale+' text: '+$key)}}
        $entries[$key]=$data[$key]
    }
}
foreach($locale in @('zh','en')){
    $lines=foreach($key in $entries.Keys){
        $value=$entries[$key][$locale].Replace("`r`n","`n").Replace("`r","`n").Replace("`n",'\n').Replace('"','""')
        # Native CSV has unquoted keys and decodes escaped newlines on load.
        $key+',"'+$value+'"'
    }
    [IO.File]::WriteAllLines((Join-Path $PSScriptRoot ('Localization/'+$locale+'.csv')),[string[]]$lines,[Text.UTF8Encoding]::new($false))
}
Write-Output ('Localized '+$entries.Count+' keys in zh and en.')
