param(
    [Parameter(Mandatory=$true)][string]$AndroidJar,
    [Parameter(Mandatory=$true)][string]$BuildTools,
    [string]$JavaHome = 'C:\Program Files\Java\jdk-21',
    [string]$BuildDirectory = (Join-Path $PSScriptRoot 'build'),
    [string]$OutputApk = (Join-Path $PSScriptRoot '..\..\SoundMesh-Android.apk')
)
$ErrorActionPreference = 'Stop'
$AndroidJar = (Resolve-Path -LiteralPath $AndroidJar).Path
$BuildTools = (Resolve-Path -LiteralPath $BuildTools).Path
$env:JAVA_HOME = $JavaHome
$env:PATH = "$JavaHome\bin;" + $env:PATH
New-Item -ItemType Directory -Force -Path $BuildDirectory,(Join-Path $BuildDirectory 'classes'),(Join-Path $BuildDirectory 'dex') | Out-Null
$BuildDirectory = (Resolve-Path -LiteralPath $BuildDirectory).Path
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Build command failed: $LASTEXITCODE" } }
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'app') -Filter '*.java' | ForEach-Object FullName
& "$JavaHome\bin\javac.exe" -encoding UTF-8 -source 8 -target 8 -Xlint:-options -cp "$AndroidJar;$PSScriptRoot\lib\zxing-core-3.5.3.jar" -d "$BuildDirectory\classes" @sources
Check-Exit
& "$JavaHome\bin\jar.exe" cf "$BuildDirectory\classes.jar" -C "$BuildDirectory\classes" .
Check-Exit
& "$BuildTools\d8.bat" --release --min-api 29 --lib $AndroidJar --output "$BuildDirectory\dex" "$BuildDirectory\classes.jar" "$PSScriptRoot\lib\zxing-core-3.5.3.jar"
Check-Exit
& "$BuildTools\aapt2.exe" link -o "$BuildDirectory\unsigned.apk" -I $AndroidJar --manifest "$PSScriptRoot\AndroidManifest.xml"
Check-Exit
& "$JavaHome\bin\jar.exe" uf "$BuildDirectory\unsigned.apk" -C "$BuildDirectory\dex" classes.dex
Check-Exit
& "$BuildTools\zipalign.exe" -f 4 "$BuildDirectory\unsigned.apk" "$BuildDirectory\aligned.apk"
Check-Exit
if (-not (Test-Path "$BuildDirectory\prototype.jks")) {
    & "$JavaHome\bin\keytool.exe" -genkeypair -keystore "$BuildDirectory\prototype.jks" -alias prototype -storepass android -keypass android -keyalg RSA -keysize 2048 -validity 3650 -dname 'CN=SoundMesh Prototype'
    Check-Exit
}
& "$BuildTools\apksigner.bat" sign --ks "$BuildDirectory\prototype.jks" --ks-key-alias prototype --ks-pass pass:android --key-pass pass:android --out $OutputApk "$BuildDirectory\aligned.apk"
Check-Exit
& "$BuildTools\apksigner.bat" verify --verbose $OutputApk
Check-Exit
Write-Output "Built $OutputApk"
