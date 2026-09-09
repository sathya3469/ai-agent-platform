@echo off
REM Phase 4 RAG Platform - Quick Start Script
REM This script helps verify everything is running correctly

echo.
echo ============================================
echo Phase 4: RAG Platform - Quick Start
echo ============================================
echo.

echo Checking prerequisites...
echo.

REM Check Ollama
echo 1. Checking Ollama...
curl -s http://localhost:11434/api/tags >nul 2>&1
if %errorlevel% equ 0 (
    echo   ✓ Ollama is running on port 11434
) else (
    echo   ✗ Ollama is NOT running
    echo   Start with: ollama serve
    pause
    exit /b 1
)

REM Check ChromaDB
echo.
echo 2. Checking ChromaDB...
curl -s http://localhost:8000/api/v1/heartbeat >nul 2>&1
if %errorlevel% equ 0 (
    echo   ✓ ChromaDB is running on port 8000
) else (
    echo   ✗ ChromaDB is NOT running
    echo   Start with: chroma run --host localhost --port 8000
    pause
    exit /b 1
)

REM Check backend
echo.
echo 3. Checking Backend API...
curl -s http://localhost:5243/swagger/v1/swagger.json >nul 2>&1
if %errorlevel% equ 0 (
    echo   ✓ Backend API is running on port 5243
) else (
    echo   ✗ Backend API is NOT running
    echo   Start with: cd backend\AiAgentPlatform.Api ^&^& dotnet run
    pause
    exit /b 1
)

REM Check frontend
echo.
echo 4. Checking Frontend...
curl -s http://localhost:5173 >nul 2>&1
if %errorlevel% equ 0 (
    echo   ✓ Frontend is running on port 5173
) else (
    echo   ✗ Frontend is NOT running
    echo   Start with: cd frontend ^&^& npm run dev
    pause
    exit /b 1
)

echo.
echo ============================================
echo All services are running! ✓
echo ============================================
echo.
echo Open your browser: http://localhost:5173
echo.
echo Quick Test:
echo 1. Click "Upload Documents" button
echo 2. Select and upload a text file
echo 3. Wait for "Chunks created" message
echo 4. Ask a question in the chat
echo 5. Response should reference your document
echo.
pause
