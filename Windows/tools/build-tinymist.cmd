@echo off
setlocal
set "PATH=%USERPROFILE%\.cargo\bin;C:\BuildTools\VC\Auxiliary\Build;%PATH%"
set "PATH=C:\TypstStudio-tools\LLVM\bin;%PATH%"
set CARGO_BUILD_JOBS=3
set RUSTUP_TOOLCHAIN=stable
cd /d C:\TypstStudio-build\tinymist
call C:\BuildTools\VC\Auxiliary\Build\vcvarsarm64.bat
cargo +stable build --release --locked --target aarch64-pc-windows-msvc -p tinymist-cli --bin tinymist
if errorlevel 1 exit /b 1
rustup target add x86_64-pc-windows-msvc --toolchain stable
if errorlevel 1 exit /b 1
call C:\BuildTools\VC\Auxiliary\Build\vcvarsarm64_amd64.bat
cargo +stable build --release --locked --target x86_64-pc-windows-msvc -p tinymist-cli --bin tinymist
if errorlevel 1 exit /b 1
