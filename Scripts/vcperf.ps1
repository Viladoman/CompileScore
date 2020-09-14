<#
.SYNOPSIS
    VC Build analysis.
.DESCRIPTION
    VC Build analysis.
.EXAMPLE
    C:\PS>vcperf -start
    Build from msvc
    C:\PS>vcperf -stop -compilescore
.NOTES
    Author: ikrima
    Date:   September 11, 2020
#>

[CmdletBinding()]

param(
  #start vcperf trace capture
  [switch]$start,
  #stop vcperf trace capture
  [switch]$stop,
  #Runs CppBuildAnalyzer on vcperf trace
  [switch]$cppbldanalyze,
  #Runs CompileScorer on vcperf trace
  [switch]$compilescore,
  #Output directory for analysis files
  [ValidateScript({if ($_){  Test-Path $_}})]
  [string]$outdir = "$PSScriptRoot\..\..\.bin-int\vcperf",
  #Tools directory containing vcperf.exe, ScoreDataExtractor.exe, CppBuildAnalyzer.exe
  [ValidateScript({if ($_){  Test-Path $_}})]
  [string]$tooldir = "$PSScriptRoot\..\..\tools\vcperf",
  #Output directory for analysis files
  [string]$seshname = "tolva"
)

$ErrorActionPreference = "STOP"

Set-ExecutionPolicy Bypass -Scope Process -Force

$vcperfExe       = "$tooldir\vcperf.exe"
$compilescoreExe = "$tooldir\ScoreDataExtractor.exe"
$cppbldanalExe   = "$tooldir\CppBuildAnalyzer.exe"
$etltraceFile    = "$outdir\vcperf_$seshname.etl"
$cmplScoreFile   = "$outdir\compileData.scor"

if ($start) {
  Write-host "Starting vcperf trace capture" -ForegroundColor DarkGreen
  gsudo cache on
  gsudo $vcperfExe /start $seshname
  # Start-Process powershell @('-command', '&', $vcperfExe, '/start', $seshname, '*>', "$outdir\vcperfscriptlog.txt") -Wait -Verb RunAs
  # Get-Content "$outdir\vcperfscriptlog.txt"
}
elseif ($stop) {
  Write-host "Stopping vcperf trace capture" -ForegroundColor DarkGreen
  gsudo $vcperfExe /stopnoanalyze $seshname $etltraceFile
  # Start-Process powershell @('-command', '&', $vcperfExe, '/stopnoanalyze', $seshname, $etltraceFile, '*>', "$outdir\vcperfscriptlog.txt") -Wait -Verb RunAs
  # Get-Content "$outdir\vcperfscriptlog.txt"
}


Switch ($PSBoundParameters.GetEnumerator().
  Where({$_.Value -eq $true}).Key)
{
  'cppbldanalyze' {
    Write-host "Running CppBuildAnalyzer" -ForegroundColor DarkGreen
    Push-Location -Path $outdir
    & $cppbldanalExe @('-i', $etltraceFile, '--analyze_all')
    Pop-Location
  }
  'compilescore' {
    Write-host "Running CompileScorer" -ForegroundColor DarkGreen
    Push-Location -Path $outdir
    & $compilescoreExe @('-msvc', '-i', $etltraceFile, '-o', $cmplScoreFile, '-v', '2')
    Pop-Location
  }
}