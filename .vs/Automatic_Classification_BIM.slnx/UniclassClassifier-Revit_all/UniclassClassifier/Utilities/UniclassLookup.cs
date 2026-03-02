using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace UniclassClassifier.Utilities
{
    public class UniclassCodeInfo
    {
        public string Code { get; set; }
        public string Title { get; set; }
    }

    public static class UniclassLookup
    {
        private static Dictionary<string, string> _codeToDescriptionCache;
        private static readonly object _lock = new object();

        /// <summary>
        /// Carrega o ficheiro JSON e cria um dicionário para lookup rápido
        /// </summary>
        public static Dictionary<string, string> LoadCodeDescriptions()
        {
            lock (_lock)
            {
                if (_codeToDescriptionCache != null)
                {
                    return _codeToDescriptionCache;
                }

                try
                {
                    string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string jsonPath = Path.Combine(dllDir, "Temp", "Ss_tabela_rows.json");

                    if (!File.Exists(jsonPath))
                    {
                        _codeToDescriptionCache = new Dictionary<string, string>();
                        return _codeToDescriptionCache;
                    }

                    string jsonContent = File.ReadAllText(jsonPath);
                    var codeInfoList = JsonConvert.DeserializeObject<List<UniclassCodeInfo>>(jsonContent);

                    _codeToDescriptionCache = codeInfoList
                        .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                        .ToDictionary(
                            item => item.Code.Replace("_", " ").Trim(),
                            item => item.Title ?? string.Empty,
                            StringComparer.OrdinalIgnoreCase
                        );

                    return _codeToDescriptionCache;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao carregar Ss_tabela_rows.json: {ex.Message}");
                    _codeToDescriptionCache = new Dictionary<string, string>();
                    return _codeToDescriptionCache;
                }
            }
        }

        /// <summary>
        /// Faz VLOOKUP do código Uniclass e retorna a descrição
        /// </summary>
        /// <param name="code">Código Uniclass (ex: "Ss_25_13_50")</param>
        /// <returns>Descrição do código ou string vazia se não encontrado</returns>
        public static string GetDescription(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            var lookup = LoadCodeDescriptions();

            // Tentar com o código exatamente como vem
            if (lookup.TryGetValue(code, out string description))
                return description;

            // Tentar com underscore convertido para espaço
            string codeWithSpaces = code.Replace("_", " ");
            if (lookup.TryGetValue(codeWithSpaces, out description))
                return description;

            // Tentar com Ss_ no início (caso venha sem prefixo)
            if (!code.StartsWith("Ss", StringComparison.OrdinalIgnoreCase))
            {
                string codeWithPrefix = "Ss_" + code;
                if (lookup.TryGetValue(codeWithPrefix, out description))
                    return description;

                codeWithPrefix = "Ss " + code;
                if (lookup.TryGetValue(codeWithPrefix, out description))
                    return description;
            }

            return string.Empty;
        }

        /// <summary>
        /// Limpa o cache (útil se o ficheiro JSON for atualizado)
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _codeToDescriptionCache = null;
            }
        }
    }
}
