param(
    [string]$JavaHome='C:\Program Files\Java\jdk-21',
    [string]$BuildDirectory=(Join-Path $PSScriptRoot 'java-build'),
    [switch]$CaptureSmoke
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $BuildDirectory | Out-Null
$BuildDirectory=(Resolve-Path -LiteralPath $BuildDirectory).Path
$android=Join-Path $PSScriptRoot '..\android\app'
& "$JavaHome\bin\javac.exe" -encoding UTF-8 -d $BuildDirectory "$android\AudioRing.java" "$android\Wire.java" "$android\MixBus.java" "$android\SourceGain.java" "$PSScriptRoot\WireProbe.java"
if($LASTEXITCODE -ne 0){throw 'JVM test compilation failed'}
& "$JavaHome\bin\java.exe" -cp $BuildDirectory WireProbe
if($LASTEXITCODE -ne 0){throw 'JVM checks failed'}
$testArgs=@($BuildDirectory,"$JavaHome\bin\java.exe")
if($CaptureSmoke){$testArgs+='--capture'}
dotnet run --project "$PSScriptRoot\AudioTests.csproj" -c Release -- @testArgs
if($LASTEXITCODE -ne 0){throw '.NET checks failed'}
