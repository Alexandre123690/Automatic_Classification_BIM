using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace UniclassClassifier.Windows
{
    public partial class InconsistenciesWindow : Window
    {
        public bool WasCancelled { get; private set; }
        
        public InconsistenciesWindow(List<FamilyTypeClassification> inconsistentTypes)
        {
            InitializeComponent();
            
            var items = inconsistentTypes.Select(ft => new InconsistencyItem
            {
                Category = ft.Category,
                FamilyTypeKey = ft.GetFamilyTypeKey(),
                InstanceCount = ft.InstanceCount,
                UniqueClassificationsCount = ft.UniqueClassifications.Count,
                MostCommonClassification = ft.MostCommonClassification,
                HighestConfidenceClassification = ft.HighestConfidenceClassification,
                MostCommonDescription = ft.MostCommonDescription
            }).ToList();
            
            InconsistenciesDataGrid.ItemsSource = items;
            InconsistentCountText.Text = $"{inconsistentTypes.Count} Family Types with inconsistent classifications";
        }
        
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = false;
            this.DialogResult = true;
            this.Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            this.DialogResult = false;
            this.Close();
        }
    }
    
    public class InconsistencyItem : INotifyPropertyChanged
    {
        private string _category;
        private string _familyTypeKey;
        private int _instanceCount;
        private int _uniqueClassificationsCount;
        private string _mostCommonClassification;
        private string _highestConfidenceClassification;
        private string _mostCommonDescription;
        
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
        
        public int UniqueClassificationsCount
        {
            get => _uniqueClassificationsCount;
            set { _uniqueClassificationsCount = value; OnPropertyChanged(nameof(UniqueClassificationsCount)); }
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
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
