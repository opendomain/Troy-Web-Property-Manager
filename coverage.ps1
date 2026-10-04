<#
.SYNOPSIS
    Runs the tests with code coverage and builds the coverage report in "Docs/Code Coverage".

.DESCRIPTION
    Runs both test projects (or just the unit tests) with the settings in coverage.runsettings, which measure only the
    app's own hand-written code. The raw Cobertura files go to TestResults/Coverage (ignored by git); ReportGenerator
    turns them into an HTML report (index.html) and a Markdown summary (SummaryGithub.md) in "Docs/Code Coverage".
    See "Code coverage" in README.md.

.EXAMPLE
    ./coverage.ps1
    Everything, UI tests included (about 15 minutes; needs Chrome and LocalDB like the UI tests do).

.EXAMPLE
    ./coverage.ps1 -UnitTestsOnly -Open
    Just the unit tests (under a minute), then opens the report.
#>
[CmdletBinding()]
param(
    # Skip the UI tests. The report then only shows what the unit tests cover.
    [switch]$UnitTestsOnly,

    # Open the HTML report in the browser when it's done.
    [switch]$Open
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$results = Join-Path $PSScriptRoot 'TestResults/Coverage'
$report = Join-Path $PSScriptRoot 'Docs/Code Coverage'
$target = if ($UnitTestsOnly) { 'Tests/Troy Web Property Manager.Tests' } else { 'Troy Web Property Manager.slnx' }

if (Test-Path $results) { Remove-Item $results -Recurse -Force }

dotnet test $target --settings coverage.runsettings --collect 'Code Coverage;Format=cobertura' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw "The tests failed (exit code $LASTEXITCODE), so no report was generated." }

# ReportGenerator is a local tool (dotnet-tools.json), so this installs the pinned version on first use.
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Couldn't restore ReportGenerator (dotnet tool restore)." }

if (Test-Path $report) { Remove-Item $report -Recurse -Force }
dotnet reportgenerator "-reports:$results/**/*.cobertura.xml" "-targetdir:$report" '-reporttypes:Html;MarkdownSummaryGithub' '-title:Troy Web Property Manager'
if ($LASTEXITCODE -ne 0) { throw "ReportGenerator failed (exit code $LASTEXITCODE)." }

Write-Host "Coverage report: $(Join-Path $report 'index.html')"
if ($Open) { Start-Process (Join-Path $report 'index.html') }
