using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Reflection;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using MDPythonPatternMaker.Core.Config;
using MDPythonPatternMaker.Core.IO;
using MDPythonPatternMaker.WPF.Properties;
using MDPythonPatternMaker.Core.Converters;

namespace MDPythonPatternMaker
{
    public partial class MainWindow : System.Windows.Window
    {
        private Mat _sourceMat;
        private string _loadedFileName = "";
        private readonly PatternConverter _converter = new PatternConverter();

        public MainWindow()
        {
            InitializeComponent();

            // 設定の復元
            var config = ConfigManager.Load();
            SldScale.Value = config.Scale;
            SldEpsilon.Value = config.Epsilon;
            SldMinArea.Value = config.MinArea;
            SldRedThr.Value = config.RedThreshold;
        }

        // --- Help メニューイベント ---

        private void MenuDocument_Click(object sender, RoutedEventArgs e)
        {
            // GitHubのリポジトリ（README）を開く
            var url = "https://github.com/posita33/MDPythonPatternMaker";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ドキュメントを開けませんでした: {ex.Message}");
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            // 実行中のアセンブリからバージョン情報を取得
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            string aboutText = "MDPythonPatternMaker\n" +
                               $"Version: {version}\n\n" +
                               "Licenses:\n" +
                               "- OpenCvSharp (Apache License 2.0)\n" +
                               "- Newtonsoft.Json (MIT License)\n" +
                               "- WPF Toolkit (Microsoft Public License)";

            MessageBox.Show(aboutText, "About This Tool", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- ファイル操作・変換ロジック ---
        // ドラッグ中のマウスカーソルの制御
        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            // ファイル形式であればコピー（受け入れ可能）のカーソルにする
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        // ファイルがドロップされた時の処理
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // ドロップされたファイルパスを取得（複数選択でも最初の1つを取得）
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    LoadImageFromFile(filePath); // 共通の読み込みメソッドを呼び出す
                }
            }
        }

        /// <summary>
        /// 指定されたパスから画像を読み込み、UIと内部データを更新します。
        /// </summary>
        /// <param name="filePath">読み込む画像のフルパス</param>
        private void LoadImageFromFile(string filePath)
        {
            try
            {
                // 1. ドラッグ＆ドロップ案内テキストを非表示にする
                TxtDragDropGuide.Visibility = Visibility.Collapsed;

                // 2. 前回のMatリソースを解放し、新しい画像を読み込む 
                _sourceMat?.Dispose();
                _sourceMat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
        
                // 3. UI（Original Image）に画像を表示 
                ImgSource.Source = _sourceMat.ToWriteableBitmap();

                // 4. 保存時のデフォルト名に使用するため、ファイル名（拡張子なし）を保持 
                _loadedFileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

                // 5. プレビュー用キャンバスのサイズを画像に合わせる 
                CanvasVector.Width = _sourceMat.Width;
                CanvasVector.Height = _sourceMat.Height;

                // 6. 以前の変換結果（プレビュー・コード）をクリア 
                CanvasVector.Children.Clear();
                TxtPython.Clear();

                // 7. ボタンの有効状態を更新
                BtnConvert.IsEnabled = true;  // 変換可能にする
                BtnCopy.IsEnabled = false;    // 新しい画像なのでコピーは一旦無効
                BtnSave.IsEnabled = false;    // 同様に保存も一旦無効
            }
            catch (Exception ex)
            {
                // 失敗した場合は案内を再表示し、エラーを通知
                TxtDragDropGuide.Visibility = Visibility.Visible;
                MessageBox.Show($"画像の読み込みに失敗しました: {ex.Message}");
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
            if (openFile.ShowDialog() == true)
            {
                LoadImageFromFile(openFile.FileName);
            }
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceMat == null) return;

            var currentConfig = new AppConfig
            {
                Scale = SldScale.Value,
                Epsilon = SldEpsilon.Value,
                MinArea = SldMinArea.Value,
                RedThreshold = (int)SldRedThr.Value
            };
            ConfigManager.Save(currentConfig);

            var result = _converter.ExtractByChannels(
                _sourceMat,
                currentConfig.Epsilon,
                currentConfig.MinArea,
                currentConfig.RedThreshold);

            UpdatePreview(result);
            TxtPython.Text = _converter.GeneratePythonScript(result, _sourceMat.Height, currentConfig.Scale);

            BtnCopy.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }

        // --- プレビュー表示（Issue #11 対応済み配色） ---

        private void UpdatePreview(ExtractionResult result)
        {
            CanvasVector.Children.Clear();

            // 外枠（パターンのベース）：赤色
            foreach (var points in result.OuterShapes)
            {
                DrawPolygon(points,
                    Brushes.Red,
                    new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)));
            }

            // 内部パーツ（InternalShape）：黄色
            foreach (var points in result.InternalShapes)
            {
                DrawPolygon(points,
                    Brushes.Yellow,
                    new SolidColorBrush(Color.FromArgb(100, 255, 255, 0)));
            }
        }

        private void DrawPolygon(OpenCvSharp.Point[] points, Brush stroke, Brush fill)
        {
            var polygon = new Polygon { Stroke = stroke, StrokeThickness = 2, Fill = fill };
            foreach (var p in points) polygon.Points.Add(new System.Windows.Point(p.X, p.Y));
            CanvasVector.Children.Add(polygon);
        }

        // --- Python コード操作 ---

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

        // --- ウィンドウ終了処理（Issue #7 対応済み） ---

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                Settings.Default.WindowLeft = Left;
                Settings.Default.WindowTop = Top;
                Settings.Default.WindowWidth = Width;
                Settings.Default.WindowHeight = Height;
            }
            Settings.Default.Save();
        }

        // --- スライダーリセット ---
        private void BtnResetScale_Click(object sender, RoutedEventArgs e) => SldScale.Value = 1.0;
        private void BtnResetEpsilon_Click(object sender, RoutedEventArgs e) => SldEpsilon.Value = 0.005;
        private void BtnResetMinArea_Click(object sender, RoutedEventArgs e) => SldMinArea.Value = 30;
        private void BtnResetRedThr_Click(object sender, RoutedEventArgs e) => SldRedThr.Value = 200;
    }
}