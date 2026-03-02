using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace UniclassClassifier.Utilities
{
    /// <summary>
    /// Gerenciamento de ambiente Python para versão Release
    /// Usa ambiente Python empacotado com o plugin
    /// </summary>
    public static class PythonEnvironmentRelease
    {
        /// <summary>
        /// Obtém o caminho do Python empacotado com o plugin
        /// </summary>
        public static string GetBundledPythonPath()
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            // Caminho para Python empacotado (distribuído com o plugin)
            string bundledPython = Path.Combine(dllDir, "Python", "python.exe");
            
            if (File.Exists(bundledPython))
            {
                return bundledPython;
            }
            
            return null;
        }
        
        /// <summary>
        /// Verifica se o ambiente Python empacotado está completo
        /// </summary>
        public static (bool isValid, string message) ValidateBundledEnvironment()
        {
            string pythonPath = GetBundledPythonPath();
            
            if (string.IsNullOrEmpty(pythonPath))
            {
                return (false, "Python empacotado não encontrado.\n\n" +
                    "O plugin deve incluir um ambiente Python completo.\n" +
                    "Por favor, reinstale o plugin.");
            }
            
            // Verificar se as dependências estão instaladas no ambiente empacotado
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "-c \"import sklearn; import pandas; import numpy; import joblib; import xgboost\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return (true, "Ambiente Python válido.");
                    }
                    else
                    {
                        return (false, $"Ambiente Python corrompido:\n{stderr}\n\n" +
                            "Por favor, reinstale o plugin.");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao validar Python:\n{ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtém a versão do Python empacotado
        /// </summary>
        public static string GetBundledPythonVersion()
        {
            string pythonPath = GetBundledPythonPath();
            
            if (string.IsNullOrEmpty(pythonPath))
                return "N/A";
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return !string.IsNullOrWhiteSpace(output) ? output.Trim() : error.Trim();
                }
            }
            catch
            {
                return "Desconhecida";
            }
        }
    }
}
