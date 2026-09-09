@echo off
REM Phase 4 RAG Platform - Start All Services
REM Run this batch file to start all services in separate windows

echo.
echo ============================================
echo Phase 4: RAG Platform - Starting All Services
echo ============================================
echo.

echo Starting Backend API...
start "Backend API (Port 5243)" cmd /k "cd /d E:\ai-agent-platform\backend\AiAgentPlatform.Api && dotnet run"

timeout /t 3

echo Starting Frontend...
start "Frontend (Port 5173)" cmd /k "cd /d E:\ai-agent-platform\frontend && npm run dev"

echo.
echo ============================================
echo Services starting...
echo.
echo Backend: http://localhost:5243
echo Frontend: http://localhost:5173
echo.
echo Make sure Ollama and ChromaDB are running!
echo.
echo Close any terminal window to stop that service.
echo ============================================
echo.
pause
