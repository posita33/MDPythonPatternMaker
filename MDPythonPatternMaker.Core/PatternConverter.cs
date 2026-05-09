using System;
using System.Collections.Generic;
using System.Text;
using OpenCvSharp;

namespace MDPythonPatternMaker.Core
{
    public class ExtractionResult
    {
        public List<Point[]> OuterShapes { get; set; } = new List<Point[]>();
        public List<Point[]> InternalShapes { get; set; } = new List<Point[]>();
    }

    public class PatternConverter
    {
        public ExtractionResult ExtractByChannels(Mat source, double epsilonFactor = 0.005)
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

            // 2. 【全体形状】の特定 (白ではない場所)
            using var isWhite = new Mat();
            using var iwR = new Mat(); using var iwG = new Mat(); using var iwB = new Mat();
            using var notWhite = new Mat();
            using var shapeMask = new Mat();

            Cv2.Threshold(r, iwR, 245, 255, ThresholdTypes.Binary);
            Cv2.Threshold(g, iwG, 245, 255, ThresholdTypes.Binary);
            Cv2.Threshold(b, iwB, 245, 255, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(iwR, iwG, isWhite);
            Cv2.BitwiseAnd(isWhite, iwB, isWhite);

            // エラー修正: Cv2.BitwiseNot を使用
            Cv2.BitwiseNot(isWhite, notWhite);
            Cv2.BitwiseAnd(alphaMask, notWhite, shapeMask);

            // 3. 【赤 (外枠)】の抽出
            // 黄色(R255, G255)やマゼンタ(R255, B255)を除外するため、GとBが低いことを条件にする
            using var rBright = new Mat();
            using var gLow = new Mat();
            using var bLow = new Mat();
            using var redMask = new Mat();

            Cv2.Threshold(r, rBright, 200, 255, ThresholdTypes.Binary);
            Cv2.Threshold(g, gLow, 150, 255, ThresholdTypes.BinaryInv);
            Cv2.Threshold(b, bLow, 150, 255, ThresholdTypes.BinaryInv);

            Cv2.BitwiseAnd(rBright, gLow, redMask);
            Cv2.BitwiseAnd(redMask, bLow, redMask);
            Cv2.BitwiseAnd(redMask, shapeMask, redMask);

            // 形を整える
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(redMask, redMask, MorphTypes.Close, kernel);
            result.OuterShapes = GetContours(redMask, epsilonFactor);

            // 4. 【内部図形 (赤以外の色)】の抽出
            // 「全体の形」から「赤の形」を引き算する
            using var notRed = new Mat();
            using var internalMask = new Mat();

            // エラー修正: Cv2.BitwiseNot を使用
            Cv2.BitwiseNot(redMask, notRed);
            Cv2.BitwiseAnd(shapeMask, notRed, internalMask);

            result.InternalShapes = GetContours(internalMask, epsilonFactor);

            // メモリ解放
            foreach (var m in channels) m.Dispose();
            return result;
        }

        private List<Point[]> GetContours(Mat mask, double epsilonFactor)
        {
            var list = new List<Point[]>();
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var c in contours)
            {
                if (Cv2.ContourArea(c) < 30) continue;
                double epsilon = epsilonFactor * Cv2.ArcLength(c, true);
                list.Add(Cv2.ApproxPolyDP(c, epsilon, true));
            }
            return list;
        }

        public string GeneratePythonScript(ExtractionResult result, int imageHeight)
        {
            var sb = new StringBuilder();
            sb.AppendLine("import pattern_api\n");
            for (int i = 0; i < result.OuterShapes.Count; i++)
            {
                string oVar = $"outer_{i}";
                AppendPoints(sb, result.OuterShapes[i], oVar, imageHeight);
                sb.AppendLine($"pid_{i} = pattern_api.CreatePatternWithPoints({oVar})");
                for (int j = 0; j < result.InternalShapes.Count; j++)
                {
                    string iVar = $"inner_{i}_{j}";
                    AppendPoints(sb, result.InternalShapes[j], iVar, imageHeight);
                    sb.AppendLine($"pattern_api.CreateInternalShapeWithPoints(pid_{i}, {iVar}, True)");
                }
            }
            return sb.ToString();
        }

        private void AppendPoints(StringBuilder sb, Point[] points, string varName, int imageHeight)
        {
            sb.AppendLine($"{varName} = [");
            foreach (var p in points) sb.AppendLine($"    ({p.X}, {imageHeight - p.Y}, 0),");
            sb.AppendLine("]");
        }
    }
}