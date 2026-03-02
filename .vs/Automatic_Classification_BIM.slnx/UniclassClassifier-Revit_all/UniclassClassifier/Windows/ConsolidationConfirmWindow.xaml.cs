using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using UniclassClassifier.Utilities;

namespace UniclassClassifier.Windows
{
    public partial class ConsolidationConfirmWindow : Window
    {
        private Document _doc;
        private List<FamilyTypeClassification> _familyTypeClassifications;
        private ObservableCollection<ConsolidationItem> _consolidationItems;
        
        public ConsolidationConfirmWindow(Document doc, List<FamilyTypeClassification> familyTypeClassifications)
        {
            InitializeComponent();
            
            _doc = doc;
            _familyTypeClassifications = familyTypeClassifications;
            
            _consolidationItems = new ObservableCollection<ConsolidationItem>(
                familyTypeClassifications.Select(ft =>
                {
                    // Create list with all unique codes PLUS "Custom..." option
                    var codes = new List<string>(ft.UniqueClassifications);
                    codes.Add("Custom...");
                    
                    return new ConsolidationItem
                    {
                        Category = ft.Category,
                        FamilyTypeKey = ft.GetFamilyTypeKey(),
                        InstanceCount = ft.InstanceCount,
                        MostCommonClassification = ft.MostCommonClassification,
                        HighestConfidenceClassification = ft.HighestConfidenceClassification,
                        MostCommonDescription = ft.MostCommonDescription,
                        HighestConfidenceDescription = ft.HighestConfidenceDescription,
                        AverageConfidencePercent = $"{(ft.AverageConfidence * 100):F1}%",
                        SelectedCode = ft.HighestConfidenceClassification, // Default to Highest Confidence
                        CurrentDescription = ft.HighestConfidenceDescription,
                        OriginalData = ft,
                        // Populate available codes with ALL unique classifications found by the model + Custom option
                        AvailableCodes = codes
                    };
                })
            );
            
            ConsolidationDataGrid.ItemsSource = _consolidationItems;
            TypeCountText.Text = $"{familyTypeClassifications.Count} Family Types ready for consolidation";
        }
        
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Apply classifications to types based on individual selections
                using (Transaction tx = new Transaction(_doc, "Apply Type Classifications"))
                {
                    tx.Start();
                    
                    foreach (var item in _consolidationItems)
                    {
                        var ftc = item.OriginalData;
                        ElementType type = _doc.GetElement(new ElementId(ftc.TypeId)) as ElementType;
                        if (type == null) continue;
                        
                        // Use the selected code and current description
                        string classification = item.SelectedCode;
                        string description = item.CurrentDescription;
                        
                        // Determine granularity based on code
                        string granularity = DetermineGranularityFromCode(classification);
                        double confidence = ftc.AverageConfidence; // Use average for now
                        
                        // Write to Type parameters
                        Parameter pCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                        if (pCode != null && !pCode.IsReadOnly)
                        {
                            pCode.Set(classification);
                        }
                        
                        Parameter pDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                        if (pDesc != null && !pDesc.IsReadOnly)
                        {
                            pDesc.Set(description);
                        }
                        
                        Parameter pGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                        if (pGran != null && !pGran.IsReadOnly)
                        {
                            pGran.Set(granularity);
                        }
                        
                        Parameter pAvgConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                        if (pAvgConf != null && !pAvgConf.IsReadOnly)
                        {
                            pAvgConf.Set(confidence);
                        }
                    }
                    
                    tx.Commit();
                }
                
                // Now synchronize instance parameters with type parameters
                int instancesUpdated = SyncInstancesWithTypes();
                
                MessageBox.Show($"Successfully applied classifications!\n\n" +
                                $"Family Types: {_consolidationItems.Count}\n" +
                                $"Instances Updated: {instancesUpdated}",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying classifications:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
        
        private string DetermineGranularityFromCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "Unknown";
            
            // Count underscores or spaces to determine level
            int separators = code.Count(c => c == '_' || c == ' ');
            
            // Ss_25 = Group (1 separator after Ss)
            // Ss_25_30 = Subgroup (2 separators after Ss)
            // Ss_25_30_50 = Section (3 separators after Ss)
            
            if (separators >= 3)
                return "Section";
            else if (separators >= 2)
                return "Subgroup";
            else if (separators >= 1)
                return "Group";
            else
                return "Unknown";
        }
        
        /// <summary>
        /// Synchronizes instance parameters with their type parameters
        /// </summary>
        private int SyncInstancesWithTypes()
        {
            int instancesUpdated = 0;
            
            using (Transaction tx = new Transaction(_doc, "Sync Instance Parameters with Types"))
            {
                tx.Start();
                
                // Get all TypeIds that were classified
                var classifiedTypeIds = _consolidationItems.Select(item => item.OriginalData.TypeId).ToList();
                
                // Categories to process
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
                            int typeId = elem.GetTypeId().IntegerValue;
                            
                            // Only update instances whose types were classified
                            if (!classifiedTypeIds.Contains(typeId))
                                continue;
                            
                            ElementType type = _doc.GetElement(new ElementId(typeId)) as ElementType;
                            if (type == null) continue;
                            
                            // Read from Type parameters
                            Parameter pTypeCode = type.LookupParameter("Type.Classification.Uniclass.Ss.Number");
                            Parameter pTypeDesc = type.LookupParameter("Type.Classification.Uniclass.Ss.Description");
                            Parameter pTypeGran = type.LookupParameter("Type.Classification.Uniclass.Granularity");
                            Parameter pTypeConf = type.LookupParameter("Type.Classification.Uniclass.AverageConfidence");
                        
                            if (pTypeCode == null || !pTypeCode.HasValue || string.IsNullOrEmpty(pTypeCode.AsString()))
                                continue;
							
							// Write to Instance parameters
							Parameter pInstCode = elem.LookupParameter("Classification.Uniclass.Ss.Number");
							if (pInstCode != null && !pInstCode.IsReadOnly)
							{
								pInstCode.Set(pTypeCode.AsString());
							}
							
							if (pTypeDesc != null && pTypeDesc.HasValue)
							{
								Parameter pInstDesc = elem.LookupParameter("Classification.Uniclass.Ss.Description");
								if (pInstDesc != null && !pInstDesc.IsReadOnly)
								{
									pInstDesc.Set(pTypeDesc.AsString());
								}
							}
							
							if (pTypeGran != null && pTypeGran.HasValue)
							{
								Parameter pInstGran = elem.LookupParameter("Classification.Uniclass.Granularity");
								if (pInstGran != null && !pInstGran.IsReadOnly)
								{
									pInstGran.Set(pTypeGran.AsString());
								}
							}
							
							if (pTypeConf != null && pTypeConf.HasValue)
							{
								Parameter pInstConf = elem.LookupParameter("Classification.Uniclass.Confidence");
								if (pInstConf != null && !pInstConf.IsReadOnly)
								{
									pInstConf.Set(pTypeConf.AsDouble());
								}
							}
							
                            instancesUpdated++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error syncing instance {elem.Id}: {ex.Message}");
                        }
                    }
                }
                
                tx.Commit();
            }
            
            return instancesUpdated;
        }
        
        private void SetAllComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (SetAllComboBox == null || SetAllComboBox.SelectedItem == null || _consolidationItems == null)
                    return;
                
                var selectedItem = SetAllComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null || selectedItem.Tag == null)
                    return;
                
                string selection = selectedItem.Tag.ToString();
                
                // Change all selections based on dropdown
                foreach (var item in _consolidationItems)
                {
                    if (selection == "MostCommon")
                    {
                        item.SelectedCode = item.MostCommonClassification;
                        item.CurrentDescription = item.MostCommonDescription;
                    }
                    else if (selection == "HighestConfidence")
                    {
                        item.SelectedCode = item.HighestConfidenceClassification;
                        item.CurrentDescription = item.HighestConfidenceDescription;
                    }
                }
                
                // Keep current selection (don't reset)
                // User can see what was last selected
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating selections:\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Event handler for when user finishes editing a code in the ComboBox
        /// </summary>
        private void CodeComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.DataContext is ConsolidationItem item)
                {
                    // Get the text from the ComboBox (either selected or typed)
                    string enteredCode = comboBox.Text;
                    
                    if (!string.IsNullOrWhiteSpace(enteredCode) && enteredCode != "Custom...")
                    {
                        // Force update of SelectedCode property
                        item.SelectedCode = enteredCode;
                        
                        // Manually trigger description lookup
                        string description = UniclassLookup.GetDescription(enteredCode);
                        
                        if (!string.IsNullOrEmpty(description))
                        {
                            item.CurrentDescription = description;
                        }
                        else
                        {
                            item.CurrentDescription = $"[No description found for: {enteredCode}]";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CodeComboBox_LostFocus: {ex.Message}");
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
    
    public class ConsolidationItem : INotifyPropertyChanged
    {
        private string _category;
        private string _familyTypeKey;
        private int _instanceCount;
        private string _mostCommonClassification;
        private string _highestConfidenceClassification;
        private string _mostCommonDescription;
        private string _highestConfidenceDescription;
        private string _averageConfidencePercent;
        private string _selectedCode;
        private string _currentDescription;
        private List<string> _availableCodes;
        
        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }
        
        public string FamilyTypeKey
        {
            get => _familyTypeKey;
            set { _familyTypeKey = value; OnPropertyChanged(nameof(FamilyTypeKey)); }
        }
        
        public int InstanceCount
        {
            get => _instanceCount;
            set { _instanceCount = value; OnPropertyChanged(nameof(InstanceCount)); }
        }
        
        public string MostCommonClassification
        {
            get => _mostCommonClassification;
            set { _mostCommonClassification = value; OnPropertyChanged(nameof(MostCommonClassification)); }
        }
        
        public string HighestConfidenceClassification
        {
            get => _highestConfidenceClassification;
            set { _highestConfidenceClassification = value; OnPropertyChanged(nameof(HighestConfidenceClassification)); }
        }
        
        public string MostCommonDescription
        {
            get => _mostCommonDescription;
            set { _mostCommonDescription = value; OnPropertyChanged(nameof(MostCommonDescription)); }
        }
        
        public string HighestConfidenceDescription
        {
            get => _highestConfidenceDescription;
            set { _highestConfidenceDescription = value; OnPropertyChanged(nameof(HighestConfidenceDescription)); }
        }
        
        public string AverageConfidencePercent
        {
            get => _averageConfidencePercent;
            set { _averageConfidencePercent = value; OnPropertyChanged(nameof(AverageConfidencePercent)); }
        }
        
        public List<string> AvailableCodes
        {
            get => _availableCodes;
            set { _availableCodes = value; OnPropertyChanged(nameof(AvailableCodes)); }
        }
        
        /// <summary>
        /// Selected Uniclass code
        /// </summary>
        public string SelectedCode
        {
            get => _selectedCode;
            set 
            { 
                if (_selectedCode != value)
                {
                    _selectedCode = value;
                    OnPropertyChanged(nameof(SelectedCode));
                    OnPropertyChanged(nameof(IsSelectedCodeMostCommon));
                    OnPropertyChanged(nameof(IsSelectedCodeHighestConfidence));
                    OnPropertyChanged(nameof(IsSelectedCodeCustom));
                    
                    // Auto-update description when code changes
                    if (value != "Custom..." && !string.IsNullOrWhiteSpace(value))
                    {
                        string desc = UniclassLookup.GetDescription(value);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            CurrentDescription = desc;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Current description (updates dynamically)
        /// </summary>
        public string CurrentDescription
        {
            get => _currentDescription;
            set 
            { 
                _currentDescription = value;
                OnPropertyChanged(nameof(CurrentDescription));
            }
        }
        
        /// <summary>
        /// True if selected code matches Most Common (for orange color)
        /// </summary>
        public bool IsSelectedCodeMostCommon
        {
            get => !string.IsNullOrWhiteSpace(SelectedCode) && 
                   SelectedCode.Equals(MostCommonClassification, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// True if selected code matches Highest Confidence (for green color)
        /// </summary>
        public bool IsSelectedCodeHighestConfidence
        {
            get => !string.IsNullOrWhiteSpace(SelectedCode) && 
                   SelectedCode.Equals(HighestConfidenceClassification, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// True if selected code is custom (for purple color)
        /// </summary>
        public bool IsSelectedCodeCustom
        {
            get => !string.IsNullOrWhiteSpace(SelectedCode) && 
                   !SelectedCode.Equals(MostCommonClassification, StringComparison.OrdinalIgnoreCase) &&
                   !SelectedCode.Equals(HighestConfidenceClassification, StringComparison.OrdinalIgnoreCase) &&
                   SelectedCode != "Custom...";
        }
        
        // Reference to original FamilyTypeClassification for applying
        public FamilyTypeClassification OriginalData { get; set; }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
