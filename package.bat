@echo off
rem Builds the app and zips it (without platform-tools or ROM files) to dist\FlashDeck-portable.zip
cd /d "%~dp0"
call "%~dp0build.bat"
if errorlevel 1 exit /b 1
if not exist dist mkdir dist
powershell -NoProfile -Command "Compress-Archive -Force -Path 'Flash Deck.exe','README.md','FlashDeck.cs','build.bat','app.ico' -DestinationPath 'dist\FlashDeck-portable.zip'"
echo Done: dist\FlashDeck-portable.zip
