using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using WpfPoint = System.Windows.Point;
using WpfLineSegment = System.Windows.Media.LineSegment;

namespace UniclassClassifier.Windows
{
    public partial class ClassificationResultsWindow : Window
    {
        private Document _doc;
        private Dictionary<string, ClassificationResult> _classificationResults;

        public ClassificationResultsWindow(
            Document doc,
            Dictionary<string, ClassificationResult> classificationResults,
            int totalElements,
            int unclassified,
            int level1,
            int level2,
            double level1Threshold,
            double level2Threshold,
            double processingTime)
        {
            InitializeComponent();

            _doc = doc;
            _classificationResults = classificationResults;

            // Set values
            TotalElementsText.Text = totalElements.ToString();
            ProcessingTimeText.Text = $"{processingTime:F2}s";

            UnclassifiedCountText.Text = unclassified.ToString();
            Level1CountText.Text = level1.ToString();
            Level2CountText.Text = level2.ToString();

            // Calculate percentages
            double unclassifiedPercent = totalElements > 0 ? (unclassified * 100.0 / totalElements) : 0;
            double level1Percent = totalElements > 0 ? (level1 * 100.0 / totalElements) : 0;
            double level2Percent = totalElements > 0 ? (level2 * 100.0 / totalElements) : 0;

            UnclassifiedPercentText.Text = $"{unclassifiedPercent:F1}%";
            Level1PercentText.Text = $"{level1Percent:F1}%";
            Level2PercentText.Text = $"{level2Percent:F1}%";

            // Set progress bars (475 is approximate available width)
            double availableWidth = 475;
            
            Level2Bar.Width = (level2Percent / 100.0) * availableWidth;
            Level1Bar.Width = (level1Percent / 100.0) * availableWidth;
            UnclassifiedBar.Width = (unclassifiedPercent / 100.0) * availableWidth;

            // Set text inside bars (only show percentage, no element count)
            if (Level2Bar.Width > 40)
            {
                Level2BarText.Text = $"{level2Percent:F1}%";
            }

            if (Level1Bar.Width > 40)
            {
                Level1BarText.Text = $"{level1Percent:F1}%";
            }

            if (UnclassifiedBar.Width > 40)
            {
                UnclassifiedBarText.Text = $"{unclassifiedPercent:F1}%";
            }

            // Calculate confidence distribution
            var groupElements = classificationResults.Where(r => r.Value.level == "level_1").ToList();
            var subgroupElements = classificationResults.Where(r => r.Value.level == "level_2").ToList();

            int groupHighConf = groupElements.Count(r => r.Value.confidence >= 0.5);
            int groupLowConf = groupElements.Count(r => r.Value.confidence < 0.5);
            
            int subgroupHighConf = subgroupElements.Count(r => r.Value.confidence >= 0.5);
            int subgroupLowConf = subgroupElements.Count(r => r.Value.confidence < 0.5);

            GroupHighConfText.Text = groupHighConf.ToString();
            GroupLowConfText.Text = groupLowConf.ToString();
            SubgroupHighConfText.Text = subgroupHighConf.ToString();
            SubgroupLowConfText.Text = subgroupLowConf.ToString();

            // Draw pie charts
            int totalGroup = groupHighConf + groupLowConf;
            if (totalGroup > 0)
            {
                double groupHighPercent = groupHighConf / (double)totalGroup;
                DrawPieSlice(GroupHighConfPie, 60, 60, 60, 0, groupHighPercent * 360);
                DrawPieSlice(GroupLowConfPie, 60, 60, 60, groupHighPercent * 360, 360);
            }
            
            int totalSubgroup = subgroupHighConf + subgroupLowConf;
            if (totalSubgroup > 0)
            {
                double subgroupHighPercent = subgroupHighConf / (double)totalSubgroup;
                DrawPieSlice(SubgroupHighConfPie, 60, 60, 60, 0, subgroupHighPercent * 360);
                DrawPieSlice(SubgroupLowConfPie, 60, 60, 60, subgroupHighPercent * 360, 360);
            }

            // Set thresholds
            Level1ThresholdText.Text = $"{(level1Threshold * 100):F0}%";
            Level2ThresholdText.Text = $"{(level2Threshold * 100):F0}%";
        }

        private void DrawPieSlice(Path path, double centerX, double centerY, double radius, double startAngle, double endAngle)
        {
            // Handle edge cases: if angles are equal or represent a full circle (360 degrees)
            if (Math.Abs(startAngle - endAngle) < 0.01)
            {
                // Don't draw if angles are the same (0% slice)
                return;
            }
            
            // If this represents a full circle (360 degrees), draw a complete circle
            if (Math.Abs(endAngle - startAngle - 360) < 0.01 || (startAngle == 0 && endAngle == 360))
            {
                // Draw full circle using EllipseGeometry
                EllipseGeometry ellipse = new EllipseGeometry(
                    new WpfPoint(centerX, centerY),
                    radius,
                    radius
                );
                path.Data = ellipse;
                return;
            }

            double startRad = (startAngle - 90) * Math.PI / 180.0;
            double endRad = (endAngle - 90) * Math.PI / 180.0;

            WpfPoint startPoint = new WpfPoint(
                centerX + radius * Math.Cos(startRad),
                centerY + radius * Math.Sin(startRad)
            );

            WpfPoint endPoint = new WpfPoint(
                centerX + radius * Math.Cos(endRad),
                centerY + radius * Math.Sin(endRad)
            );

            bool isLargeArc = (endAngle - startAngle) > 180;

            PathGeometry pathGeometry = new PathGeometry();
            PathFigure pathFigure = new PathFigure
            {
                StartPoint = new WpfPoint(centerX, centerY),
                IsClosed = true
            };

            pathFigure.Segments.Add(new WpfLineSegment(startPoint, true));
            pathFigure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true
            ));

            pathGeometry.Figures.Add(pathFigure);
            path.Data = pathGeometry;
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pass only document - window will read current state from model
                var listWindow = new ClassificationListWindow(_doc);
                listWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening details window:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void ConsolidateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Analyze classifications by family/type
                var familyTypeClassifications = ClassificationConsolidator.AnalyzeByFamilyType(_doc, _classificationResults);
                
                // Check for inconsistencies
                var inconsistentTypes = familyTypeClassifications.Where(ft => ft.IsInconsistent).ToList();
                
                if (inconsistentTypes.Count > 0)
                {
                    // Show inconsistencies window
                    var inconsistenciesWindow = new InconsistenciesWindow(inconsistentTypes);
                    bool? result = inconsistenciesWindow.ShowDialog();
                    
                    if (result != true || inconsistenciesWindow.WasCancelled)
                    {
                        return; // User cancelled
                    }
                }
                
                // Show consolidation confirmation window
                var confirmWindow = new ConsolidationConfirmWindow(_doc, familyTypeClassifications);
                confirmWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during consolidation:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show configuration window
                var configWindow = new ClassificationConfigWindow();
                bool? result = configWindow.ShowDialog();

                if (result == true && !configWindow.WasCancelled)
                {
                    // User wants to reclassify - close this window and trigger reclassification
                    this.DialogResult = true; // Signal to parent that we want to reclassify
                    this.Tag = new ReclassifyRequest 
                    { 
                        Level1Threshold = configWindow.Level1Threshold,
                        Level2Threshold = configWindow.Level2Threshold
                    };
                    this.Close();
                }
                // If cancelled, just stay on this window
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening configuration window:\n{ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }
    }
    
    // Helper class to pass reclassification request
    public class ReclassifyRequest
    {
        public double Level1Threshold { get; set; }
        public double Level2Threshold { get; set; }
    }
}
