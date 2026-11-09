param([string]$ReportPath='')
$ErrorActionPreference='Stop'
Add-Type -Path @((Join-Path $PSScriptRoot 'TransitPresentationHarness.cs'),(Join-Path $PSScriptRoot '..\StationTransitUI.cs'))
$report=[TransitHarness.Runner]::Run()
if($ReportPath){$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
$report | Format-List
