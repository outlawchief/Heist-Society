@echo off
cd /d "%~dp0"
echo Starting Heist Society LAN server (needs POST /coop for multiplayer)...
python serve.py
if errorlevel 1 py serve.py
pause
