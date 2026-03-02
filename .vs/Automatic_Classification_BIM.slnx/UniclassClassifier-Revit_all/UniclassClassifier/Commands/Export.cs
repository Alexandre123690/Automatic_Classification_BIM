// ExportCommand.cs (versão final com "None" e precisão total)
// Passo 1: Imports essenciais para Revit, IO, JSON, UI e cultura
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExportCommand : IExternalCommand
    {
        // Passo 2: Constantes de conversão
        private const double FeetToMeter = 0.3048;
        private const double Ft2ToM2 = 0.092903;
        private const double Ft3ToM3 = 0.0283168;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Passo 3: Acesso ao documento ativo
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Passo 4: Diálogo para escolher local do ficheiro JSON (usando WPF SaveFileDialog)
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Escolher onde guardar o ficheiro JSON",
                FileName = "export.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveFileDialog.ShowDialog() != true)
                return Result.Cancelled;

            string jsonPath = saveFileDialog.FileName;
            string csvPath = Path.ChangeExtension(jsonPath, ".csv");

            // Passo 5: Definir categorias a exportar
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

            // Passo 6: Iterar sobre as categorias e recolher dados dos elementos
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

                    var data = new Dictionary<string, string>();
                    var coords = GetElementCoordinates(elem);
                    var bbox = elem.get_BoundingBox(null);
                    var centroid = GetCentroid(elem);

                    // Passo 7: Parâmetros básicos de identificação
                    data["ElementID"] = elem.Id.IntegerValue.ToString();
                    data["SECClass_Code_EF"] = GetParamValue(type, "ClassificacaoSecclassEFNumero");
                    data["SECClass_Title_EF"] = GetParamValue(type, "ClassificacaoSecclassEFDescricao");
                    data["SECClasS_Code_Ss"] = GetParamValue(type, "ClassificacaoSecclassSsNumero");
                    data["SECClasS_Title_Ss"] = GetParamValue(type, "ClassificacaoSecclassSsDescricao");
                    data["SECClass_Code_Pr"] = GetParamValue(type, "ClassificacaoSecclassPrNumero");
                    data["SECClass_Title_Pr"] = GetParamValue(type, "ClassificacaoSecclassPrDescricao");
                    data["Family and Type"] = FormatString($"{type.FamilyName} - {type.Name}");
                    data["Category"] = FormatString(elem.Category?.Name);
                    data["Volume"] = GetDouble(elem, BuiltInParameter.HOST_VOLUME_COMPUTED, Ft3ToM3);
                    data["Area"] = GetDouble(elem, BuiltInParameter.HOST_AREA_COMPUTED, Ft2ToM2);
                    data["Volume_to_Surface_Area_Ratio"] = "None";
                    data["load_bearing_status"] = FormatString(GetStructuralStatus(elem));
                    data["Length"] = GetDouble(elem, BuiltInParameter.CURVE_ELEM_LENGTH, FeetToMeter);
                    data["Height"] = TryGetHeight(elem, type);
                    data["Thickness/Width"] = TryGetWidthOrThickness(elem, type);
                    data["Aspect_Ratio"] = "None";
                    data["Total_Surface_Area"] = bbox != null ? FormatDouble((bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * Ft2ToM2) : "None";
                    data["Total_Edge_Length"] = "None";
                    data["Base_constraint"] = GetParamValue(elem, "Base Constraint");
                    data["Top_constraint"] = GetParamValue(elem, "Top Constraint");
                    data["Base_extension_distance"] = GetParamDouble(elem, "Base Extension Distance");
                    data["Level"] = GetParamValue(elem, "Level");
                    data["Base_level"] = GetParamValue(elem, "Base Level");
                    data["Base_offset"] = GetParamDouble(elem, "Base Offset");
                    data["Top_level"] = GetParamValue(elem, "Top Level");
                    data["Top_offset"] = GetParamDouble(elem, "Top Offset");
                    data["Number_of_Faces"] = GetNumberOfFaces(elem);
                    data["Phase_Created"] = GetParamValue(elem, "Phase Created");
                    data["Start_X"] = FormatNullable(coords.startX);
                    data["Start_Y"] = FormatNullable(coords.startY);
                    data["Start_Z"] = FormatNullable(coords.startZ);
                    data["End_X"] = FormatNullable(coords.endX);
                    data["End_Y"] = FormatNullable(coords.endY);
                    data["End_Z"] = FormatNullable(coords.endZ);
                    data["Bounding_Box_Width"] = bbox != null ? FormatDouble((bbox.Max.X - bbox.Min.X) * FeetToMeter) : "None";
                    data["Bounding_Box_Height"] = bbox != null ? FormatDouble((bbox.Max.Z - bbox.Min.Z) * FeetToMeter) : "None";
                    data["Bounding_Box_Depth"] = bbox != null ? FormatDouble((bbox.Max.Y - bbox.Min.Y) * FeetToMeter) : "None";
                    data["Centroid_X"] = centroid.x;
                    data["Centroid_Y"] = centroid.y;
                    data["Centroid_Z"] = centroid.z;
                    data["Orientation_Angle"] = GetOrientationAngle(elem);
                    data["Curvature"] = GetCurvature(elem);
                    data["Comments"] = GetParamValue(elem, "Comments");
                    data["Keynote"] = GetParamValue(type, "Keynote");
                    data["Description"] = GetParamValue(type, "Description");

                    // Passo 12: Materiais
                    data["Materials"] = FormatString(string.Join(" | ",
                        elem.GetMaterialIds(false).Select(id => doc.GetElement(id)?.Name).Where(n => !string.IsNullOrEmpty(n))));

                    exportData.Add(data);
                }
            }

            // Passo 13: Exportação para JSON
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(exportData, Formatting.Indented));

            // Passo 14: Exportação para CSV
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

            // Passo 15: Mensagem de sucesso
            TaskDialog.Show("Exportação concluída", $"JSON:\n{jsonPath}\nCSV:\n{csvPath}");
            return Result.Succeeded;
        }

        // Passo 16: Funções auxiliares
        private string GetDouble(Element elem, BuiltInParameter bip, double factor)
        {
            var p = elem.get_Parameter(bip);
            return (p != null && p.StorageType == StorageType.Double) ? FormatDouble(p.AsDouble() * factor) : "None";
        }

        private string GetParamValue(Element elem, string name) => FormatString(elem.LookupParameter(name)?.AsValueString());

        private string GetParamDouble(Element elem, string name)
        {
            var p = elem.LookupParameter(name);
            return (p != null && p.StorageType == StorageType.Double) ? FormatDouble(p.AsDouble() * FeetToMeter) : "None";
        }

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

        private static string SanitizeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "None";
            var clean = input.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
            return $"\"{clean}\"";
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

        private (string startX, string startY, string startZ, string endX, string endY, string endZ) GetElementCoordinates(Element elem)
        {
            var loc = elem.Location;
            if (loc is LocationPoint lp)
            {
                var pt = lp.Point;
                return (
                    FormatDouble(pt.X * FeetToMeter),
                    FormatDouble(pt.Y * FeetToMeter),
                    FormatDouble(pt.Z * FeetToMeter),
                    "None", "None", "None");
            }
            if (loc is LocationCurve lc)
            {
                var start = lc.Curve.GetEndPoint(0);
                var end = lc.Curve.GetEndPoint(1);
                return (
                    FormatDouble(start.X * FeetToMeter),
                    FormatDouble(start.Y * FeetToMeter),
                    FormatDouble(start.Z * FeetToMeter),
                    FormatDouble(end.X * FeetToMeter),
                    FormatDouble(end.Y * FeetToMeter),
                    FormatDouble(end.Z * FeetToMeter));
            }
            return ("None", "None", "None", "None", "None", "None");
        }
    }
}
