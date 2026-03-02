using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using UniclassClassifier.Windows;

namespace UniclassClassifier.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ViewResultsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Simply open ClassificationListWindow to show current classifications
                var listWindow = new ClassificationListWindow(doc);
                listWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", 
                    $"An error occurred viewing results:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
