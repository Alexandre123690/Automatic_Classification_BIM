using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ClearCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Confirm action
                TaskDialogResult confirm = TaskDialog.Show(
                    "Clear Classifications",
                    "?? WARNING\n\n" +
                    "This will remove ALL Uniclass classification data from:\n\n" +
                    "  • Instance parameters\n" +
                    "  • Type parameters\n\n" +
                    "This action cannot be undone!\n\n" +
                    "Do you want to continue?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    TaskDialogResult.No
                );

                if (confirm != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }

                // Get categories
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

                int instancesCleared = 0;
                int typesCleared = 0;
                HashSet<int> clearedTypes = new HashSet<int>();

                using (Transaction tx = new Transaction(doc, "Clear Uniclass Classifications"))
                {
                    tx.Start();

                    foreach (var bic in categories)
                    {
                        var collector = new FilteredElementCollector(doc)
                            .OfCategory(bic)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        foreach (var elem in collector)
                        {
                            try
                            {
                                // Clear instance parameters
                                Parameter pCode = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                                if (pCode != null && !pCode.IsReadOnly && pCode.HasValue)
                                {
                                    pCode.Set(string.Empty);
                                }

                                Parameter pDesc = elem.LookupParameter("Classification.Uniclass.Ss.Description");
                                if (pDesc != null && !pDesc.IsReadOnly && pDesc.HasValue)
                                {
                                    pDesc.Set(string.Empty);
                                }

                                Parameter pConf = elem.LookupParameter("Classification.Uniclass.Confidence");
                                if (pConf != null && !pConf.IsReadOnly && pConf.HasValue)
                                {
                                    pConf.Set(0.0);
                                }

                                Parameter pGran = elem.LookupParameter("Classification.Uniclass.Granularity");
                                if (pGran != null && !pGran.IsReadOnly && pGran.HasValue)
                                {
                                    pGran.Set(string.Empty);
                                }

                                instancesCleared++;

                                // Clear type parameters
                                int typeId = elem.GetTypeId().IntegerValue;
                                if (!clearedTypes.Contains(typeId))
                                {
                                    ElementType type = doc.GetElement(new ElementId(typeId)) as ElementType;
                                    if (type != null)
                                    {
                                        Parameter pTypeCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                                        if (pTypeCode != null && !pTypeCode.IsReadOnly && pTypeCode.HasValue)
                                        {
                                            pTypeCode.Set(string.Empty);
                                        }

                                        Parameter pTypeDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                                        if (pTypeDesc != null && !pTypeDesc.IsReadOnly && pTypeDesc.HasValue)
                                        {
                                            pTypeDesc.Set(string.Empty);
                                        }

                                        Parameter pTypeGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                                        if (pTypeGran != null && !pTypeGran.IsReadOnly && pTypeGran.HasValue)
                                        {
                                            pTypeGran.Set(string.Empty);
                                        }

                                        Parameter pTypeConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                                        if (pTypeConf != null && !pTypeConf.IsReadOnly && pTypeConf.HasValue)
                                        {
                                            pTypeConf.Set(0.0);
                                        }

                                        clearedTypes.Add(typeId);
                                        typesCleared++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error clearing element {elem.Id}: {ex.Message}");
                            }
                        }
                    }

                    tx.Commit();
                }

                // Show success message
                TaskDialog.Show("Clear Complete",
                    $"? Successfully cleared classifications!\n\n" +
                    $"Instances Cleared: {instancesCleared}\n" +
                    $"Types Cleared: {typesCleared}\n\n" +
                    $"All Uniclass parameters have been reset.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Clear Error",
                    $"An error occurred clearing classifications:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
