using MDPythonPatternMaker.Core;
using MDPythonPatternMaker.Core.Config;
using MDPythonPatternMaker.Core.IO;
using MDPythonPatternMaker.WPF.Properties;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MDPythonPatternMaker
{
    public partial class MainWindow : System.Windows.Window
    {
        private Mat _sourceMat;
        private readonly PatternConverter _converter = new PatternConverter();

        public MainWindow()
        {
            InitializeComponent();

            // 起動時にロードしてUIに反映
            var config = ConfigManager.Load();
            SldScale.Value = config.Scale;
            SldEpsilon.Value = config.Epsilon;
            SldMinArea.Value = config.MinArea;
            SldRedThr.Value = config.RedThreshold;
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

            // 変換時に現在の設定を保存
            var currentConfig = new AppConfig
            {
                Scale = SldScale.Value,
                Epsilon = SldEpsilon.Value,
                MinArea = SldMinArea.Value,
                RedThreshold = (int)SldRedThr.Value
            };
            ConfigManager.Save(currentConfig);

            // 変換ロジックの実行
            var result = _converter.ExtractByChannels(
                _sourceMat,
                currentConfig.Epsilon,
                currentConfig.MinArea,
                currentConfig.RedThreshold);

            UpdatePreview(result);
            TxtPython.Text = _converter.GeneratePythonScript(result, _sourceMat.Height, currentConfig.Scale);
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 最小化・最大化状態ではない場合のみ、現在のサイズと位置を記録
            if (WindowState == WindowState.Normal)
            {
                Settings.Default.WindowLeft = Left;
                Settings.Default.WindowTop = Top;
                Settings.Default.WindowWidth = Width;
                Settings.Default.WindowHeight = Height;
            }

            // 設定を永続化保存
            Settings.Default.Save();
        }

        // --- リセット処理 ---
        private void BtnResetScale_Click(object sender, RoutedEventArgs e) => SldScale.Value = 1.0;
        private void BtnResetEpsilon_Click(object sender, RoutedEventArgs e) => SldEpsilon.Value = 0.005;
        private void BtnResetMinArea_Click(object sender, RoutedEventArgs e) => SldMinArea.Value = 30;
        private void BtnResetRedThr_Click(object sender, RoutedEventArgs e) => SldRedThr.Value = 200;
    }
}