@echo off
title Connect to GitHub - Sparkle Ecommerce
cd /d "%~dp0"

REM Ensure Git and GitHub CLI are in PATH
set "PATH=C:\Users\Minhajul Islam\.gh-bin\bin;C:\Users\Minhajul Islam\.git-bin\cmd;%PATH%"

echo ================================================================
echo               Connect Sparkle Ecommerce to GitHub
echo ================================================================
echo Repository: https://github.com/minhajsoyan07/Sparkle-Ecommerce
echo.
echo Step 1: Logging in to GitHub...
echo (A one-time code will appear. Press Enter to open GitHub in your browser)
echo.

gh auth login -w -p https

echo.
echo ================================================================
echo Step 2: Configuring Git credential helper...
echo ================================================================
gh auth setup-git

echo.
echo ================================================================
echo Step 3: Pushing project to GitHub...
echo ================================================================
git push -u origin main

echo.
echo ================================================================
echo Done! Your project is connected and synced with GitHub.
echo URL: https://github.com/minhajsoyan07/Sparkle-Ecommerce
echo ================================================================
pause
