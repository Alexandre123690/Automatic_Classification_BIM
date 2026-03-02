using System;
using System.IO;
using Autodesk.Revit.DB;

namespace UniclassClassifier.Utilities
{
    public static class SharedParameterUtils
    {
        public static void CreateSharedParameters(Document doc)
        {
            // Get application object from document
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            // Get or create shared parameter file
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath);
            string sharedParamFile = Path.Combine(assemblyDir, "UniclassParameters.txt");

            // Create shared parameter file if it doesn't exist
            if (!File.Exists(sharedParamFile))
            {
                using (File.Create(sharedParamFile)) { }
            }

            // Set the shared parameter file
            app.SharedParametersFilename = sharedParamFile;

            // Get the shared parameter file object
            DefinitionFile defFile = app.OpenSharedParameterFile();

            // Create parameter group if it doesn't exist
            DefinitionGroup group = defFile.Groups.get_Item("UniclassClassifier");
            if (group == null)
            {
                group = defFile.Groups.Create("UniclassClassifier");
            }

            // Create category set for parameters
            CategorySet categories = app.Create.NewCategorySet();
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Walls));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Floors));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Roofs));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralColumns));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFraming));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Columns));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Doors));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_CurtainWallPanels));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Stairs));
            categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFoundation));

            // Create shared parameters (Instance level)
            CreateSharedParameter(doc, app, defFile, group, "Classification.Uniclass.Ss.Number", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, true);
            CreateSharedParameter(doc, app, defFile, group, "Classification.Uniclass.Ss.Description", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, true);
            CreateSharedParameter(doc, app, defFile, group, "Classification.Uniclass.Confidence", 
                SpecTypeId.Number, true, categories, GroupTypeId.Data, true);
            CreateSharedParameter(doc, app, defFile, group, "Classification.Uniclass.Granularity", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, true);
            
            // Create shared parameters (Type level) - for final consolidated classification
            CreateSharedParameter(doc, app, defFile, group, "Type.Classification.Uniclass.Ss.Number", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, false);
            CreateSharedParameter(doc, app, defFile, group, "Type.Classification.Uniclass.Ss.Description", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, false);
            CreateSharedParameter(doc, app, defFile, group, "Type.Classification.Uniclass.Granularity", 
                SpecTypeId.String.Text, true, categories, GroupTypeId.Data, false);
            CreateSharedParameter(doc, app, defFile, group, "Type.Classification.Uniclass.AverageConfidence", 
                SpecTypeId.Number, true, categories, GroupTypeId.Data, false);
        }

        private static void CreateSharedParameter(Document doc, Autodesk.Revit.ApplicationServices.Application app,
            DefinitionFile defFile, DefinitionGroup group, string paramName, ForgeTypeId specTypeId, 
            bool visible, CategorySet categories, ForgeTypeId groupTypeId, bool isInstance)
        {
            // Create parameter definition if it doesn't exist
            Definition definition = group.Definitions.get_Item(paramName);
            if (definition == null)
            {
                ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(paramName, specTypeId)
                {
                    Visible = visible
                };
                definition = group.Definitions.Create(options);
            }

            // Create binding (Instance or Type)
            Binding binding = isInstance 
                ? (Binding)app.Create.NewInstanceBinding(categories)
                : (Binding)app.Create.NewTypeBinding(categories);

            // Add parameter to document
            doc.ParameterBindings.Insert(definition, binding, groupTypeId);
        }
    }
}