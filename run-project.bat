@echo off
title Sparkle E-Commerce
cd /d "%~dp0"

REM Ensure dotnet and node are on PATH
set "PATH=C:\Users\Minhajul Islam\.dotnet;C:\Users\Minhajul Islam\.nodejs\node-v20.18.0-win-x64;%PATH%"

echo ================================================================
echo               Starting Sparkle E-Commerce Project
echo ================================================================
echo.
echo URL: http://localhost:5279
echo Admin URL: http://localhost:5279/admin/login
echo.
echo ================================================================

cd Sparkle.Api
dotnet watch run
pause
