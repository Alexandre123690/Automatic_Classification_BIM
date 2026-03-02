@echo off
echo ============================================
echo Instalando dependencias Python necessarias
echo ============================================
echo.

REM Usar o Python encontrado no sistema
set PYTHON_EXE=C:\Python314\python.exe

echo Verificando Python...
"%PYTHON_EXE%" --version
if %errorlevel% neq 0 (
    echo ERRO: Python nao encontrado em %PYTHON_EXE%
    pause
    exit /b 1
)

echo.
echo Atualizando pip...
"%PYTHON_EXE%" -m pip install --upgrade pip

echo.
echo Instalando scikit-learn...
"%PYTHON_EXE%" -m pip install scikit-learn

echo.
echo Instalando pandas...
"%PYTHON_EXE%" -m pip install pandas

echo.
echo Instalando joblib...
"%PYTHON_EXE%" -m pip install joblib

echo.
echo Instalando numpy...
"%PYTHON_EXE%" -m pip install numpy

echo.
echo Instalando xgboost...
"%PYTHON_EXE%" -m pip install xgboost

echo.
echo ============================================
echo Instalacao concluida!
echo ============================================
echo.
echo Verificando instalacao...
"%PYTHON_EXE%" -c "import sklearn; import pandas; import numpy; import joblib; import xgboost; print('Todas as dependencias instaladas com sucesso!')"

if %errorlevel% neq 0 (
    echo.
    echo AVISO: Algumas dependencias podem nao ter sido instaladas corretamente.
    echo Execute este script novamente como Administrador.
)

echo.
pause
