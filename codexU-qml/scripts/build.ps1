param([string]$Config = "Release")
$ErrorActionPreference = "Stop"

$root   = Split-Path -Parent $PSScriptRoot
$build  = Join-Path $root "build"
New-Item -ItemType Directory -Force -Path $build | Out-Null

$QtDir     = "C:\Qt\6.11.1\mingw_64"
$CmakeExe  = "C:\Qt\Tools\CMake_64\bin\cmake.exe"
$GccExe    = "C:\Qt\Tools\mingw1310_64\bin\gcc.exe"
$GppExe    = "C:\Qt\Tools\mingw1310_64\bin\g++.exe"

Write-Host "Configuring..."
& $CmakeExe -S $root -B $build -G Ninja `
    "-DCMAKE_BUILD_TYPE=$Config" `
    "-DCMAKE_PREFIX_PATH=$QtDir" `
    "-DCMAKE_C_COMPILER=$GccExe" `
    "-DCMAKE_CXX_COMPILER=$GppExe"
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed" }

Write-Host "Building..."
& $CmakeExe --build $build --config $Config
if ($LASTEXITCODE -ne 0) { throw "CMake build failed" }

Write-Host "Deploying with windeployqt..."
$app = Join-Path $build "codexU-qml.exe"
& "$QtDir\bin\windeployqt.exe" $app --qmldir (Join-Path $root "src\qml")
if ($LASTEXITCODE -ne 0) { Write-Host "windeployqt warning (non-fatal)" }

Write-Host "Done: $app"
