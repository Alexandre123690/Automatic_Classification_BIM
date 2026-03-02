using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UniclassClassifier.Utilities
{
    public static class PythonEnvironment
    {
        private static string _pythonPath;
        
        /// <summary>
        /// Detecta automaticamente o caminho do Python instalado no sistema
        /// </summary>
        public static string GetPythonPath()
        {
            if (!string.IsNullOrEmpty(_pythonPath) && File.Exists(_pythonPath))
                return _pythonPath;

            // Locais comuns de instalação do Python
            string[] possiblePaths = new[]
            {
                @"C:\Python314\python.exe",
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python39\python.exe",
                @"C:\Python38\python.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python314\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"anaconda3\python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"miniconda3\python.exe"),
                @"C:\anaconda3\python.exe",
                @"C:\miniconda3\python.exe"
            };

            // Procurar Python no PATH
            string pythonFromPath = FindPythonInPath();
            if (!string.IsNullOrEmpty(pythonFromPath))
            {
                _pythonPath = pythonFromPath;
                return _pythonPath;
            }

            // Procurar nos locais comuns
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _pythonPath = path;
                    return _pythonPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Procura Python no PATH do sistema
        /// </summary>
        private static string FindPythonInPath()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "python",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        // Pegar a primeira linha (primeiro Python encontrado)
                        var firstPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (!string.IsNullOrEmpty(firstPath) && File.Exists(firstPath))
                        {
                            return firstPath;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Verifica se o Python tem todas as dependências necessárias instaladas
        /// </summary>
        public static (bool success, string message) CheckDependencies(string pythonPath)
        {
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                return (false, "Python não encontrado.");

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
                        return (true, "Todas as dependências estão instaladas.");
                    }
                    else
                    {
                        string missing = "";
                        if (stderr.Contains("sklearn")) missing += "scikit-learn ";
                        if (stderr.Contains("pandas")) missing += "pandas ";
                        if (stderr.Contains("numpy")) missing += "numpy ";
                        if (stderr.Contains("joblib")) missing += "joblib ";
                        if (stderr.Contains("xgboost")) missing += "xgboost ";
                        
                        return (false, $"Dependências em falta: {missing}");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao verificar dependências: {ex.Message}");
            }
        }

        /// <summary>
        /// Instala as dependências Python necessárias
        /// </summary>
        public static (bool success, string message) InstallDependencies(string pythonPath)
        {
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                return (false, "Python não encontrado.");

            try
            {
                string[] packages = { "scikit-learn", "pandas", "joblib", "numpy", "xgboost" };
                
                foreach (var package in packages)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = $"-m pip install {package}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        
                        if (process.ExitCode != 0)
                        {
                            string stderr = process.StandardError.ReadToEnd();
                            return (false, $"Erro ao instalar {package}: {stderr}");
                        }
                    }
                }

                return (true, "Todas as dependências foram instaladas com sucesso!");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao instalar dependências: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtém informações sobre a versão do Python
        /// </summary>
        public static string GetPythonVersion(string pythonPath)
        {
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                return "Desconhecida";

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
