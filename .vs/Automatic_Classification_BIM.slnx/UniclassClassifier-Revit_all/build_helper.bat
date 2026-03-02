@echo off
echo ========================================
echo Build Helper - Fecha Revit e Compila
echo ========================================
echo.

REM Verificar se o Revit está a correr
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I /N "Revit.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Revit detectado. A fechar...
    taskkill /IM Revit.exe /F
    timeout /t 3 /nobreak > NUL
    echo Revit fechado.
) else (
    echo Revit nao esta a correr.
)

echo.
echo A compilar projeto...
dotnet build "UniclassClassifier\UniclassClassifier.csproj" --configuration "Debug R25"

if %ERRORLEVEL% == 0 (
    echo.
    echo ========================================
    echo Compilacao concluida com sucesso!
    echo ========================================
    echo.
    echo Deseja abrir o Revit 2025? (S/N)
    choice /C SN /N
    if errorlevel 2 goto :end
    if errorlevel 1 goto :openrevit
) else (
    echo.
    echo ========================================
    echo ERRO na compilacao!
    echo ========================================
    pause
    goto :end
)

:openrevit
echo.
echo A abrir Revit 2025...
start "" "C:\Program Files\Autodesk\Revit 2025\Revit.exe"
goto :end

:end
echo.
pause
