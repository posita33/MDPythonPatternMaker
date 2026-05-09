using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using MDPythonPatternMaker.Core; // Coreプロジェクトの参照

namespace MDPythonPatternMaker.WPF
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
                // ★重要：Unchanged を指定してαチャンネル(透過)を保持する
                _sourceMat = Cv2.ImRead(openFile.FileName, ImreadModes.Unchanged);
                ImgSource.Source = _sourceMat.ToWriteableBitmap();

                CanvasVector.Width = _sourceMat.Width;
                CanvasVector.Height = _sourceMat.Height;
                CanvasVector.Children.Clear();
                TxtPython.Clear();
            }
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceMat == null)
            {
                MessageBox.Show("先に画像を読み込んでください。");
                return;
            }

            // 1. Coreロジック：チャンネル（色）分離による抽出を実行
            // エラー箇所修正：ExtractPatternsWithHierarchy ではなく ExtractByChannels を呼ぶ
            var result = _converter.ExtractByChannels(_sourceMat, 0.002);

            // 2. プレビュー表示の更新
            UpdatePreview(result);

            // 3. Pythonスクリプトの生成
            TxtPython.Text = _converter.GeneratePythonScript(result, _sourceMat.Height);
        }

        // 修正：引数の型を List<PatternResult> から ExtractionResult に変更
        private void UpdatePreview(ExtractionResult result)
        {
            CanvasVector.Children.Clear();

            // 赤チャンネル (外枠) の描画
            foreach (var points in result.OuterShapes)
            {
                DrawPolygon(points, Brushes.Yellow, new SolidColorBrush(Color.FromArgb(120, 255, 0, 0)));
            }

            // 緑・青チャンネル (内部図形) の描画
            foreach (var points in result.InternalShapes)
            {
                DrawPolygon(points, Brushes.White, new SolidColorBrush(Color.FromArgb(120, 0, 255, 0)));
            }
        }

        private void DrawPolygon(OpenCvSharp.Point[] points, Brush stroke, Brush fill)
        {
            var polygon = new Polygon
            {
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = 2
            };

            foreach (var p in points)
            {
                polygon.Points.Add(new System.Windows.Point(p.X, p.Y));
            }

            CanvasVector.Children.Add(polygon);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtPython.Text))
            {
                Clipboard.SetText(TxtPython.Text);
                MessageBox.Show("Pythonコードをコピーしました。");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _sourceMat?.Dispose();
            base.OnClosed(e);
        }
    }
}