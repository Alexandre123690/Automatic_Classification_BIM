using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using UniclassClassifier.Utilities;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ValidateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Get all classified elements
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

                int totalElements = 0;
                int classifiedElements = 0;
                int validCodes = 0;
                int invalidCodes = 0;
                List<string> invalidCodesList = new List<string>();

                foreach (var bic in categories)
                {
                    var collector = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    totalElements += collector.Count;

                    foreach (var elem in collector)
                    {
                        // Check instance parameter
                        Parameter pCode = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                        
                        if (pCode != null && pCode.HasValue && !string.IsNullOrEmpty(pCode.AsString()))
                        {
                            string code = pCode.AsString();
                            classifiedElements++;

                            // Validate against Uniclass table
                            string description = UniclassLookup.GetDescription(code);
                            
                            if (!string.IsNullOrEmpty(description))
                            {
                                validCodes++;
                            }
                            else
                            {
                                invalidCodes++;
                                if (!invalidCodesList.Contains(code))
                                {
                                    invalidCodesList.Add(code);
                                }
                            }
                        }
                    }
                }

                // Build report
                string report = "??????????????????????????????????????\n" +
                                "UNICLASS CLASSIFICATION VALIDATION\n" +
                                "??????????????????????????????????????\n\n" +
                                
                                $"?? Total Elements: {totalElements}\n" +
                                $"?  Classified Elements: {classifiedElements}\n" +
                                $"?  Unclassified Elements: {totalElements - classifiedElements}\n\n" +
                                
                                "??????????????????????????????????????\n" +
                                "CODE VALIDATION RESULTS:\n" +
                                "??????????????????????????????????????\n\n" +
                                
                                $"? Valid Codes: {validCodes}\n" +
                                $"? Invalid Codes: {invalidCodes}\n\n";

                if (invalidCodes > 0)
                {
                    report += "??????????????????????????????????????\n" +
                              "INVALID CODES FOUND:\n" +
                              "??????????????????????????????????????\n\n";
                    
                    foreach (var code in invalidCodesList.OrderBy(c => c))
                    {
                        report += $"  • {code}\n";
                    }
                    
                    report += "\n?? These codes are not found in the Uniclass 2015\n" +
                              "   Systems (Ss) table. Please review and correct.";
                }
                else if (classifiedElements > 0)
                {
                    report += "? All classified elements have valid Uniclass codes!";
                }
                else
                {
                    report += "??  No classified elements found in the model.\n" +
                              "   Run AI Classifier first to classify elements.";
                }

                // Show results
                TaskDialog td = new TaskDialog("Uniclass Validation Results");
                td.MainInstruction = invalidCodes > 0 ? 
                    "?? Validation Issues Found" : 
                    "? Validation Successful";
                
                td.MainContent = report;
                td.CommonButtons = TaskDialogCommonButtons.Close;
                td.TitleAutoPrefix = false;
                td.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Validation Error", 
                    $"An error occurred during validation:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
