using System;
using System.Windows;
using System.Windows.Controls;

namespace UniclassClassifier.Windows
{
    public partial class ClassificationConfigWindow : Window
    {
        public double Level1Threshold { get; private set; }
        public double Level2Threshold { get; private set; }
        public bool WasCancelled { get; private set; }

        public ClassificationConfigWindow()
        {
            InitializeComponent();
            
            // Set default values
            Level1Threshold = 0.20;
            Level2Threshold = 0.40;
            WasCancelled = false;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == Level1Slider && Level1ValueText != null)
            {
                Level1ValueText.Text = ((int)Level1Slider.Value).ToString();
            }
            else if (sender == Level2Slider && Level2ValueText != null)
            {
                Level2ValueText.Text = ((int)Level2Slider.Value).ToString();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Level1Threshold = Level1Slider.Value / 100.0;
            Level2Threshold = Level2Slider.Value / 100.0;
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
}
