using System;
using System.Collections.Generic;
using System.Text;
using OpenCvSharp;

namespace MDPythonPatternMaker.Core.Converters
{
    public class ExtractionResult
    {
        public List<Point[]> OuterShapes { get; set; } = new List<Point[]>();
        public List<Point[]> InternalShapes { get; set; } = new List<Point[]>();
    }

    public class PatternConverter
    {
        /// <summary>
        /// 画像から各チャンネルに基づいて形状を抽出します。
        /// </summary>
        /// <param name="source">入力画像(Mat)</param>
        /// <param name="epsilonFactor">滑らかさ（小さいほど精密、大きいほど簡略化）</param>
        /// <param name="minArea">抽出する最小面積（ゴミ取り用）</param>
        /// <param name="redThreshold">赤色の判定しきい値（外枠の太さに影響）</param>
        public ExtractionResult ExtractByChannels(
            Mat source,
            double epsilonFactor = 0.005,
            double minArea = 30,
            int redThreshold = 200)
        {
            var result = new ExtractionResult();
            if (source == null) return result;

            // 1. チャンネル分離
            Mat[] channels = source.Split();
            using var b = channels[0];
            using var g = channels[1];
            using var r = channels[2];

            // 不透明な場所のマスク (背景除外)
            using var alphaMask = new Mat();
            if (source.Channels() == 4)
            {
                Cv2.Threshold(channels[3], alphaMask, 1, 255, ThresholdTypes.Binary);
            }
            else
            {
                alphaMask.Create(source.Size(), MatType.CV_8UC1);
                alphaMask.SetTo(new Scalar(255));
            }

            // 2. 【全体形状】の特定 (背景の白ではない場所)
            using var isWhite = new Mat();
            using var iwR = new Mat(); using var iwG = new Mat(); using var iwB = new Mat();
            using var notWhite = new Mat();
            using var shapeMask = new Mat();

            Cv2.Threshold(r, iwR, 245, 255, ThresholdTypes.Binary);
            Cv2.Threshold(g, iwG, 245, 255, ThresholdTypes.Binary);
            Cv2.Threshold(b, iwB, 245, 255, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(iwR, iwG, isWhite);
            Cv2.BitwiseAnd(isWhite, iwB, isWhite);

            Cv2.BitwiseNot(isWhite, notWhite);
            Cv2.BitwiseAnd(alphaMask, notWhite, shapeMask);

            // 3. 【赤 (外枠)】の抽出
            // パラメータ redThreshold を使用
            using var rBright = new Mat();
            using var gLow = new Mat();
            using var bLow = new Mat();
            using var redMask = new Mat();

            Cv2.Threshold(r, rBright, redThreshold, 255, ThresholdTypes.Binary);
            Cv2.Threshold(g, gLow, Math.Max(0, redThreshold - 50), 255, ThresholdTypes.BinaryInv);
            Cv2.Threshold(b, bLow, Math.Max(0, redThreshold - 50), 255, ThresholdTypes.BinaryInv);

            Cv2.BitwiseAnd(rBright, gLow, redMask);
            Cv2.BitwiseAnd(redMask, bLow, redMask);
            Cv2.BitwiseAnd(redMask, shapeMask, redMask);

            // 形を整える ( Closing処理 )
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Close, kernel);

            // 輪郭抽出 ( 外枠 )
            result.OuterShapes = GetContours(redMask, epsilonFactor, minArea);

            // 4. 【内部図形 (赤以外の色)】の抽出
            using var notRed = new Mat();
            using var internalMask = new Mat();

            Cv2.BitwiseNot(redMask, notRed);
            Cv2.BitwiseAnd(shapeMask, notRed, internalMask);

            // 輪郭抽出 ( 内部図形 )
            result.InternalShapes = GetContours(internalMask, epsilonFactor, minArea);

            // メモリ解放
            foreach (var m in channels) m.Dispose();
            return result;
        }

        /// <summary>
        /// マスク画像から輪郭を近似して取得します。
        /// </summary>
        private List<Point[]> GetContours(Mat mask, double epsilonFactor, double minArea)
        {
            var list = new List<Point[]>();
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var c in contours)
            {
                // 指定された最小面積以下のゴミを除去
                if (Cv2.ContourArea(c) < minArea) continue;

                double epsilon = epsilonFactor * Cv2.ArcLength(c, true);
                list.Add(Cv2.ApproxPolyDP(c, epsilon, true));
            }
            return list;
        }

        /// <summary>
        /// 抽出結果からMD用Pythonスクリプトを生成します。
        /// </summary>
        /// <param name="result">抽出データ</param>
        /// <param name="imageHeight">画像高さ(Y反転用)</param>
        /// <param name="scale">出力スケール倍率</param>
        public string GeneratePythonScript(ExtractionResult result, int imageHeight, double scale = 1.0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("import pattern_api\n");
            sb.AppendLine($"# Generated with Scale: {scale}, Outer Count: {result.OuterShapes.Count}");

            for (int i = 0; i < result.OuterShapes.Count; i++)
            {
                string oVar = $"outer_{i}";
                AppendPoints(sb, result.OuterShapes[i], oVar, imageHeight, scale);
                sb.AppendLine($"pid_{i} = pattern_api.CreatePatternWithPoints({oVar})");

                for (int j = 0; j < result.InternalShapes.Count; j++)
                {
                    string iVar = $"inner_{i}_{j}";
                    AppendPoints(sb, result.InternalShapes[j], iVar, imageHeight, scale);
                    sb.AppendLine($"pattern_api.CreateInternalShapeWithPoints(pid_{i}, {iVar}, True)");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 頂点リストをPythonのリスト形式で書き出します。
        /// </summary>
        private void AppendPoints(StringBuilder sb, Point[] points, string varName, int imageHeight, double scale)
        {
            sb.AppendLine($"{varName} = [");
            foreach (var p in points)
            {
                // 座標にスケールを適用
                double x = p.X * scale;
                double y = (imageHeight - p.Y) * scale;
                sb.AppendLine($"    ({x:F2}, {y:F2}, 0),");
            }
            sb.AppendLine("]");
        }
    }
}