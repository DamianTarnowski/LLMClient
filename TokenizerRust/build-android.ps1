# Android NDK cross-compilation build script for TokenizerRust
# Requires NDK r27c

# Change to script directory
Set-Location $PSScriptRoot

$NDK_PATH = "C:\Program Files (x86)\Android\AndroidNDK\android-ndk-r27c"
$TOOLCHAIN = "$NDK_PATH\toolchains\llvm\prebuilt\windows-x86_64\bin"

# Build for ARM64 (aarch64)
Write-Host "Building for aarch64-linux-android (arm64-v8a)..." -ForegroundColor Green
$env:CC_aarch64_linux_android = "$TOOLCHAIN\aarch64-linux-android21-clang.cmd"
$env:CXX_aarch64_linux_android = "$TOOLCHAIN\aarch64-linux-android21-clang++.cmd"
$env:AR_aarch64_linux_android = "$TOOLCHAIN\llvm-ar.exe"
$env:TARGET_CC = "$TOOLCHAIN\aarch64-linux-android21-clang.cmd"
$env:TARGET_CXX = "$TOOLCHAIN\aarch64-linux-android21-clang++.cmd"
$env:TARGET_AR = "$TOOLCHAIN\llvm-ar.exe"

cargo build --release --target aarch64-linux-android
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build for aarch64-linux-android" -ForegroundColor Red
    exit 1
}

# Build for ARM32 (armv7)
Write-Host "Building for armv7-linux-androideabi (armeabi-v7a)..." -ForegroundColor Green
$env:CC_armv7_linux_androideabi = "$TOOLCHAIN\armv7a-linux-androideabi21-clang.cmd"
$env:CXX_armv7_linux_androideabi = "$TOOLCHAIN\armv7a-linux-androideabi21-clang++.cmd"
$env:AR_armv7_linux_androideabi = "$TOOLCHAIN\llvm-ar.exe"
$env:TARGET_CC = "$TOOLCHAIN\armv7a-linux-androideabi21-clang.cmd"
$env:TARGET_CXX = "$TOOLCHAIN\armv7a-linux-androideabi21-clang++.cmd"
$env:TARGET_AR = "$TOOLCHAIN\llvm-ar.exe"

cargo build --release --target armv7-linux-androideabi
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build for armv7-linux-androideabi" -ForegroundColor Red
    exit 1
}

# Build for x86_64
Write-Host "Building for x86_64-linux-android..." -ForegroundColor Green
$env:CC_x86_64_linux_android = "$TOOLCHAIN\x86_64-linux-android21-clang.cmd"
$env:CXX_x86_64_linux_android = "$TOOLCHAIN\x86_64-linux-android21-clang++.cmd"
$env:AR_x86_64_linux_android = "$TOOLCHAIN\llvm-ar.exe"
$env:TARGET_CC = "$TOOLCHAIN\x86_64-linux-android21-clang.cmd"
$env:TARGET_CXX = "$TOOLCHAIN\x86_64-linux-android21-clang++.cmd"
$env:TARGET_AR = "$TOOLCHAIN\llvm-ar.exe"

cargo build --release --target x86_64-linux-android
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build for x86_64-linux-android" -ForegroundColor Red
    exit 1
}

# Build for x86
Write-Host "Building for i686-linux-android (x86)..." -ForegroundColor Green
$env:CC_i686_linux_android = "$TOOLCHAIN\i686-linux-android21-clang.cmd"
$env:CXX_i686_linux_android = "$TOOLCHAIN\i686-linux-android21-clang++.cmd"
$env:AR_i686_linux_android = "$TOOLCHAIN\llvm-ar.exe"
$env:TARGET_CC = "$TOOLCHAIN\i686-linux-android21-clang.cmd"
$env:TARGET_CXX = "$TOOLCHAIN\i686-linux-android21-clang++.cmd"
$env:TARGET_AR = "$TOOLCHAIN\llvm-ar.exe"

cargo build --release --target i686-linux-android
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build for i686-linux-android" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All Android builds completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Output libraries:"
Write-Host "  target\aarch64-linux-android\release\libtokenizer_rust.so (arm64-v8a)"
Write-Host "  target\armv7-linux-androideabi\release\libtokenizer_rust.so (armeabi-v7a)"
Write-Host "  target\x86_64-linux-android\release\libtokenizer_rust.so (x86_64)"
Write-Host "  target\i686-linux-android\release\libtokenizer_rust.so (x86)"
