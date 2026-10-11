@echo off
.\tools\onsmake
if ERRORLEVEL 1 goto :error
cd game
start onscripter-ru-dev.exe
cd ..
exit
:error
pause