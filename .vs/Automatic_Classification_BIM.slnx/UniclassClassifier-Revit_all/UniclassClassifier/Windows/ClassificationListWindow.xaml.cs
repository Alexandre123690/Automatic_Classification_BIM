using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using UniclassClassifier.Utilities;

namespace UniclassClassifier.Windows
{
    public partial class ClassificationListWindow : Window
    {
        private ObservableCollection<ClassifiedElementItem> _allElements;
        private ObservableCollection<ClassifiedElementItem> _filteredElements;
        private Document _doc;

        public ClassificationListWindow(Document doc, Dictionary<string, ClassificationResult> classificationResults = null)
        {
            // Inicializar coleções ANTES do InitializeComponent
            _allElements = new ObservableCollection<ClassifiedElementItem>();
            _filteredElements = new ObservableCollection<ClassifiedElementItem>();
            _doc = doc;
            
            InitializeComponent();
            
            try
            {
                // Load elements from current model state (not from static results)
                LoadElementsFromModel();
                ElementsDataGrid.ItemsSource = _filteredElements;
                UpdateCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading elements:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void LoadElementsFromModel()
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

            foreach (var bic in categories)
            {
                var collector = new FilteredElementCollector(_doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var elem in collector)
                {
                    try
                    {
                        var type = _doc.GetElement(elem.GetTypeId()) as ElementType;
                        if (type == null) continue;

                        // Read CURRENT classification from parameters
                        string classification = "-";
                        string description = "-";
                        string confidence = "-";
                        string level = "Unclassified";

                        // Try to read from Instance parameters first
                        Parameter pCode = elem.LookupParameter("Classification.Uniclass.Ss.Number");
                        if (pCode != null && pCode.HasValue && !string.IsNullOrEmpty(pCode.AsString()))
                        {
                            classification = pCode.AsString();
                        }

                        Parameter pDesc = elem.LookupParameter("Classification.Uniclass.Ss.Description");
                        if (pDesc != null && pDesc.HasValue && !string.IsNullOrEmpty(pDesc.AsString()))
                        {
                            description = pDesc.AsString();
                        }
                        else if (classification != "-")
                        {
                            // Try to get description from lookup
                            string desc = UniclassLookup.GetDescription(classification);
                            if (!string.IsNullOrEmpty(desc))
                            {
                                description = desc;
                            }
                        }

                        Parameter pConf = elem.LookupParameter("Classification.Uniclass.Confidence");
                        if (pConf != null && pConf.HasValue)
                        {
                            confidence = $"{(pConf.AsDouble() * 100):F1}%";
                        }

                        Parameter pLevel = elem.LookupParameter("Classification.Uniclass.Granularity");
                        if (pLevel != null && pLevel.HasValue && !string.IsNullOrEmpty(pLevel.AsString()))
                        {
                            level = pLevel.AsString();
                        }

                        // If no instance classification, try Type parameters
                        if (classification == "-")
                        {
                            Parameter pTypeCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                            if (pTypeCode != null && pTypeCode.HasValue && !string.IsNullOrEmpty(pTypeCode.AsString()))
                            {
                                classification = pTypeCode.AsString() + " (Type)";
                                
                                Parameter pTypeDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                                if (pTypeDesc != null && pTypeDesc.HasValue)
                                {
                                    description = pTypeDesc.AsString();
                                }

                                Parameter pTypeGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                                if (pTypeGran != null && pTypeGran.HasValue)
                                {
                                    level = pTypeGran.AsString() + " (Type)";
                                }

                                Parameter pTypeConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                                if (pTypeConf != null && pTypeConf.HasValue)
                                {
                                    confidence = $"{(pTypeConf.AsDouble() * 100):F1}% (Type Avg)";
                                }
                            }
                        }

                        var item = new ClassifiedElementItem
                        {
                            ElementId = elem.Id.IntegerValue.ToString(),
                            Category = elem.Category?.Name ?? "Unknown",
                            FamilyType = $"{type.FamilyName} - {type.Name}",
                            Classification = classification,
                            Description = description,
                            Confidence = confidence,
                            Level = level,
                            LevelRaw = level.Contains("Subgroup") ? "level_2" : 
                                      level.Contains("Group") ? "level_1" : "none"
                        };

                        _allElements.Add(item);
                        _filteredElements.Add(item);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with other elements
                        System.Diagnostics.Debug.WriteLine($"Error loading element {elem.Id}: {ex.Message}");
                    }
                }
            }
        }

        // Keep old method for backward compatibility but mark as obsolete
        [Obsolete("Use LoadElementsFromModel() instead")]
        private void LoadElements(Dictionary<string, ClassificationResult> classificationResults)
        {
            foreach (var kvp in classificationResults)
            {
                try
                {
                    if (int.TryParse(kvp.Key, out int eid))
                    {
                        Element elem = _doc.GetElement(new ElementId(eid));
                        if (elem == null) continue;

                        var type = _doc.GetElement(elem.GetTypeId()) as ElementType;
                        if (type == null) continue;

                        string category = elem.Category?.Name ?? "Unknown";
                        string familyType = $"{type.FamilyName} - {type.Name}";
                        string classification = kvp.Value.classification == "unclassified" ? "-" : kvp.Value.classification;
                        string confidence = $"{(kvp.Value.confidence * 100):F1}%";
                        string level = kvp.Value.level switch
                        {
                            "level_2" => "Subgroup",
                            "level_1" => "Group",
                            "none" => "Unclassified",
                            _ => kvp.Value.level
                        };
                        
                        // Get description using UniclassLookup
                        string description = "-";
                        if (kvp.Value.classification != "unclassified" && !string.IsNullOrEmpty(kvp.Value.classification))
                        {
                            string desc = UniclassLookup.GetDescription(kvp.Value.classification);
                            if (!string.IsNullOrEmpty(desc))
                            {
                                description = desc;
                            }
                        }

                        var item = new ClassifiedElementItem
                        {
                            ElementId = eid.ToString(),
                            Category = category,
                            FamilyType = familyType,
                            Classification = classification,
                            Description = description,
                            Confidence = confidence,
                            Level = level,
                            LevelRaw = kvp.Value.level
                        };

                        _allElements.Add(item);
                        _filteredElements.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue with other elements
                    System.Diagnostics.Debug.WriteLine($"Error loading element {kvp.Key}: {ex.Message}");
                }
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Verificar se as coleções já foram inicializadas
                if (_filteredElements == null || _allElements == null || FilterComboBox?.SelectedItem == null) 
                    return;

                var selectedItem = FilterComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null) return;

                string filter = selectedItem.Content?.ToString();
                if (string.IsNullOrEmpty(filter)) return;

                _filteredElements.Clear();

                IEnumerable<ClassifiedElementItem> filtered = filter switch
                {
                    "Subgroup Only" => _allElements.Where(x => x.LevelRaw == "level_2" || x.Level.Contains("Subgroup")),
                    "Group Only" => _allElements.Where(x => x.LevelRaw == "level_1" || (x.Level.Contains("Group") && !x.Level.Contains("Subgroup"))),
                    "Unclassified Only" => _allElements.Where(x => x.LevelRaw == "none" || x.Level.Contains("Unclassified")),
                    _ => _allElements
                };

                foreach (var item in filtered)
                {
                    _filteredElements.Add(item);
                }

                UpdateCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error filtering elements:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void UpdateCount()
        {
            // Verificar se CountText e _filteredElements estão inicializados
            if (CountText != null && _filteredElements != null)
            {
                CountText.Text = $"Total: {_filteredElements.Count}";
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Export Classified Elements",
                FileName = "classified_elements.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(saveFileDialog.FileName, false, new UTF8Encoding(true)))
                    {
                        // Header
                        writer.WriteLine("ElementID,Category,Family and Type,Classification Code,Description,Confidence,Granularity");

                        // Data
                        foreach (var item in _filteredElements)
                        {
                            writer.WriteLine($"\"{item.ElementId}\",\"{item.Category}\",\"{item.FamilyType}\",\"{item.Classification}\",\"{item.Description}\",\"{item.Confidence}\",\"{item.Level}\"");
                        }
                    }

                    MessageBox.Show($"Successfully exported {_filteredElements.Count} elements to:\n{saveFileDialog.FileName}", 
                                    "Export Successful", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting file:\n{ex.Message}", 
                                    "Export Error", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class ClassifiedElementItem : INotifyPropertyChanged
    {
        private string _elementId;
        private string _category;
        private string _familyType;
        private string _classification;
        private string _description;
        private string _confidence;
        private string _level;
        private string _levelRaw;

        public string ElementId
        {
            get => _elementId;
            set { _elementId = value; OnPropertyChanged(nameof(ElementId)); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public string FamilyType
        {
            get => _familyType;
            set { _familyType = value; OnPropertyChanged(nameof(FamilyType)); }
        }

        public string Classification
        {
            get => _classification;
            set { _classification = value; OnPropertyChanged(nameof(Classification)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string Confidence
        {
            get => _confidence;
            set { _confidence = value; OnPropertyChanged(nameof(Confidence)); }
        }

        public string Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(nameof(Level)); }
        }

        public string LevelRaw
        {
            get => _levelRaw;
            set { _levelRaw = value; OnPropertyChanged(nameof(LevelRaw)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
