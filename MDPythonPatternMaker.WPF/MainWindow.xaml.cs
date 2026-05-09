using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using MDPythonPatternMaker.Core;

namespace MDPythonPatternMaker
{
    public partial class MainWindow : System.Windows.Window
    {
        private Mat _sourceMat;
        private readonly PatternConverter _converter = new PatternConverter();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
            if (openFile.ShowDialog() == true)
            {
                _sourceMat?.Dispose();
                _sourceMat = Cv2.ImRead(openFile.FileName, ImreadModes.Unchanged);
                ImgSource.Source = _sourceMat.ToWriteableBitmap();

                CanvasVector.Width = _sourceMat.Width;
                CanvasVector.Height = _sourceMat.Height;
                CanvasVector.Children.Clear();
                TxtPython.Clear();

                // 画像が読み込まれたら変換を許可
                BtnConvert.IsEnabled = true;
                BtnCopy.IsEnabled = false;
            }
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceMat == null) return;

            var result = _converter.ExtractByChannels(
                _sourceMat,
                SldEpsilon.Value,
                SldMinArea.Value,
                (int)SldRedThr.Value);

            UpdatePreview(result);
            TxtPython.Text = _converter.GeneratePythonScript(result, _sourceMat.Height, SldScale.Value);

            // 変換されたらコピーを許可
            BtnCopy.IsEnabled = true;
        }

        private void UpdatePreview(ExtractionResult result)
        {
            CanvasVector.Children.Clear();
            foreach (var points in result.OuterShapes)
                DrawPolygon(points, Brushes.Yellow, new SolidColorBrush(Color.FromArgb(120, 255, 0, 0)));
            foreach (var points in result.InternalShapes)
                DrawPolygon(points, Brushes.White, new SolidColorBrush(Color.FromArgb(120, 0, 255, 0)));
        }

        private void DrawPolygon(OpenCvSharp.Point[] points, Brush stroke, Brush fill)
        {
            var polygon = new Polygon { Stroke = stroke, StrokeThickness = 2, Fill = fill };
            foreach (var p in points) polygon.Points.Add(new System.Windows.Point(p.X, p.Y));
            CanvasVector.Children.Add(polygon);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtPython.Text))
            {
                Clipboard.SetText(TxtPython.Text);
                MessageBox.Show("コードをコピーしました。");
            }
        }

        // --- リセット処理 ---
        private void BtnResetScale_Click(object sender, RoutedEventArgs e) => SldScale.Value = 1.0;
        private void BtnResetEpsilon_Click(object sender, RoutedEventArgs e) => SldEpsilon.Value = 0.005;
        private void BtnResetMinArea_Click(object sender, RoutedEventArgs e) => SldMinArea.Value = 30;
        private void BtnResetRedThr_Click(object sender, RoutedEventArgs e) => SldRedThr.Value = 200;
    }
}