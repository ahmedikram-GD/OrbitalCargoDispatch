@echo off
cd /d "%~dp0"
echo Open http://127.0.0.1:8767/ in your browser.
echo Stop this preview with Ctrl+C.
python -m http.server 8767 --bind 127.0.0.1
pause
