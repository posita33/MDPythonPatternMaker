using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using MDPythonPatternMaker.Core;
using MDPythonPatternMaker.Core.Config;
using MDPythonPatternMaker.Core.IO;
using MDPythonPatternMaker.WPF.Properties;

namespace MDPythonPatternMaker
{
    public partial class MainWindow : System.Windows.Window
    {
        private Mat _sourceMat;
        private string _loadedFileName = ""; // 読み込んだ画像名を保持
        private readonly PatternConverter _converter = new PatternConverter();

        public MainWindow()
        {
            InitializeComponent();

            // 前回終了時の設定を復元
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

                // 拡張子なしのファイル名を保持
                _loadedFileName = System.IO.Path.GetFileNameWithoutExtension(openFile.FileName);

                CanvasVector.Width = _sourceMat.Width;
                CanvasVector.Height = _sourceMat.Height;
                CanvasVector.Children.Clear();
                TxtPython.Clear();

                // 画像が読み込まれたら「変換」を許可
                BtnConvert.IsEnabled = true;
                BtnCopy.IsEnabled = false;
                BtnSave.IsEnabled = false;
            }
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceMat == null) return;

            // パラメータ設定をconfig.jsonに保存
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

            // 変換完了後にコピーと保存を有効化
            BtnCopy.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }

        private void UpdatePreview(ExtractionResult result)
        {
            CanvasVector.Children.Clear();

            // パターンのベース（外枠）：制作フローに合わせて赤色で描画
            foreach (var points in result.OuterShapes)
            {
                DrawPolygon(points,
                    Brushes.Red,
                    new SolidColorBrush(Color.FromArgb(100, 255, 0, 0))); // 半透明の赤
            }

            // 内部パーツ（InternalShape）：ベースの赤色の上でも目立つよう黄色で描画
            foreach (var points in result.InternalShapes)
            {
                DrawPolygon(points,
                    Brushes.Yellow,
                    new SolidColorBrush(Color.FromArgb(100, 255, 255, 0))); // 半透明の黄色
            }
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPython.Text)) return;

            // 画像名に基づいた初期ファイル名を生成
            string defaultName = string.IsNullOrEmpty(_loadedFileName)
                ? "md_pattern"
                : $"md_pattern_{_loadedFileName}";

            var saveFile = new SaveFileDialog
            {
                Filter = "Python Files|*.py",
                FileName = defaultName,
                DefaultExt = ".py"
            };

            if (saveFile.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFile.FileName, TxtPython.Text);
                    MessageBox.Show("スクリプトを保存しました。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存に失敗しました: {ex.Message}");
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ウィンドウの位置とサイズを記憶
            if (WindowState == WindowState.Normal)
            {
                Settings.Default.WindowLeft = Left;
                Settings.Default.WindowTop = Top;
                Settings.Default.WindowWidth = Width;
                Settings.Default.WindowHeight = Height;
            }
            Settings.Default.Save();
        }

        // --- リセット処理 ---
        private void BtnResetScale_Click(object sender, RoutedEventArgs e) => SldScale.Value = 1.0;
        private void BtnResetEpsilon_Click(object sender, RoutedEventArgs e) => SldEpsilon.Value = 0.005;
        private void BtnResetMinArea_Click(object sender, RoutedEventArgs e) => SldMinArea.Value = 30;
        private void BtnResetRedThr_Click(object sender, RoutedEventArgs e) => SldRedThr.Value = 200;
    }
}