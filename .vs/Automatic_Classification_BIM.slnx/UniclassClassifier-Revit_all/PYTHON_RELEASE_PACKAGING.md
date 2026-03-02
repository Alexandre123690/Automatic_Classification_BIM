# ?? Guia de Empacotamento Python para Release

## ?? Objetivo
Criar um ambiente Python portátil e independente que seja distribuído com o plugin Revit.

---

## ?? Estratégia

### **Debug Build**
? Usa Python do sistema  
? Instala dependências automaticamente  
? Facilita desenvolvimento  

### **Release Build**
? Usa Python empacotado (incluído no instalador)  
? Ambiente isolado (não depende do sistema)  
? Todas as dependências pré-instaladas  
? Utilizador não precisa instalar nada  

---

## ??? Como Criar Ambiente Python Empacotado

### **Opção 1: Python Embeddable (Recomendado)**

#### Passo 1: Download Python Embeddable
```bash
# Download Python 3.11 Embeddable (64-bit)
https://www.python.org/ftp/python/3.11.8/python-3.11.8-embed-amd64.zip
```

#### Passo 2: Extrair e Configurar
```bash
# Extrair para pasta do projeto
UniclassClassifier/
??? Python/
    ??? python.exe
    ??? python311.dll
    ??? python311.zip
    ??? ... (outros ficheiros)
```

#### Passo 3: Habilitar pip
```bash
# 1. Download get-pip.py
https://bootstrap.pypa.io/get-pip.py

# 2. Executar
cd UniclassClassifier\Python
python.exe get-pip.py

# 3. Editar python311._pth (remover comentário):
python311.zip
.
import site  # <-- Descomentar esta linha
```

#### Passo 4: Instalar Dependências
```bash
cd UniclassClassifier\Python
python.exe -m pip install scikit-learn pandas numpy joblib xgboost
```

#### Passo 5: Limpar Cache (reduzir tamanho)
```bash
# Remover ficheiros desnecessários
cd UniclassClassifier\Python\Lib\site-packages
# Apagar pastas __pycache__
# Apagar ficheiros .pyc
# Apagar pasta tests/
```

---

### **Opção 2: Virtual Environment (Alternativa)**

```bash
# 1. Criar ambiente virtual
python -m venv UniclassClassifier\Python

# 2. Ativar ambiente
UniclassClassifier\Python\Scripts\activate

# 3. Instalar dependências
pip install scikit-learn pandas numpy joblib xgboost

# 4. Desativar
deactivate
```

---

## ?? Estrutura do Instalador Release

```
UniclassClassifier_Release/
??? UniclassClassifier.addin
??? UniclassClassifier/
?   ??? UniclassClassifier.dll
?   ??? UniclassClassifier.pdb (opcional)
?   ??? *.dll (dependências .NET)
?   ??? Python/                        ?? NOVO: Python empacotado
?   ?   ??? python.exe
?   ?   ??? python311.dll
?   ?   ??? Lib/
?   ?   ?   ??? site-packages/
?   ?   ?       ??? sklearn/
?   ?   ?       ??? pandas/
?   ?   ?       ??? numpy/
?   ?   ?       ??? joblib/
?   ?   ?       ??? xgboost/
?   ?   ??? Scripts/
?   ??? Scripts/
?   ?   ??? predict_and_return.py
?   ??? Temp/
?       ??? *.pkl
?       ??? Ss_tabela_rows.json
??? Install.bat
```

---

## ?? Modificações no Código

### **Classifier.cs - Usar PythonManager**

```csharp
// Antes (Debug apenas):
string pythonExe = PythonEnvironment.GetPythonPath();

// Depois (Debug + Release):
var (success, pythonExe, message) = PythonManager.GetPythonEnvironment();

if (!success)
{
    if (PythonManager.IsDebugMode())
    {
        // Oferecer instalação automática
        if (PythonManager.ShowPythonSetupDialog(message))
        {
            var (installOk, installMsg) = PythonManager.TryInstallDependencies(pythonExe);
            // ...
        }
    }
    else
    {
        // Release: Apenas mostrar erro e sair
        TaskDialog.Show("Erro", message);
        return Result.Failed;
    }
}
```

---

## ?? Tamanho Estimado

| Componente | Tamanho |
|------------|---------|
| Python Embeddable | ~10 MB |
| scikit-learn | ~30 MB |
| pandas | ~40 MB |
| numpy | ~20 MB |
| xgboost | ~10 MB |
| joblib | ~1 MB |
| **Total Python** | **~110 MB** |
| Plugin .NET | ~2 MB |
| Modelos ML (.pkl) | ~15 MB |
| **TOTAL** | **~127 MB** |

---

## ?? Script de Build Release

```batch
@echo off
echo ========================================
echo Build Release - Uniclass Classifier
echo ========================================

set SOURCE=UniclassClassifier\bin\Release R25\net8.0-windows
set DEST=Release\UniclassClassifier

REM Criar estrutura
mkdir "%DEST%"
mkdir "%DEST%\Scripts"
mkdir "%DEST%\Temp"

REM Copiar DLLs
xcopy /Y /Q "%SOURCE%\*.dll" "%DEST%\"
xcopy /Y /Q "%SOURCE%\UniclassClassifier.dll" "%DEST%\"

REM Copiar Scripts Python
xcopy /Y /Q "UniclassClassifier\Scripts\*.py" "%DEST%\Scripts\"

REM Copiar Modelos ML
xcopy /Y /Q "UniclassClassifier\Temp\*.pkl" "%DEST%\Temp\"
xcopy /Y /Q "UniclassClassifier\Temp\*.json" "%DEST%\Temp\"

REM Copiar Python Empacotado
xcopy /E /I /Y /Q "UniclassClassifier\Python" "%DEST%\Python"

REM Copiar .addin
copy /Y "UniclassClassifier\UniclassClassifier.addin" "Release\"

echo.
echo Build Release concluído!
echo Pasta: %CD%\Release
pause
```

---

## ? Checklist Pre-Release

- [ ] Python empacotado testado
- [ ] Todas as dependências instaladas
- [ ] Modelos ML incluídos
- [ ] Scripts Python incluídos
- [ ] Testado em máquina limpa (sem Python instalado)
- [ ] Testado em Revit 2025
- [ ] Tamanho total do instalador < 150 MB
- [ ] Documentação de instalação criada

---

## ?? Vantagens do Python Empacotado

? **Utilizador não precisa instalar nada**  
? **Não conflita com Python do sistema**  
? **Versão específica garantida**  
? **Todas as dependências incluídas**  
? **Funciona offline**  
? **Fácil de desinstalar** (apenas apagar pasta)  

---

## ?? Considerações Legais

- Python: Licença PSF (Python Software Foundation) - Open Source ?
- scikit-learn: BSD License - Open Source ?
- pandas: BSD License - Open Source ?
- numpy: BSD License - Open Source ?
- xgboost: Apache 2.0 - Open Source ?
- joblib: BSD License - Open Source ?

**Todos os componentes podem ser redistribuídos legalmente!**

---

## ?? Suporte

Para problemas com o ambiente Python empacotado:
1. Verificar integridade dos ficheiros
2. Reinstalar o plugin
3. Verificar logs em `%APPDATA%\Autodesk\Revit\Addins\2025\`
