# Builds the Android version (Release APK, signed with the debug key) into publish\PokeRogue-android.apk.
# Install it by copying the file to the phone, or with the phone plugged in (USB debugging on):
#   & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" install -r publish\PokeRogue-android.apk
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet publish "$root\src\PokeRogue.Android\PokeRogue.Android.csproj" -c Release -o "$root\publish\android"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$apk = Get-ChildItem "$root\publish\android\*-Signed.apk" | Select-Object -First 1
Copy-Item $apk.FullName "$root\publish\PokeRogue-android.apk" -Force
Write-Host ("APK: {0} ({1:N0} MB)" -f "$root\publish\PokeRogue-android.apk", ((Get-Item "$root\publish\PokeRogue-android.apk").Length / 1MB))
