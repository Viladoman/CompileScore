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
  #Runs CompileScorer on vcperf trace
  [switch]$compilescore,
  #Output directory for analysis files
  [ValidateScript({if ($_){  Test-Path $_}})]
  [string]$outdir = "$PSScriptRoot",
  #Tools directory containing vcperf.exe, ScoreDataExtractor.exe, CppBuildAnalyzer.exe
  [ValidateScript({if ($_){  Test-Path $_}})]
  [string]$tooldir = "$PSScriptRoot",
  #Output directory for analysis files
  [string]$seshname = "compilescore"
)

$ErrorActionPreference = "STOP"

Set-ExecutionPolicy Bypass -Scope Process -Force

$vcperfExe       = "$tooldir\vcperf.exe"
$compilescoreExe = "$tooldir\ScoreDataExtractor.exe"
$etltraceFile    = "$outdir\vcperf_$seshname.etl"
$cmplScoreFile   = "$outdir\compileData.scor"

if ($start) {
  Write-host "Starting vcperf trace capture" -ForegroundColor DarkGreen
  Start-Process powershell @('-command', '&', $vcperfExe, '/start', $seshname, '*>', "$outdir\vcperfscriptlog.txt") -Wait -Verb RunAs
  Get-Content "$outdir\vcperfscriptlog.txt"
}
elseif ($stop) {
  Write-host "Stopping vcperf trace capture" -ForegroundColor DarkGreen
  Start-Process powershell @('-command', '&', $vcperfExe, '/stopnoanalyze', $seshname, $etltraceFile, '*>', "$outdir\vcperfscriptlog.txt") -Wait -Verb RunAs
  Get-Content "$outdir\vcperfscriptlog.txt"
}


Switch ($PSBoundParameters.GetEnumerator().
  Where({$_.Value -eq $true}).Key)
{
  'compilescore' {
    Write-host "Running CompileScorer" -ForegroundColor DarkGreen
    Push-Location -Path $outdir
    & $compilescoreExe @('-msvc', '-i', $etltraceFile, '-o', $cmplScoreFile, '-v', '2')
    Pop-Location
  }
}