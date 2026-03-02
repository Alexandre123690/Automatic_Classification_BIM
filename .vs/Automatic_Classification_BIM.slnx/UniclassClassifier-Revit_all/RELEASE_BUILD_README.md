# Uniclass AI Classifier - Release Build Guide

## ?? Diferenças: Debug vs Release

| Aspecto | Debug | Release |
|---------|-------|---------|
| **Python** | Sistema do utilizador | Empacotado com plugin |
| **Dependências** | Instalação automática | Pré-instaladas |
| **Tamanho** | ~15 MB | ~130 MB |
| **Setup** | Requer Python instalado | Tudo incluído |
| **Portabilidade** | Depende do sistema | 100% portátil |

---

## ?? Como Criar Build Release

### 1. Preparar Python Empacotado

```bash
# 1. Download Python Embeddable 3.11
https://www.python.org/ftp/python/3.11.8/python-3.11.8-embed-amd64.zip

# 2. Extrair para UniclassClassifier/Python/

# 3. Habilitar pip (editar python311._pth)
# Descomentar: import site

# 4. Instalar get-pip.py
python.exe get-pip.py

# 5. Instalar dependências
python.exe -m pip install scikit-learn pandas numpy joblib xgboost

# 6. Limpar cache
del /s /q __pycache__
del /s /q *.pyc
```

### 2. Compilar Release

```
Visual Studio ? Configuration: "Release R25" ? Build
```

### 3. Testar

```
1. Fechar Revit
2. Limpar %APPDATA%\Autodesk\Revit\Addins\2025\UniclassClassifier\
3. Compilar (build copiará Python empacotado)
4. Abrir Revit
5. Testar plugin
```

---

## ? Checklist Release

- [ ] Python empacotado na pasta `UniclassClassifier/Python/`
- [ ] Todas as dependências instaladas no Python empacotado
- [ ] Modelos ML (.pkl) incluídos
- [ ] Testado em máquina sem Python instalado
- [ ] Testado no Revit 2025
- [ ] Documentação atualizada
- [ ] Versão atualizada em `AssemblyInfo` ou `.csproj`

---

## ?? Estrutura Final Release

```
%APPDATA%\Autodesk\Revit\Addins\2025\
??? UniclassClassifier.addin
??? UniclassClassifier/
    ??? UniclassClassifier.dll
    ??? *.dll (dependências)
    ??? Python/                    ?? NOVO em Release
    ?   ??? python.exe
    ?   ??? python311.dll
    ?   ??? Lib/site-packages/
    ?       ??? sklearn/
    ?       ??? pandas/
    ?       ??? numpy/
    ?       ??? joblib/
    ?       ??? xgboost/
    ??? Scripts/
    ?   ??? predict_and_return.py
    ??? Temp/
        ??? *.pkl
        ??? Ss_tabela_rows.json
```

---

## ?? Vantagens Release

? **Utilizador não precisa instalar Python**  
? **Não conflita com Python do sistema**  
? **Versão controlada**  
? **Funciona offline**  
? **Mais profissional**  

---

## ?? Notas Importantes

1. **Tamanho**: Release será ~130MB vs Debug ~15MB
2. **Licenças**: Todas as bibliotecas são open-source
3. **Atualizações**: Para atualizar dependências Python, recriar pasta Python/
4. **Compatibilidade**: Python empacotado só funciona no Windows x64

---

## ?? Troubleshooting Release

### Erro: "Python empacotado não encontrado"
**Solução**: Verificar se pasta `Python/` existe e contém `python.exe`

### Erro: "Módulo não encontrado"
**Solução**: Reinstalar dependências no Python empacotado

### Erro: "DLL não encontrada"
**Solução**: Verificar se todos os ficheiros .dll do Python estão presentes

---

## ?? Suporte

Para problemas com Release build:
1. Verificar logs em `%APPDATA%\Autodesk\Revit\Addins\2025\`
2. Testar em modo Debug primeiro
3. Contactar suporte técnico
