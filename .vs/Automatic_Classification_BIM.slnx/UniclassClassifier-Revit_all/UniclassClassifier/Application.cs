using System.Reflection;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using UniclassClassifier.Commands;

namespace UniclassClassifier
{
    /// <summary>
    ///     Application entry point
    /// </summary>
    [UsedImplicitly]
    public class Application : ExternalApplication
    {
        public override void OnStartup()
        {
            CreateRibbon();
        }

        private void CreateRibbon()
        {
            // Painel 1 – Classifier
            var panelClassifier = Application.CreatePanel("Classify", "UniclassClassifier");

            var classifierButton = panelClassifier.AddPushButton<ClassifierCommand>("AI Classifier")
                .SetImage("/UniclassClassifier;component/Resources/Icons/icon.png")
                .SetLargeImage("/UniclassClassifier;component/Resources/Icons/icon.png");

            classifierButton.ToolTip = "Classify BIM elements using AI.";
            classifierButton.LongDescription = "Uses trained machine learning models to automatically classify BIM elements using Uniclass codes based on geometry and metadata.";

            // Painel 2 – Tools
            var panelTools = Application.CreatePanel("Tools", "UniclassClassifier");

            // Caminho da DLL atual
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // Criar os botões para o painel Tools (3 botões empilhados)
            var viewResultsBtnData = new PushButtonData("ViewResults", "View\nResults", assemblyPath, typeof(ViewResultsCommand).FullName);
            var validateBtnData = new PushButtonData("Validate", "Validate", assemblyPath, typeof(ValidateCommand).FullName);
            var clearBtnData = new PushButtonData("Clear", "Clear All", assemblyPath, typeof(ClearCommand).FullName);

            // Adicionar os 3 botões empilhados
            var toolsButtons = panelTools.AddStackedItems(viewResultsBtnData, validateBtnData, clearBtnData);

            // Configurar botão View Results
            var viewResultsButton = (PushButton)toolsButtons[0];
            viewResultsButton.ToolTip = "View classification results.";
            viewResultsButton.LongDescription = "Opens a detailed list of all classified elements in the current model with their Uniclass codes.";
            viewResultsButton.SetImage("/UniclassClassifier;component/Resources/Icons/icon2.png");

            // Configurar botão Validate
            var validateButton = (PushButton)toolsButtons[1];
            validateButton.ToolTip = "Validate classifications.";
            validateButton.LongDescription = "Validates all Uniclass codes in the model against the official Uniclass 2015 Systems table and reports any invalid codes.";
            validateButton.SetImage("/UniclassClassifier;component/Resources/Icons/icon3.png");

            // Configurar botão Clear
            var clearButton = (PushButton)toolsButtons[2];
            clearButton.ToolTip = "Clear all classifications.";
            clearButton.LongDescription = "Removes all Uniclass classification data from both instance and type parameters. This action cannot be undone.";
            clearButton.SetImage("/UniclassClassifier;component/Resources/Icons/icon3.png");

            // Painel 3 – Data
            var panelData = Application.CreatePanel("Data", "UniclassClassifier");

            // Criar os botões Export e Info
            var exportBtnData = new PushButtonData("Export", "Export", assemblyPath, typeof(ExportCommand).FullName);
            var infoBtnData = new PushButtonData("Info", "Info", assemblyPath, typeof(InfoCommand).FullName);

            // Adicionar botões empilhados no painel Data
            var dataButtons = panelData.AddStackedItems(exportBtnData, infoBtnData);

            // Botão Export
            var exportButton = (PushButton)dataButtons[0];
            exportButton.ToolTip = "Export BIM element data.";
            exportButton.LongDescription = "Exports element geometry, classification and quantities to external files (CSV or JSON).";
            exportButton.SetImage("/UniclassClassifier;component/Resources/Icons/icon2.png");

            // Botão Info
            var infoButton = (PushButton)dataButtons[1];
            infoButton.ToolTip = "Plugin information.";
            infoButton.LongDescription = "Displays plugin details including version, authorship and reference documentation.";
            infoButton.SetImage("/UniclassClassifier;component/Resources/Icons/icon3.png");
        }
    }
}
