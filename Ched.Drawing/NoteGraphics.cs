﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

using Ched.Core.Notes;

namespace Ched.Drawing
{
    public static class NoteGraphics
    {

        private static int BezierPrecision = 15;
        public static void DrawTap(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawTappableNote(rect, dc.ColorProfile.TapColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawExTap(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawTappableNote(rect, dc.ColorProfile.ExTapColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawExTapDown(this DrawingContext dc, RectangleF rect)
        {
            //Console.WriteLine(String.Format("extapdown at {0} {1}", rect.Y, rect.Height));
            dc.Graphics.DrawTappableNote(rect, dc.ColorProfile.ExTapDownColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawExTapCenter(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawTappableNote(rect, dc.ColorProfile.ExTapCenterColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawFlick(this DrawingContext dc, RectangleF rect)
        {
            var foregroundRect = new RectangleF(rect.Left + rect.Width / 4, rect.Top, rect.Width / 2, rect.Height);
            dc.Graphics.DrawNoteBase(rect, dc.ColorProfile.FlickColor.Item1);
            dc.Graphics.DrawNoteBase(foregroundRect, dc.ColorProfile.FlickColor.Item2);
            dc.Graphics.DrawBorder(rect, dc.ColorProfile.BorderColor);
            dc.Graphics.DrawTapSymbol(foregroundRect);
        }

        public static void DrawDamage(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawSquarishNote(rect, dc.ColorProfile.DamageColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawHoldBegin(this DrawingContext dc, RectangleF rect)
        {
            dc.DrawHoldEnd(rect);
            dc.Graphics.DrawTapSymbol(rect);
        }

        public static void DrawHoldEnd(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawNote(rect, dc.ColorProfile.HoldColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawHoldBackground(this DrawingContext dc, RectangleF rect)
        {
            Color BackgroundEdgeColor = dc.ColorProfile.HoldBackgroundColor.DarkColor;
            Color BackgroundMiddleColor = dc.ColorProfile.HoldBackgroundColor.LightColor;

            var prevMode = dc.Graphics.SmoothingMode;
            dc.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new LinearGradientBrush(rect, BackgroundEdgeColor, BackgroundMiddleColor, LinearGradientMode.Vertical))
            {
                var blend = new ColorBlend(4)
                {
                    Colors = new Color[] { BackgroundEdgeColor, BackgroundMiddleColor, BackgroundMiddleColor, BackgroundEdgeColor },
                    Positions = new float[] { 0.0f, 0.3f, 0.7f, 1.0f }
                };
                brush.InterpolationColors = blend;
                dc.Graphics.FillRectangle(brush, rect);
            }
            dc.Graphics.SmoothingMode = prevMode;
        }

        public static void DrawSlideBegin(this DrawingContext dc, RectangleF rect)
        {
            dc.DrawSlideStep(rect);
            dc.Graphics.DrawTapSymbol(rect);
        }

        public static void DrawSlideStep(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawNote(rect, dc.ColorProfile.SlideColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawSlideCurve(this DrawingContext dc, RectangleF rect, bool disabled=false)
        {
            dc.Graphics.DrawNote(rect, disabled ? dc.ColorProfile.SlideCurveDisabledColor : dc.ColorProfile.SlideCurveColor, dc.ColorProfile.BorderColor);
        }

        private static Tuple<List<PointF>, List<PointF>, List<PointF>> GenerateLineCurvePath(IEnumerable<SlideStepElement> points, float leftBound, float rightBound)
        {
            if (points.First().IsCurve || points.Last().IsCurve)
            {
                throw new InvalidOperationException("First and last points cannot be curve");
            }

            var anchors = points.Select(p => new SlideStepElement
            {
                Point = new PointF(p.Point.X + p.Width / 2, p.Point.Y),
                Width = p.Width,
                IsCurve = p.IsCurve
            }).ToList();

            var middleCurve = new List<PointF> { };
            var widths = new List<float> { };

            int i = 0;
            while (i < anchors.Count() - 1)
            {
                if (anchors[i + 1].IsCurve)
                {
                    var startAnchor = anchors[i];
                    var controlAnchor = anchors[i + 1];
                    var endAnchor = anchors[i + 2];

                    var controlPos = (controlAnchor.Point.Y - startAnchor.Point.Y) / (endAnchor.Point.Y - startAnchor.Point.Y);
                    var controlWidth = endAnchor.Width * controlPos + startAnchor.Width * (1 - controlPos);

                    var slices = Enumerable.Range(0, BezierPrecision).Select(p => ((float)p) / BezierPrecision);

                    middleCurve.AddRange(slices.Select(
                        p => new PointF {
                            X = (1 - p) * (1 - p) * startAnchor.Point.X + 2 * p * (1 - p) * controlAnchor.Point.X + p * p * endAnchor.Point.X,
                            Y = (1 - p) * (1 - p) * startAnchor.Point.Y + 2 * p * (1 - p) * controlAnchor.Point.Y + p * p * endAnchor.Point.Y
                        }));
                    widths.AddRange(slices.Select(
                        p => (1 - p) * (1 - p) * startAnchor.Width + 2 * p * (1 - p) * controlWidth + p * p * endAnchor.Width));
                    i += 2;
                } else
                {
                    middleCurve.Add(anchors[i].Point);
                    widths.Add(anchors[i].Width);
                    i += 1;
                }
            }
            middleCurve.Add(anchors.Last().Point);
            widths.Add(anchors[i].Width);

            var leftCurve = middleCurve.Zip(widths, (point, width) => new PointF {
                X = Math.Max(point.X - width * 0.5f, leftBound),
                Y = point.Y
            }).ToList();
            var rightCurve = middleCurve.Zip(widths, (point, width) => new PointF
            {
                X = Math.Min(point.X + width * 0.5f, rightBound),
                Y = point.Y
            }).ToList();

            return Tuple.Create(leftCurve, middleCurve, rightCurve);
        }

        /// <summary>
        /// SLIDEの背景を描画します。
        /// </summary>
        /// <param name="dc">処理対象の<see cref="DrawingContext"/></param>
        /// <param name="steps">全ての中継点位置からなるリスト</param>
        /// <param name="visibleSteps">可視中継点のY座標からなるリスト</param>
        /// <param name="noteHeight">ノート描画高さ</param>
        public static void DrawSlideBackground(this DrawingContext dc, IEnumerable<SlideStepElement> steps,IEnumerable<float> visibleSteps, float noteHeight, float laneWidth)
        {
            var prevMode = dc.Graphics.SmoothingMode;
            dc.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color BackgroundEdgeColor = dc.ColorProfile.SlideBackgroundColor.DarkColor;
            Color BackgroundMiddleColor = dc.ColorProfile.SlideBackgroundColor.LightColor;

            var orderedSteps = steps.OrderBy(p => p.Point.Y).ToList();
            var orderedVisibleSteps = visibleSteps.OrderBy(p => p).ToList();

            if (orderedSteps.First().Point.Y < orderedVisibleSteps.First() || orderedSteps.First().Point.Y > orderedVisibleSteps.Last())
            {
                throw new ArgumentOutOfRangeException("visibleSteps", "visibleSteps must contain steps");
            }

            // Interpolate slides and curves
            var (leftCurve, middleCurve, rightCurve) = GenerateLineCurvePath(orderedSteps, 0.0f, laneWidth);

            using (var path = new GraphicsPath())
            {
                rightCurve.Reverse();
                path.AddPolygon(leftCurve.Concat(rightCurve).ToArray());

                float head = orderedVisibleSteps[0];
                float height = orderedVisibleSteps[orderedVisibleSteps.Count - 1] - head;
                var pathBounds = path.GetBounds();
                var blendBounds = new RectangleF(pathBounds.X, head, pathBounds.Width, height);
                using (var brush = new LinearGradientBrush(blendBounds, Color.Black, Color.Black, LinearGradientMode.Vertical))
                {
                    var heights = orderedVisibleSteps.Zip(orderedVisibleSteps.Skip(1), (p, q) => Tuple.Create(p, q - p));
                    var absPos = new[] { head }.Concat(heights.SelectMany(p => new[] { p.Item1 + p.Item2 * 0.3f, p.Item1 + p.Item2 * 0.7f, p.Item1 + p.Item2 }));
                    var blend = new ColorBlend()
                    {
                        Positions = absPos.Select(p => (p - head) / height).ToArray(),
                        Colors = new[] { BackgroundEdgeColor }.Concat(Enumerable.Range(0, orderedVisibleSteps.Count - 1).SelectMany(p => new[] { BackgroundMiddleColor, BackgroundMiddleColor, BackgroundEdgeColor })).ToArray()
                    };
                    brush.InterpolationColors = blend;
                    dc.Graphics.FillPath(brush, path);
                }
            }

            using (var pen = new Pen(dc.ColorProfile.SlideLineColor, noteHeight * 0.4f))
            {
                dc.Graphics.DrawLines(pen, middleCurve.ToArray());
            }

            dc.Graphics.SmoothingMode = prevMode;
        }

        public static GraphicsPath GetSlideBackgroundPath(float width1, float width2, float x1, float y1, float x2, float y2)
        {
            var path = new GraphicsPath();
            path.AddPolygon(new PointF[]
            {
                new PointF(x1, y1),
                new PointF(x1 + width1, y1),
                new PointF(x2 + width2, y2),
                new PointF(x2, y2)
            });
            return path;
        }

        public static void DrawAir(this DrawingContext dc, RectangleF targetNoteRect, VerticalAirDirection verticalDirection, HorizontalAirDirection horizontalDirection)
        {
            var targetRect = GetAirRect(targetNoteRect);
            // ノートを内包するRect(ノートの下部中心が原点)
            var box = new RectangleF(-targetRect.Width / 2, -targetRect.Height, targetRect.Width, targetRect.Height);
            // ノート形状の構成点(上向き)
            var points = new PointF[]
            {
                new PointF(box.Left, box.Bottom),
                new PointF(box.Left, box.Top + box.Height / 3),
                new PointF(box.Left + box.Width / 2 , box.Top),
                new PointF(box.Right, box.Top + box.Height / 3),
                new PointF(box.Right, box.Bottom),
                new PointF(box.Left + box.Width / 2, box.Bottom - box.Height / 3)
            };

            using (var path = new GraphicsPath())
            {
                path.AddPolygon(points);
                var prevMatrix = dc.Graphics.Transform;
                var matrix = prevMatrix.Clone();

                // 描画先の下部中心を原点にもってくる
                matrix.Translate(targetRect.Left + targetRect.Width / 2, targetRect.Top);
                // 振り上げなら上下反転(描画座標が上下逆になってるので……)
                if (verticalDirection == VerticalAirDirection.Up) matrix.Scale(1, -1);
                // 左右分で傾斜をかける
                if (horizontalDirection != HorizontalAirDirection.Center) matrix.Shear(horizontalDirection == HorizontalAirDirection.Left ? 0.5f : -0.5f, 0);
                // 振り下げでずれた高さを補正
                if (verticalDirection == VerticalAirDirection.Down) matrix.Translate(0, box.Height);

                dc.Graphics.Transform = matrix;

                using (var brush = new SolidBrush(verticalDirection == VerticalAirDirection.Down ? dc.ColorProfile.AirDownColor : dc.ColorProfile.AirUpColor))
                {
                    dc.Graphics.FillPath(brush, path);
                }
                // 斜めになると太さが大きく出てしまう
                using (var pen = new Pen(dc.ColorProfile.BorderColor.LightColor, targetRect.Height * (horizontalDirection == HorizontalAirDirection.Center ? 0.12f : 0.1f)) { LineJoin = LineJoin.Bevel })
                {
                    dc.Graphics.DrawPath(pen, path);
                }

                dc.Graphics.Transform = prevMatrix;
            }
        }

        public static RectangleF GetAirRect(RectangleF targetNoteRect)
        {
            var targetSize = new SizeF(targetNoteRect.Width * 0.9f, targetNoteRect.Height * 3);
            var targetLocation = new PointF(targetNoteRect.Left + targetNoteRect.Width * 0.05f, targetNoteRect.Bottom + targetNoteRect.Height);
            return new RectangleF(targetLocation, targetSize);
        }

        public static void DrawAirAction(this DrawingContext dc, RectangleF rect)
        {
            using (var brush = new LinearGradientBrush(rect, dc.ColorProfile.AirActionColor.DarkColor, dc.ColorProfile.AirActionColor.LightColor, LinearGradientMode.Vertical))
            {
                dc.Graphics.FillRectangle(brush, rect);
            }
            using (var brush = new LinearGradientBrush(rect.Expand(rect.Height * 0.1f), dc.ColorProfile.BorderColor.DarkColor, dc.ColorProfile.BorderColor.LightColor, LinearGradientMode.Vertical))
            {
                using (var pen = new Pen(brush, rect.Height * 0.1f))
                {
                    dc.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        public static void DrawAirStep(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawNote(rect, dc.ColorProfile.AirStepColor, dc.ColorProfile.BorderColor);
        }

        public static void DrawAirHoldLine(this DrawingContext dc, float x, float y1, float y2, float noteHeight)
        {
            using (var pen = new Pen(dc.ColorProfile.AirHoldLineColor, noteHeight / 2))
            {
                dc.Graphics.DrawLine(pen, x, y1, x, y2);
            }
        }

        public static void DrawBorder(this DrawingContext dc, RectangleF rect)
        {
            dc.Graphics.DrawBorder(rect, dc.ColorProfile.BorderColor);
        }

        public static void DrawRecorderGroundBackground(this DrawingContext dc, RectangleF rect)
        {
            Color color = dc.ColorProfile.RecorderGroundColor;

            using (var brush = new SolidBrush(color))
            {
                dc.Graphics.FillRectangle(brush, rect);
            }
        }

        public static void DrawRecorderAirBackground(this DrawingContext dc, RectangleF rect)
        {
            Color color = dc.ColorProfile.RecorderAirColor;

            using (var brush = new SolidBrush(color))
            {
                dc.Graphics.FillRectangle(brush, rect);
            }
        }

        public static void DrawRecorderAirUnderBackground(this DrawingContext dc, RectangleF rect)
        {
            Color color = dc.ColorProfile.RecorderAirUnderColor;

            using (var brush = new SolidBrush(color))
            {
                dc.Graphics.FillRectangle(brush, rect);
            }
        }
    }

    public class SlideStepElement
    {
        public PointF Point { get; set; }
        public float Width { get; set; }

        public bool IsCurve { get; set; }
    }
}
