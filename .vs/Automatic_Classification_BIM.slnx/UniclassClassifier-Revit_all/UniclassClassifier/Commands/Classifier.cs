using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using UniclassClassifier.Utilities;
using UniclassClassifier.Windows;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ClassifierCommand : IExternalCommand
    {
        private const double FeetToMeter = 0.3048;
        private const double Ft2ToM2 = 0.092903;
        private const double Ft3ToM3 = 0.0283168;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Show configuration window
                var configWindow = new ClassificationConfigWindow();
                bool? result = configWindow.ShowDialog();

                if (result != true || configWindow.WasCancelled)
                {
                    return Result.Cancelled;
                }

                double level1Threshold = configWindow.Level1Threshold;
                double level2Threshold = configWindow.Level2Threshold;

                // Start timer
                var stopwatch = Stopwatch.StartNew();

                // Primeiro, criar os parâmetros compartilhados se não existirem
                using (Transaction tx = new Transaction(doc, "Criar Parâmetros Uniclass"))
                {
                    tx.Start();
                    SharedParameterUtils.CreateSharedParameters(doc);
                    tx.Commit();
                }

                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string tempDir = Path.Combine(dllDir, "Temp");
                string scriptPath = Path.Combine(dllDir, "Scripts", "predict_and_return.py");

                Directory.CreateDirectory(tempDir);
                string jsonPath = Path.Combine(tempDir, "export.json");
                string csvPath = Path.Combine(tempDir, "export.csv");
                string classifiedPath = Path.Combine(tempDir, "classified.json");
                string modelDir = Path.Combine(dllDir, "Temp");

                var exportData = ExportElementData(doc);
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(exportData, Formatting.Indented));

                using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true)))
                {
                    if (exportData.Count > 0)
                    {
                        var headers = exportData[0].Keys.ToList();
                        writer.WriteLine(string.Join(",", headers));

                        foreach (var row in exportData)
                        {
                            var values = headers.Select(h => SanitizeCsv(row.ContainsKey(h) ? row[h] : "None"));
                            writer.WriteLine(string.Join(",", values));
                        }
                    }
                }

                // caminho original dos modelos (no teu projeto)
                // NOTA: Os modelos já devem estar copiados para o AppData durante o build
                // O MSBuild target "CopyScriptsAndModelsToRoaming" faz isto automaticamente
                string targetModelDir = Path.Combine(dllDir, "Temp");

                // Verificar se os modelos existem no destino (AppData)
                // Se não existirem, tentar copiar da pasta do projeto (para desenvolvimento)
                string[] requiredModels = { "model_level1.pkl", "encodertargetlevel1.pkl", "model_level2.pkl", "encodertargetlevel2.pkl" };
                
                foreach (var modelFile in requiredModels)
                {
                    string dst = Path.Combine(targetModelDir, modelFile);
                    
                    if (!File.Exists(dst))
                    {
                        // Tentar encontrar na pasta do projeto (apenas para desenvolvimento)
                        string projectTempDir = Path.Combine(Path.GetDirectoryName(dllDir), "Temp");
                        string src = Path.Combine(projectTempDir, modelFile);
                        
                        if (File.Exists(src))
                        {
                            File.Copy(src, dst, true);
                        }
                        else
                        {
                            TaskDialog.Show("Erro - Modelos em Falta", 
                                $"Modelo não encontrado: {modelFile}\n\n" +
                                $"Esperado em: {dst}\n\n" +
                                "Os modelos ML devem estar incluídos na instalação do plugin.\n" +
                                "Certifique-se de que todos os ficheiros .pkl foram copiados corretamente.");
                            return Result.Failed;
                        }
                    }
                }

                // Detectar Python automaticamente
                string pythonExe = PythonEnvironment.GetPythonPath();
                
                if (string.IsNullOrEmpty(pythonExe))
                {
                    TaskDialog.Show("Python não encontrado", 
                        "Não foi possível encontrar o Python instalado no sistema.\n\n" +
                        "Por favor, instale o Python 3.8 ou superior:\n" +
                        "https://www.python.org/downloads/\n\n" +
                        "Ou configure o caminho manualmente no código.");
                    return Result.Failed;
                }

                // Obter informações do Python
                string pythonVersion = PythonEnvironment.GetPythonVersion(pythonExe);
                
                // Verificar dependências
                var (depsOk, depsMessage) = PythonEnvironment.CheckDependencies(pythonExe);
                
                if (!depsOk)
                {
                    // Perguntar se deseja instalar as dependências
                    TaskDialog td = new TaskDialog("Dependências Python em Falta");
                    td.MainInstruction = "Dependências Python Necessárias";
                    td.MainContent = $"Python encontrado: {pythonVersion}\n" +
                                    $"Localização: {pythonExe}\n\n" +
                                    $"{depsMessage}\n\n" +
                                    "Deseja instalar as dependências automaticamente?";
                    
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, 
                        "Instalar Automaticamente", 
                        "O plugin irá instalar: scikit-learn, pandas, numpy, joblib, xgboost");
                    
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, 
                        "Instalar Manualmente", 
                        "Abrir instruções para instalação manual");
                    
                    td.CommonButtons = TaskDialogCommonButtons.Close;
                    td.DefaultButton = TaskDialogResult.CommandLink1;
                    
                    TaskDialogResult tdResult = td.Show();
                    
                    if (tdResult == TaskDialogResult.CommandLink1)
                    {
                        // Instalar automaticamente
                        TaskDialog progressDialog = new TaskDialog("Instalando Dependências");
                        progressDialog.MainInstruction = "A instalar dependências Python...";
                        progressDialog.MainContent = "Por favor aguarde. Isto pode demorar alguns minutos.";
                        progressDialog.CommonButtons = TaskDialogCommonButtons.None;
                        
                        var (installOk, installMessage) = PythonEnvironment.InstallDependencies(pythonExe);
                        
                        if (!installOk)
                        {
                            TaskDialog.Show("Erro na Instalação", 
                                $"Erro ao instalar dependências:\n\n{installMessage}\n\n" +
                                "Por favor, instale manualmente usando:\n" +
                                $"{pythonExe} -m pip install scikit-learn pandas joblib numpy xgboost");
                            return Result.Failed;
                        }
                        
                        TaskDialog.Show("Instalação Completa", 
                            "Dependências instaladas com sucesso!\n\n" +
                            "Execute o comando novamente para classificar os elementos.");
                        return Result.Succeeded;
                    }
                    else if (tdResult == TaskDialogResult.CommandLink2)
                    {
                        // Mostrar instruções manuais
                        TaskDialog.Show("Instalação Manual",
                            "Para instalar as dependências manualmente:\n\n" +
                            "1. Abra o Command Prompt (cmd) como Administrador\n\n" +
                            "2. Execute os seguintes comandos:\n\n" +
                            $"   {pythonExe} -m pip install scikit-learn\n" +
                            $"   {pythonExe} -m pip install pandas\n" +
                            $"   {pythonExe} -m pip install joblib\n" +
                            $"   {pythonExe} -m pip install numpy\n" +
                            $"   {pythonExe} -m pip install xgboost\n\n" +
                            "3. Após a instalação, execute este comando novamente.");
                        return Result.Cancelled;
                    }
                    else
                    {
                        return Result.Cancelled;
                    }
                }
                
                // Verificar se todos os ficheiros necessários existem
                if (!File.Exists(scriptPath))
                {
                    TaskDialog.Show("Erro", $"Script Python não encontrado em:\n{scriptPath}\n\nVerifique a instalação do add-in.");
                    return Result.Failed;
                }
                
                // Verificar se os modelos existem
                string[] requiredModelFiles = { "model_level1.pkl", "encodertargetlevel1.pkl", "model_level2.pkl", "encodertargetlevel2.pkl" };
                foreach (var modelFile in requiredModelFiles)
                {
                    string modelPath = Path.Combine(modelDir, modelFile);
                    if (!File.Exists(modelPath))
                    {
                        TaskDialog.Show("Erro", $"Modelo não encontrado:\n{modelPath}\n\nVerifique se os modelos foram copiados corretamente.");
                        return Result.Failed;
                    }
                }
                
                // Pass thresholds as arguments to Python script
                string level1ThresholdStr = level1Threshold.ToString("0.00", CultureInfo.InvariantCulture);
                string level2ThresholdStr = level2Threshold.ToString("0.00", CultureInfo.InvariantCulture);
                
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\" \"{csvPath}\" \"{jsonPath}\" \"{modelDir}\" {level1ThresholdStr} {level2ThresholdStr}",
                    WorkingDirectory = dllDir,  // Definir o diretório de trabalho
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        // Mostrar detalhes completos do erro
                        string errorDetails = $"Python Error:\n{stderr}\n\nOutput:\n{stdout}\n\n" +
                                            $"Script: {scriptPath}\n" +
                                            $"Working Dir: {dllDir}\n" +
                                            $"Python: {pythonExe}";
                        TaskDialog.Show("Erro no script", errorDetails);
                        return Result.Failed;
                    }
                }

                if (!File.Exists(classifiedPath))
                {
                    TaskDialog.Show("Erro", "Ficheiro 'classified.json' não encontrado.");
                    return Result.Failed;
                }

                // Ler classificações
                var classificationResults = JsonConvert.DeserializeObject<Dictionary<string, ClassificationResult>>(File.ReadAllText(classifiedPath));

                // Apply classifications to Revit elements
                using (Transaction tx = new Transaction(doc, "Aplicar Classificação Uniclass"))
                {
                    tx.Start();

                    foreach (var kvp in classificationResults)
                    {
                        if (int.TryParse(kvp.Key, out int eid))
                        {
                            Element elem = doc.GetElement(new ElementId(eid));
                            if (elem == null) continue;

                            // Se não foi classificado, pula
                            if (kvp.Value.level == "none")
                                continue;

                            // Parâmetro Uniclass Code
                            Parameter pUniclass = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                            if (pUniclass != null && !pUniclass.IsReadOnly)
                            {
                                pUniclass.Set(kvp.Value.classification);
                            }

                            // Parâmetro Uniclass Description (VLOOKUP)
                            Parameter pDescription = elem.LookupParameter("Classification.Uniclass.Ss.Description");
                            if (pDescription != null && !pDescription.IsReadOnly)
                            {
                                string description = UniclassLookup.GetDescription(kvp.Value.classification);
                                if (!string.IsNullOrEmpty(description))
                                {
                                    pDescription.Set(description);
                                }
                            }

                            // Parâmetro Confiança
                            Parameter pConf = elem.LookupParameter("Classification.Uniclass.Confidence");
                            if (pConf != null && !pConf.IsReadOnly)
                            {
                                pConf.Set(kvp.Value.confidence);
                            }

                            // Parâmetro Nível (agora Granularity)
                            Parameter pLevel = elem.LookupParameter("Classification.Uniclass.Granularity");
                            if (pLevel != null && !pLevel.IsReadOnly)
                            {
                                // Map level_1 to "Group" and level_2 to "Subgroup"
                                string granularityValue = kvp.Value.level switch
                                {
                                    "level_1" => "Group",
                                    "level_2" => "Subgroup",
                                    "none" => "Unclassified",
                                    _ => kvp.Value.level
                                };
                                pLevel.Set(granularityValue);
                            }
                        }
                    }

                    tx.Commit();
                }

                stopwatch.Stop();

                // Calcular estatísticas
                int total = classificationResults.Count;
                int nUnclassified = classificationResults.Count(r => r.Value.level == "none");
                int nLevel1 = classificationResults.Count(r => r.Value.level == "level_1");
                int nLevel2 = classificationResults.Count(r => r.Value.level == "level_2");

                // Show results window in a loop to allow reclassification
                bool shouldReclassify = true;
                while (shouldReclassify)
                {
                    var resultsWindow = new ClassificationResultsWindow(
                        doc,
                        classificationResults,
                        total,
                        nUnclassified,
                        nLevel1,
                        nLevel2,
                        level1Threshold,
                        level2Threshold,
                        stopwatch.Elapsed.TotalSeconds
                    );
                    
                    bool? resultsDialogResult = resultsWindow.ShowDialog();
                    
                    // Check if user wants to reclassify
                    if (resultsDialogResult == true && resultsWindow.Tag is ReclassifyRequest reclassifyReq)
                    {
                        // User clicked "Back to Configuration" and set new thresholds
                        level1Threshold = reclassifyReq.Level1Threshold;
                        level2Threshold = reclassifyReq.Level2Threshold;
                        
                        // Restart timer for new classification
                        stopwatch.Restart();
                        
                        // Re-run Python script with new thresholds
                        string level1ThresholdStr2 = level1Threshold.ToString("0.00", CultureInfo.InvariantCulture);
                        string level2ThresholdStr2 = level2Threshold.ToString("0.00", CultureInfo.InvariantCulture);
                        
                        var psi2 = new ProcessStartInfo
                        {
                            FileName = pythonExe,
                            Arguments = $"\"{scriptPath}\" \"{csvPath}\" \"{jsonPath}\" \"{modelDir}\" {level1ThresholdStr2} {level2ThresholdStr2}",
                            WorkingDirectory = dllDir,  // Definir o diretório de trabalho
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(psi2))
                        {
                            string stdout = process.StandardOutput.ReadToEnd();
                            string stderr = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (process.ExitCode != 0)
                            {
                                TaskDialog.Show("Erro no script", stderr);
                                return Result.Failed;
                            }
                        }
                        
                        // Read new classifications
                        classificationResults = JsonConvert.DeserializeObject<Dictionary<string, ClassificationResult>>(File.ReadAllText(classifiedPath));
                        
                        // Apply new classifications
                        using (Transaction tx2 = new Transaction(doc, "Aplicar Classificação Uniclass (Reclassify)"))
                        {
                            tx2.Start();

                            foreach (var kvp in classificationResults)
                            {
                                if (int.TryParse(kvp.Key, out int eid))
                                {
                                    Element elem = doc.GetElement(new ElementId(eid));
                                    if (elem == null) continue;

                                    // Se não foi classificado, pula
                                    if (kvp.Value.level == "none")
                                        continue;

                                    // Parâmetro Uniclass Code
                                    Parameter pUniclass = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                                    if (pUniclass != null && !pUniclass.IsReadOnly)
                                    {
                                        pUniclass.Set(kvp.Value.classification);
                                    }

                                    // Parâmetro Uniclass Description (VLOOKUP)
                                    Parameter pDescription = elem.LookupParameter("Classification.Uniclass.Ss.Description");
                                    if (pDescription != null && !pDescription.IsReadOnly)
                                    {
                                        string description = UniclassLookup.GetDescription(kvp.Value.classification);
                                        if (!string.IsNullOrEmpty(description))
                                        {
                                            pDescription.Set(description);
                                        }
                                    }

                                    // Parâmetro Confiança
                                    Parameter pConf = elem.LookupParameter("Classification.Uniclass.Confidence");
                                    if (pConf != null && !pConf.IsReadOnly)
                                    {
                                        pConf.Set(kvp.Value.confidence);
                                    }

                                    // Parâmetro Nível (agora Granularity)
                                    Parameter pLevel = elem.LookupParameter("Classification.Uniclass.Granularity");
                                    if (pLevel != null && !pLevel.IsReadOnly)
                                    {
                                        // Map level_1 to "Group" and level_2 to "Subgroup"
                                        string granularityValue = kvp.Value.level switch
                                        {
                                            "level_1" => "Group",
                                            "level_2" => "Subgroup",
                                            "none" => "Unclassified",
                                            _ => kvp.Value.level
                                        };
                                        pLevel.Set(granularityValue);
                                    }
                                }
                            }

                            tx2.Commit();
                        }
                        
                        stopwatch.Stop();
                        
                        // Recalculate statistics
                        total = classificationResults.Count;
                        nUnclassified = classificationResults.Count(r => r.Value.level == "none");
                        nLevel1 = classificationResults.Count(r => r.Value.level == "level_1");
                        nLevel2 = classificationResults.Count(r => r.Value.level == "level_2");
                        
                        // Continue loop to show new results
                    }
                    else
                    {
                        // User closed window normally, exit loop
                        shouldReclassify = false;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string tempDir = Path.Combine(dllDir, "Temp");
                string scriptPath = Path.Combine(dllDir, "Scripts", "predict_and_return.py");
                string pythonExe = @"C:\Users\Admin\anaconda3\envs\Pyrevit\python.exe";

                string detalhesErro = $"Erro: {ex.Message}\n\n" +
                                      $"Python exe: {pythonExe} (Exists: {File.Exists(pythonExe)})\n" +
                                      $"Script: {scriptPath} (Exists: {File.Exists(scriptPath)})\n" +
                                      $"DLL Dir: {dllDir}\n" +
                                      $"Temp Dir: {tempDir}\n\n" +
                                      $"Stack Trace:\n{ex.StackTrace}";

                TaskDialog.Show("Erro no Classifier", detalhesErro);
                return Result.Failed;
            }
        }

        private List<Dictionary<string, string>> ExportElementData(Document doc)
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_CurtainWallPanels,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_StructuralFoundation
            };

            var exportData = new List<Dictionary<string, string>>();

            foreach (var bic in categories)
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var elem in collector)
                {
                    var type = doc.GetElement(elem.GetTypeId()) as ElementType;
                    if (type == null) continue;

                    var bbox = elem.get_BoundingBox(null);
                    var coords = GetElementCoordinates(elem);
                    var centroid = GetCentroid(elem);
                    

                    var data = new Dictionary<string, string>
                    {
                        ["ElementID"] = elem.Id.IntegerValue.ToString(),
                        ["Family and Type"] = FormatString($"{type.FamilyName} - {type.Name}"),
                        ["Category"] = FormatString(elem.Category?.Name),
                        ["Volume"] = GetDouble(elem, BuiltInParameter.HOST_VOLUME_COMPUTED, Ft3ToM3),
                        ["Area"] = GetDouble(elem, BuiltInParameter.HOST_AREA_COMPUTED, Ft2ToM2),
                        ["load_bearing_status"] = FormatString(GetStructuralStatus(elem)),
                        ["Length"] = GetDouble(elem, BuiltInParameter.CURVE_ELEM_LENGTH, FeetToMeter),
                        ["Height"] = TryGetHeight(elem, type),
                        ["Thickness/Width"] = TryGetWidthOrThickness(elem, type),
                        ["Total_Surface_Area"] = bbox != null ? FormatDouble((bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * Ft2ToM2) : "None",
                        ["Base_constraint"] = GetParamValue(elem, "Base Constraint"),
                        ["Top_constraint"] = GetParamValue(elem, "Top Constraint"),
                        ["Base_offset"] = GetParamValue(elem, "Base Offset"),
                        ["Top_offset"] = GetParamValue(elem, "Top Offset"),
                        ["Number_of_Faces"] = GetNumberOfFaces(elem),
                        ["Level"] = GetParamValue(elem, "Level"),
                        ["Phase_Created"] = GetParamValue(elem, "Phase Created"),
                        ["Start_X"] = FormatNullable(coords.startX),
                        ["Start_Y"] = FormatNullable(coords.startY),
                        ["Start_Z"] = FormatNullable(coords.startZ),
                        ["End_X"] = FormatNullable(coords.endX),
                        ["End_Y"] = FormatNullable(coords.endY),
                        ["End_Z"] = FormatNullable(coords.endZ),
                        ["Orientation_Angle"] = GetOrientationAngle(elem),
                        ["Curvature"] = GetCurvature(elem),
                        ["Bounding_Box_Width"] = bbox != null ? FormatDouble((bbox.Max.X - bbox.Min.X) * FeetToMeter) : "None",
                        ["Bounding_Box_Height"] = bbox != null ? FormatDouble((bbox.Max.Z - bbox.Min.Z) * FeetToMeter) : "None",
                        ["Bounding_Box_Depth"] = bbox != null ? FormatDouble((bbox.Max.Y - bbox.Min.Y) * FeetToMeter) : "None",
                        ["Centroid_X"] = centroid.x,
                        ["Centroid_Y"] = centroid.y,
                        ["Centroid_Z"] = centroid.z,
                        ["Materials"] = FormatString(string.Join(" | ", elem.GetMaterialIds(false).Select(id => doc.GetElement(id)?.Name).Where(n => !string.IsNullOrEmpty(n))))
                    };

                    exportData.Add(data);
                }
            }

            return exportData;
        }

        private (string x, string y, string z) GetCentroid(Element elem)
        {
            try
            {
                Options options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geomElement = elem.get_Geometry(options);

                if (geomElement == null) return ("None", "None", "None");

                foreach (GeometryObject geomObj in geomElement)
                {
                    Solid solid = geomObj as Solid;
                    if (solid != null && solid.Volume > 0)
                    {
                        var centroid = solid.ComputeCentroid();
                        return (FormatDouble(centroid.X * FeetToMeter), FormatDouble(centroid.Y * FeetToMeter), FormatDouble(centroid.Z * FeetToMeter));
                    }
                }
            }
            catch { }
            return ("None", "None", "None");
        }

        private string GetDouble(Element elem, BuiltInParameter bip, double factor)
        {
            var p = elem.get_Parameter(bip);
            return (p != null && p.StorageType == StorageType.Double) ? FormatDouble(p.AsDouble() * factor) : "None";
        }

        private string GetParamValue(Element elem, string name) => FormatString(elem.LookupParameter(name)?.AsValueString());

        private string TryGetHeight(Element elem, ElementType type)
        {
            var names = new[] { "Height", "Unconnected Height", "Altura", "Head Height" };
            foreach (var n in names)
            {
                var p = elem.LookupParameter(n) ?? type.LookupParameter(n);
                if (p != null && p.StorageType == StorageType.Double)
                    return FormatDouble(p.AsDouble() * FeetToMeter);
            }
            return "None";
        }

        private string TryGetWidthOrThickness(Element elem, ElementType type)
        {
            var names = new[] { "Width", "Thickness" };
            foreach (var n in names)
            {
                var p = elem.LookupParameter(n) ?? type.LookupParameter(n);
                if (p != null && p.StorageType == StorageType.Double)
                    return FormatDouble(p.AsDouble() * FeetToMeter);
            }
            return "None";
        }

        private string GetStructuralStatus(Element elem)
        {
            try
            {
                var cat = elem.Category.Name;
                BuiltInParameter bip = cat switch
                {
                    "Walls" => BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT,
                    "Floors" => BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL,
                    _ => 0
                };
                var p = elem.get_Parameter(bip);
                return p?.AsInteger() == 1 ? "Load-Bearing" : "Non Load-Bearing";
            }
            catch { return "None"; }
        }

        private string GetNumberOfFaces(Element elem)
        {
            try
            {
                Options options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geomElement = elem.get_Geometry(options);

                if (geomElement == null) return "None";

                int faceCount = 0;

                foreach (GeometryObject geomObj in geomElement)
                {
                    if (geomObj is Solid solid && solid.Faces != null)
                        faceCount += solid.Faces.Size;
                    else if (geomObj is GeometryInstance instance)
                    {
                        GeometryElement instGeom = instance.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Solid instSolid && instSolid.Faces != null)
                                faceCount += instSolid.Faces.Size;
                        }
                    }
                }

                return faceCount > 0 ? faceCount.ToString() : "None";
            }
            catch
            {
                return "None";
            }
        }
        private string GetOrientationAngle(Element elem)
        {
            try
            {
                if (elem.Location is LocationCurve lc)
                {
                    XYZ start = lc.Curve.GetEndPoint(0);
                    XYZ end = lc.Curve.GetEndPoint(1);
                    XYZ direction = (end - start).Normalize();
                    double angleRad = Math.Atan2(direction.Y, direction.X); // ângulo no plano XY
                    double angleDeg = angleRad * (180.0 / Math.PI);
                    return FormatDouble(angleDeg);
                }
            }
            catch { }

            return "None";
        }
        private string GetCurvature(Element elem)
        {
            try
            {
                if (elem.Location is LocationCurve lc)
                {
                    Curve curve = lc.Curve;
                    return curve.IsCyclic ? "Cyclic" : curve.IsBound ? "Bound" : "Unbound";
                }
            }
            catch { }
            return "None";
        }
        private string FormatDouble(double value) => value.ToString(CultureInfo.InvariantCulture);
        private string FormatNullable(string value) => string.IsNullOrWhiteSpace(value) ? "None" : value;
        private string FormatString(string value) => string.IsNullOrWhiteSpace(value) ? "None" : value;
        private static string SanitizeCsv(string input) => $"\"{input?.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"") ?? "None"}\"";

        private (string startX, string startY, string startZ, string endX, string endY, string endZ) GetElementCoordinates(Element elem)
        {
            var loc = elem.Location;
            if (loc is LocationPoint lp)
            {
                var pt = lp.Point;
                return (FormatDouble(pt.X * FeetToMeter), FormatDouble(pt.Y * FeetToMeter), FormatDouble(pt.Z * FeetToMeter), "None", "None", "None");
            }
            if (loc is LocationCurve lc)
            {
                var start = lc.Curve.GetEndPoint(0);
                var end = lc.Curve.GetEndPoint(1);
                return (FormatDouble(start.X * FeetToMeter), FormatDouble(start.Y * FeetToMeter), FormatDouble(start.Z * FeetToMeter),
                        FormatDouble(end.X * FeetToMeter), FormatDouble(end.Y * FeetToMeter), FormatDouble(end.Z * FeetToMeter));
            }
            return ("None", "None", "None", "None", "None", "None");
        }
    } // <--- ESTA CHAVE FECHA A CLASSIFIERCOMMAND
} // <--- ESTA CHAVE FECHA O NAMESPACE

































































































































































































































































































































































































































































































































