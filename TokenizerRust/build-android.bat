@echo off
REM Android NDK cross-compilation build script for TokenizerRust
REM Requires NDK r27c

SET NDK_PATH=C:\Program Files (x86)\Android\AndroidNDK\android-ndk-r27c
SET TOOLCHAIN=%NDK_PATH%\toolchains\llvm\prebuilt\windows-x86_64\bin

REM Build for ARM64 (aarch64)
echo Building for aarch64-linux-android (arm64-v8a)...
SET CC=%TOOLCHAIN%\aarch64-linux-android21-clang.cmd
SET CXX=%TOOLCHAIN%\aarch64-linux-android21-clang++.cmd
SET AR=%TOOLCHAIN%\llvm-ar.exe
cargo build --release --target aarch64-linux-android
if %ERRORLEVEL% NEQ 0 (
    echo Failed to build for aarch64-linux-android
    exit /b 1
)

REM Build for ARM32 (armv7)
echo Building for armv7-linux-androideabi (armeabi-v7a)...
SET CC=%TOOLCHAIN%\armv7a-linux-androideabi21-clang.cmd
SET CXX=%TOOLCHAIN%\armv7a-linux-androideabi21-clang++.cmd
cargo build --release --target armv7-linux-androideabi
if %ERRORLEVEL% NEQ 0 (
    echo Failed to build for armv7-linux-androideabi
    exit /b 1
)

REM Build for x86_64
echo Building for x86_64-linux-android...
SET CC=%TOOLCHAIN%\x86_64-linux-android21-clang.cmd
SET CXX=%TOOLCHAIN%\x86_64-linux-android21-clang++.cmd
cargo build --release --target x86_64-linux-android
if %ERRORLEVEL% NEQ 0 (
    echo Failed to build for x86_64-linux-android
    exit /b 1
)

REM Build for x86
echo Building for i686-linux-android (x86)...
SET CC=%TOOLCHAIN%\i686-linux-android21-clang.cmd
SET CXX=%TOOLCHAIN%\i686-linux-android21-clang++.cmd
cargo build --release --target i686-linux-android
if %ERRORLEVEL% NEQ 0 (
    echo Failed to build for i686-linux-android
    exit /b 1
)

echo.
echo All Android builds completed successfully!
echo.
echo Output libraries:
echo   target\aarch64-linux-android\release\libtokenizer_rust.so (arm64-v8a)
echo   target\armv7-linux-androideabi\release\libtokenizer_rust.so (armeabi-v7a)
echo   target\x86_64-linux-android\release\libtokenizer_rust.so (x86_64)
echo   target\i686-linux-android\release\libtokenizer_rust.so (x86)
