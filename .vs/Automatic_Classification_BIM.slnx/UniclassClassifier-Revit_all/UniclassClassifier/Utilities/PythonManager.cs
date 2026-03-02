using System;
using Autodesk.Revit.UI;

namespace UniclassClassifier.Utilities
{
    /// <summary>
    /// Gerenciador unificado de ambiente Python
    /// Escolhe automaticamente entre ambiente empacotado (Release) e sistema (Debug)
    /// </summary>
    public static class PythonManager
    {
        /// <summary>
        /// Obtém o caminho do Python apropriado para a configuração atual
        /// </summary>
        public static (bool success, string pythonPath, string message) GetPythonEnvironment()
        {
#if DEBUG
            // Modo Debug: Usar Python do sistema + instalação automática
            string pythonPath = PythonEnvironment.GetPythonPath();
            
            if (string.IsNullOrEmpty(pythonPath))
            {
                return (false, null, 
                    "Python não encontrado no sistema.\n\n" +
                    "Em modo Debug, o plugin usa o Python do sistema.\n" +
                    "Instale Python 3.8+ de: https://www.python.org/downloads/");
            }
            
            // Verificar dependências
            var (depsOk, depsMessage) = PythonEnvironment.CheckDependencies(pythonPath);
            
            if (!depsOk)
            {
                // Em Debug, oferecemos instalar automaticamente
                return (false, pythonPath, 
                    $"Modo DEBUG: {depsMessage}\n\n" +
                    "O plugin pode instalar automaticamente as dependências.");
            }
            
            return (true, pythonPath, "Python do sistema (Debug)");
#else
            // Modo Release: Usar Python empacotado
            string bundledPython = PythonEnvironmentRelease.GetBundledPythonPath();
            
            if (string.IsNullOrEmpty(bundledPython))
            {
                return (false, null,
                    "Ambiente Python empacotado não encontrado.\n\n" +
                    "O plugin deve incluir um ambiente Python completo.\n" +
                    "Por favor, reinstale o plugin ou contacte o suporte.");
            }
            
            // Validar ambiente empacotado
            var (isValid, validMessage) = PythonEnvironmentRelease.ValidateBundledEnvironment();
            
            if (!isValid)
            {
                return (false, bundledPython, validMessage);
            }
            
            return (true, bundledPython, "Python empacotado (Release)");
#endif
        }
        
        /// <summary>
        /// Tenta instalar dependências (apenas em modo Debug)
        /// </summary>
        public static (bool success, string message) TryInstallDependencies(string pythonPath)
        {
#if DEBUG
            return PythonEnvironment.InstallDependencies(pythonPath);
#else
            return (false, "Instalação de dependências não está disponível em modo Release.\n" +
                "Por favor, reinstale o plugin.");
#endif
        }
        
        /// <summary>
        /// Verifica se está em modo Debug
        /// </summary>
        public static bool IsDebugMode()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Mostra diálogo apropriado para configurar Python
        /// </summary>
        public static bool ShowPythonSetupDialog(string errorMessage)
        {
#if DEBUG
            // Debug: Oferecer instalação automática
            TaskDialog td = new TaskDialog("Configuração Python (Debug)");
            td.MainInstruction = "Dependências Python Necessárias";
            td.MainContent = $"{errorMessage}\n\n" +
                "Modo DEBUG ativo: O plugin pode instalar automaticamente.";
            
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Instalar Automaticamente",
                "Instalar: scikit-learn, pandas, numpy, joblib, xgboost");
            
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "Cancelar",
                "Sair sem instalar");
            
            td.CommonButtons = TaskDialogCommonButtons.None;
            
            return td.Show() == TaskDialogResult.CommandLink1;
#else
            // Release: Apenas mostrar erro
            TaskDialog.Show("Erro - Ambiente Python",
                $"{errorMessage}\n\n" +
                "Por favor, reinstale o plugin ou contacte o suporte técnico.");
            
            return false;
#endif
        }
    }
}
