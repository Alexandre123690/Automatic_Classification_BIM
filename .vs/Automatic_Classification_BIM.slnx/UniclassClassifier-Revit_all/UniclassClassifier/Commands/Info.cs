using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class InfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Create a professional TaskDialog for Release Notes
                TaskDialog td = new TaskDialog("Uniclass AI Classifier - Information");
                
                // Main content
                td.MainInstruction = "Uniclass AI Classifier";
                td.MainContent = "AI-powered automatic classification system for BIM elements using Uniclass codes.";
                
                // Expanded content with release notes and current capabilities
                td.ExpandedContent = 
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    "📋 CURRENT VERSION: 1.0.0 (2024)\n" +
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    
                    "🔹 CURRENT CAPABILITIES:\n\n" +
                    
                    "  • Uniclass Table: Systems (Ss)\n" +
                    "  • Classification Levels:\n" +
                    "    └─ Group (Level 1): Ss_XX\n" +
                    "    └─ Subgroup (Level 2): Ss_XX_XX\n\n" +
                    
                    "  • AI Models: Hierarchical Machine Learning\n" +
                    "    └─ Levels 1-2: Geometry + Location Features\n" +
                    "    └─ Level 3+: Geometry + Location + Semantics\n\n" +
                    
                    "  • Interoperable Features Extracted:\n" +
                    "    ✓ Geometry: Volume, Area, Dimensions, Faces\n" +
                    "    ✓ Location: Coordinates, Orientation, Centroid\n" +
                    "    ✓ Properties: Material, Load-bearing, Constraints\n" +
                    "    ✓ Semantics: Family Type, Category, Context\n\n" +
                    
                    "  • Supported Categories:\n" +
                    "    └─ Walls, Floors, Roofs\n" +
                    "    └─ Structural Columns & Framing\n" +
                    "    └─ Doors, Windows\n" +
                    "    └─ Stairs, Foundations\n" +
                    "    └─ Curtain Wall Panels\n\n" +
                    
                    "  • Features:\n" +
                    "    ✓ Automatic AI classification\n" +
                    "    ✓ Confidence scoring (>50% threshold)\n" +
                    "    ✓ Type-level consolidation\n" +
                    "    ✓ Instance synchronization\n" +
                    "    ✓ Custom code support\n" +
                    "    ✓ Description lookup (VLOOKUP)\n\n" +
                    
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    "🚀 FUTURE ROADMAP:\n" +
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    
                    "  📌 Version 2.0 (Planned):\n" +
                    "    • Section Level: Ss_XX_XX_XX\n" +
                    "      └─ Enhanced with semantic features\n" +
                    "      └─ Context-aware classification\n" +
                    "    • Object Level: Ss_XX_XX_XX_XX\n" +
                    "    • Sub-object Level: Ss_XX_XX_XX_XX_XX\n\n" +
                    
                    "  📌 Version 3.0 (Future):\n" +
                    "    • Additional Uniclass Tables (Pr, EF, etc.)\n" +
                    "    • Multi-table classification\n" +
                    "    • Enhanced semantic models\n" +
                    "    • Deep learning integration\n" +
                    "    • Batch processing optimization\n\n" +
                    
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    "ℹ️  TECHNICAL INFORMATION:\n" +
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    
                    "  • AI Engine: Hierarchical Machine Learning\n" +
                    "  • Algorithm: scikit-learn ensemble methods\n" +
                    "  • Feature Engineering: Interoperable BIM data\n" +
                    "  • Training Data: IFC-compliant features\n" +
                    "  • Framework: .NET Framework 4.8\n" +
                    "  • Platform: Autodesk Revit 2023+\n" +
                    "  • Data Format: Uniclass 2015\n" +
                    "  • Parameters: Shared Parameters\n\n" +
                    
                    "  Feature Extraction Pipeline:\n" +
                    "    1. Geometric Features (40+ attributes)\n" +
                    "       └─ Dimensions, volumes, areas, ratios\n" +
                    "    2. Spatial Features (15+ attributes)\n" +
                    "       └─ Coordinates, orientation, position\n" +
                    "    3. Semantic Features (20+ attributes)\n" +
                    "       └─ Type names, categories, materials\n" +
                    "    4. Hierarchical Classification\n" +
                    "       └─ Level 1 → Level 2 → Level 3+\n\n" +
                    
                    "  Shared Parameters Created:\n" +
                    "    • Classification.Uniclass.Ss.Number\n" +
                    "    • Classification.Uniclass.Ss.Description\n" +
                    "    • Classification.Uniclass.Confidence\n" +
                    "    • Classification.Uniclass.Granularity\n" +
                    "    • Type.Classification.Uniclass.Ss.Number\n" +
                    "    • Type.Classification.Uniclass.Ss.Description\n" +
                    "    • Type.Classification.Uniclass.Granularity\n" +
                    "    • Type.Classification.Uniclass.AverageConfidence\n\n" +
                    
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    "📞 SUPPORT & CONTACT:\n" +
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    
                    "  For questions, issues, or feature requests:\n" +
                    "  Please contact your BIM administrator.\n\n" +
                    
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    "© 2024 Uniclass AI Classifier. All rights reserved.\n" +
                    "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
                
                // Footer with version
                td.FooterText = "Version 1.0.0 | Uniclass 2015 - Systems (Ss) Table";
                
                // Common buttons
                td.CommonButtons = TaskDialogCommonButtons.Close;
                
                // Add command links for quick actions
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, 
                    "View Documentation", 
                    "Learn more about Uniclass classification and how to use this tool");
                
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, 
                    "Check for Updates", 
                    "View available updates and new features");
                
                // Icons and settings
                td.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                td.TitleAutoPrefix = false;
                td.AllowCancellation = true;
                td.EnableMarqueeProgressBar = false;
                
                // Show the dialog
                TaskDialogResult result = td.Show();
                
                // Handle command link responses
                if (result == TaskDialogResult.CommandLink1)
                {
                    TaskDialog.Show("Documentation", 
                        "Documentation is being prepared.\n\n" +
                        "For now, please refer to:\n" +
                        "• Uniclass 2015 specification\n" +
                        "• Internal BIM standards\n" +
                        "• System administrator");
                }
                else if (result == TaskDialogResult.CommandLink2)
                {
                    TaskDialog.Show("Updates", 
                        "You are running the latest version (1.0.0).\n\n" +
                        "Future updates will include:\n" +
                        "• Section-level classification (Ss_XX_XX_XX)\n" +
                        "• Object and Sub-object levels\n" +
                        "• Additional Uniclass tables\n\n" +
                        "Check back for new features!");
                }
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", 
                    $"An error occurred displaying information:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}