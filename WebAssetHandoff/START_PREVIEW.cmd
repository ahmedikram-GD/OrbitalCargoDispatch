@echo off
cd /d "%~dp0"
echo Open http://127.0.0.1:8766 in your browser.
echo Leave this window open while viewing. Press Ctrl+C to stop.
python -m http.server 8766 --bind 127.0.0.1
pause
