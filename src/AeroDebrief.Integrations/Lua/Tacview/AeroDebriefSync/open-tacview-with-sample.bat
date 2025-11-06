@echo off
REM Quick script to open Tacview with the test sample file

echo ========================================
echo Opening Tacview with Test Sample File
echo ========================================
echo.

set SAMPLE_FILE=%~dp0test-sample.txt.acmi
set TACVIEW_EXE=C:\Program Files (x86)\Tacview\Tacview.exe

if not exist "%SAMPLE_FILE%" (
    echo ERROR: Sample file not found at:
    echo %SAMPLE_FILE%
    echo.
    echo Please ensure test-sample.txt.acmi exists in this directory.
    pause
    exit /b 1
)

if not exist "%TACVIEW_EXE%" (
    echo ERROR: Tacview not found at:
    echo %TACVIEW_EXE%
    echo.
    echo Please install Tacview or update the path in this script.
    pause
    exit /b 1
)

echo Opening Tacview...
start "" "%TACVIEW_EXE%" "%SAMPLE_FILE%"

echo.
echo Tacview should now be opening with the sample file.
echo.
echo NEXT STEPS:
echo 1. Wait for Tacview to fully load the file
echo 2. Check Tacview logs (Help -^> Show Log)
echo 3. Look for: "==^> OnUpdate loop started"
echo 4. Run test client: cd ..\..\..\..\AeroDebrief.TacviewTestApp
echo 5. Then: dotnet run
echo.
echo Press any key to exit...
pause >nul
