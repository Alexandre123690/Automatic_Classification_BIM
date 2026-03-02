using System.Collections.Generic;
using System.Linq;

namespace UniclassClassifier.Windows
{
    /// <summary>
    /// Represents classification data aggregated by Family and Type
    /// </summary>
    public class FamilyTypeClassification
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string Category { get; set; }
        public int TypeId { get; set; }
        public int InstanceCount { get; set; }
        public List<string> UniqueClassifications { get; set; }
        public string MostCommonClassification { get; set; }
        public string MostCommonDescription { get; set; }
        public double AverageConfidence { get; set; }
        public string DominantGranularity { get; set; }
        public bool IsInconsistent { get; set; }
        
        // For highest confidence method
        public string HighestConfidenceClassification { get; set; }
        public string HighestConfidenceDescription { get; set; }
        public double HighestConfidenceValue { get; set; }
        public string HighestConfidenceGranularity { get; set; }
        
        public FamilyTypeClassification()
        {
            UniqueClassifications = new List<string>();
        }
        
        public string GetFamilyTypeKey()
        {
            return $"{FamilyName} - {TypeName}";
        }
    }
    
    /// <summary>
    /// Analyzes instance classifications and detects inconsistencies
    /// </summary>
    public static class ClassificationConsolidator
    {
        public static List<FamilyTypeClassification> AnalyzeByFamilyType(
            Autodesk.Revit.DB.Document doc,
            Dictionary<string, ClassificationResult> instanceResults)
        {
            var groupedByType = new Dictionary<int, List<(int instanceId, ClassificationResult result)>>();
            
            // Group instances by TypeId
            foreach (var kvp in instanceResults)
            {
                if (int.TryParse(kvp.Key, out int instanceId))
                {
                    var elem = doc.GetElement(new Autodesk.Revit.DB.ElementId(instanceId));
                    if (elem == null) continue;
                    
                    int typeId = elem.GetTypeId().IntegerValue;
                    
                    if (!groupedByType.ContainsKey(typeId))
                    {
                        groupedByType[typeId] = new List<(int, ClassificationResult)>();
                    }
                    
                    groupedByType[typeId].Add((instanceId, kvp.Value));
                }
            }
            
            // Analyze each type
            var familyTypeClassifications = new List<FamilyTypeClassification>();
            
            foreach (var kvp in groupedByType)
            {
                var typeId = kvp.Key;
                var instances = kvp.Value;
                
                var typeElement = doc.GetElement(new Autodesk.Revit.DB.ElementId(typeId)) as Autodesk.Revit.DB.ElementType;
                if (typeElement == null) continue;
                
                var firstInstance = doc.GetElement(new Autodesk.Revit.DB.ElementId(instances[0].instanceId));
                if (firstInstance == null) continue;
                
                // Get unique classifications (excluding unclassified)
                var classifiedInstances = instances.Where(i => i.result.level != "none").ToList();
                
                if (classifiedInstances.Count == 0) continue;
                
                var uniqueClassifications = classifiedInstances
                    .Select(i => i.result.classification)
                    .Distinct()
                    .ToList();
                
                // Find most common classification
                var classificationGroups = classifiedInstances
                    .GroupBy(i => i.result.classification)
                    .OrderByDescending(g => g.Count())
                    .ToList();
                
                var mostCommon = classificationGroups.First().Key;
                var avgConfidence = classifiedInstances.Average(i => i.result.confidence);
                
                // Get dominant granularity
                var granularityGroups = classifiedInstances
                    .GroupBy(i => i.result.level)
                    .OrderByDescending(g => g.Count())
                    .ToList();
                
                var dominantGranularity = granularityGroups.First().Key switch
                {
                    "level_1" => "Group",
                    "level_2" => "Subgroup",
                    _ => "Unknown"
                };
                
                // Find instance with highest confidence
                var highestConfidenceInstance = classifiedInstances
                    .OrderByDescending(i => i.result.confidence)
                    .First();
                
                var highestConfGranularity = highestConfidenceInstance.result.level switch
                {
                    "level_1" => "Group",
                    "level_2" => "Subgroup",
                    _ => "Unknown"
                };
                
                var ftc = new FamilyTypeClassification
                {
                    FamilyName = typeElement.FamilyName,
                    TypeName = typeElement.Name,
                    Category = firstInstance.Category?.Name ?? "Unknown",
                    TypeId = typeId,
                    InstanceCount = instances.Count,
                    UniqueClassifications = uniqueClassifications,
                    MostCommonClassification = mostCommon,
                    MostCommonDescription = Utilities.UniclassLookup.GetDescription(mostCommon),
                    AverageConfidence = avgConfidence,
                    DominantGranularity = dominantGranularity,
                    IsInconsistent = uniqueClassifications.Count > 1,
                    HighestConfidenceClassification = highestConfidenceInstance.result.classification,
                    HighestConfidenceDescription = Utilities.UniclassLookup.GetDescription(highestConfidenceInstance.result.classification),
                    HighestConfidenceValue = highestConfidenceInstance.result.confidence,
                    HighestConfidenceGranularity = highestConfGranularity
                };
                
                familyTypeClassifications.Add(ftc);
            }
            
            return familyTypeClassifications.OrderBy(f => f.FamilyName).ThenBy(f => f.TypeName).ToList();
        }
        
        /// <summary>
        /// Classify types by the instance with highest confidence for each type
        /// </summary>
        public static int AutoClassifyTypesByHighestConfidence(
            Autodesk.Revit.DB.Document doc,
            Dictionary<string, ClassificationResult> instanceResults)
        {
            var familyTypeClassifications = AnalyzeByFamilyType(doc, instanceResults);
            int typesClassified = 0;
            
            using (Autodesk.Revit.DB.Transaction tx = new Autodesk.Revit.DB.Transaction(doc, "Auto-Classify Types by Highest Confidence"))
            {
                tx.Start();
                
                foreach (var ftc in familyTypeClassifications)
                {
                    Autodesk.Revit.DB.ElementType type = doc.GetElement(new Autodesk.Revit.DB.ElementId(ftc.TypeId)) as Autodesk.Revit.DB.ElementType;
                    if (type == null) continue;
                    
                    // Write to Type parameters using highest confidence instance
                    Autodesk.Revit.DB.Parameter pCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                    if (pCode != null && !pCode.IsReadOnly)
                    {
                        pCode.Set(ftc.HighestConfidenceClassification);
                    }
                    
                    Autodesk.Revit.DB.Parameter pDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                    if (pDesc != null && !pDesc.IsReadOnly)
                    {
                        pDesc.Set(ftc.HighestConfidenceDescription);
                    }
                    
                    Autodesk.Revit.DB.Parameter pGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                    if (pGran != null && !pGran.IsReadOnly)
                    {
                        pGran.Set(ftc.HighestConfidenceGranularity);
                    }
                    
                    Autodesk.Revit.DB.Parameter pAvgConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                    if (pAvgConf != null && !pAvgConf.IsReadOnly)
                    {
                        pAvgConf.Set(ftc.HighestConfidenceValue);
                    }
                    
                    typesClassified++;
                }
                
                tx.Commit();
            }
            
            // Now sync instances with type parameters
            int instancesUpdated = SyncInstancesWithTypeParameters(doc, familyTypeClassifications.Select(f => f.TypeId).ToList());
            
            return typesClassified;
        }
        
        /// <summary>
        /// Synchronizes instance parameters with their type parameters
        /// </summary>
        private static int SyncInstancesWithTypeParameters(
            Autodesk.Revit.DB.Document doc,
            List<int> classifiedTypeIds)
        {
            int instancesUpdated = 0;
            
            using (Autodesk.Revit.DB.Transaction tx = new Autodesk.Revit.DB.Transaction(doc, "Sync Instance Parameters"))
            {
                tx.Start();
                
                var categories = new List<Autodesk.Revit.DB.BuiltInCategory>
                {
                    Autodesk.Revit.DB.BuiltInCategory.OST_Walls,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Floors,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Roofs,
                    Autodesk.Revit.DB.BuiltInCategory.OST_StructuralColumns,
                    Autodesk.Revit.DB.BuiltInCategory.OST_StructuralFraming,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Columns,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Doors,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Windows,
                    Autodesk.Revit.DB.BuiltInCategory.OST_CurtainWallPanels,
                    Autodesk.Revit.DB.BuiltInCategory.OST_Stairs,
                    Autodesk.Revit.DB.BuiltInCategory.OST_StructuralFoundation
                };
                
                foreach (var bic in categories)
                {
                    var collector = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    
                    foreach (var elem in collector)
                    {
                        try
                        {
                            int typeId = elem.GetTypeId().IntegerValue;
                            
                            if (!classifiedTypeIds.Contains(typeId))
                                continue;
                            
                            Autodesk.Revit.DB.ElementType type = doc.GetElement(new Autodesk.Revit.DB.ElementId(typeId)) as Autodesk.Revit.DB.ElementType;
                            if (type == null) continue;
                            
                            // Read from Type parameters
                            Autodesk.Revit.DB.Parameter pTypeCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                            if (pTypeCode == null || !pTypeCode.HasValue || string.IsNullOrEmpty(pTypeCode.AsString()))
                                continue;
                            
                            // Write to Instance parameters
                            Autodesk.Revit.DB.Parameter pInstCode = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                            if (pInstCode != null && !pInstCode.IsReadOnly)
                            {
                                pInstCode.Set(pTypeCode.AsString());
                            }
                            
                            Autodesk.Revit.DB.Parameter pTypeDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                            if (pTypeDesc != null && pTypeDesc.HasValue)
                            {
                                Autodesk.Revit.DB.Parameter pInstDesc = elem.LookupParameter("Classification.Uniclass.Ss.Description");
                                if (pInstDesc != null && !pInstDesc.IsReadOnly)
                                {
                                    pInstDesc.Set(pTypeDesc.AsString());
                                }
                            }
                            
                            Autodesk.Revit.DB.Parameter pTypeGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                            if (pTypeGran != null && pTypeGran.HasValue)
                            {
                                Autodesk.Revit.DB.Parameter pInstGran = elem.LookupParameter("Classification.Uniclass.Granularity");
                                if (pInstGran != null && !pInstGran.IsReadOnly)
                                {
                                    pInstGran.Set(pTypeGran.AsString());
                                }
                            }
                            
                            Autodesk.Revit.DB.Parameter pTypeConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                            if (pTypeConf != null && pTypeConf.HasValue)
                            {
                                Autodesk.Revit.DB.Parameter pInstConf = elem.LookupParameter("Classification.Uniclass.Confidence");
                                if (pInstConf != null && !pInstConf.IsReadOnly)
                                {
                                    pInstConf.Set(pTypeConf.AsDouble());
                                }
                            }
                            
                            instancesUpdated++;
                        }
                        catch { }
                    }
                }
                
                tx.Commit();
            }
            
            return instancesUpdated;
        }
    }
}
