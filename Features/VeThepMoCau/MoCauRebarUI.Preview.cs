using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace Vetheprevit.MoCau
{
    public partial class MoCauRebarUI
    {
        private PreviewViewMode _previewViewMode = PreviewViewMode.Plan;
        private double _previewZoom = 1.0;
        private double _previewPanX;
        private double _previewPanY;

        private enum PreviewViewMode
        {
            Plan,
            Front,
            Side
        }

        private void PreviewView_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string viewName))
                return;

            if (Enum.TryParse(viewName, out PreviewViewMode viewMode))
            {
                _previewViewMode = viewMode;
                DrawPreview2D();
            }
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawPreview2D();
        }

        private void PreviewCanvas_MouseWheel(
            object sender,
            System.Windows.Input.MouseWheelEventArgs e)
        {
            Point mouse = e.GetPosition(previewCanvas);
            double centerX = previewCanvas.ActualWidth / 2;
            double centerY = previewCanvas.ActualHeight / 2;
            double oldZoom = _previewZoom;
            double zoomFactor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            _previewZoom = Math.Max(0.5, Math.Min(5.0, _previewZoom * zoomFactor));

            double appliedFactor = _previewZoom / oldZoom;
            _previewPanX =
                mouse.X - centerX -
                appliedFactor * (mouse.X - centerX - _previewPanX);
            _previewPanY =
                mouse.Y - centerY -
                appliedFactor * (mouse.Y - centerY - _previewPanY);

            DrawPreview2D();
            e.Handled = true;
        }

        private void PreviewCanvas_MouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            _previewZoom = 1.0;
            _previewPanX = 0;
            _previewPanY = 0;
            DrawPreview2D();
            e.Handled = true;
        }

        private void DrawPreview2D()
        {
            if (previewCanvas == null) return;

            previewCanvas.Children.Clear();
            if (_geoInfo == null || (!_isStandalonePreview && SelectedAbutment == null))
            {
                if (txtPreviewTitle != null) txtPreviewTitle.Text = "PREVIEW 2D";
                if (txtPreviewHint != null)
                    txtPreviewHint.Text = "Chọn Family mố để xem hình học.";
                DrawPreviewMessage("Chưa có dữ liệu hình học");
                return;
            }

            if (previewCanvas.ActualWidth < 40 || previewCanvas.ActualHeight < 40)
                return;

            UpdatePreviewButtonStyles();

            switch (_previewViewMode)
            {
                case PreviewViewMode.Front:
                    DrawFrontElevationPreview();
                    break;
                case PreviewViewMode.Side:
                    DrawSideElevationPreview();
                    break;
                default:
                    DrawFootingPlanPreview();
                    break;
            }

            AppendZoomHint();
        }

        private void DrawFootingPlanPreview()
        {
            bool showStem = SelectedTabIndex == 1;
            double minL = showStem ? _geoInfo.StemMinL : _geoInfo.FootMinL;
            double maxL = showStem ? _geoInfo.StemMaxL : _geoInfo.FootMaxL;
            double minT = showStem ? _geoInfo.StemMinT : _geoInfo.FootMinT;
            double maxT = showStem ? _geoInfo.StemMaxT : _geoInfo.FootMaxT;
            double length = maxL - minL;
            double width = maxT - minT;
            if (length <= 0 || width <= 0) return;

            txtPreviewTitle.Text = showStem
                ? "MẶT BẰNG THÂN MỐ"
                : "MẶT BẰNG BỆ MỐ";

            PreviewTransform transform =
                CreatePreviewTransform(minL, maxL, minT, maxT);

            AddPreviewRectangle(
                transform.MapX(minL),
                transform.MapY(maxT),
                length * transform.Scale,
                width * transform.Scale,
                showStem ? "#C7E7DF" : "#DDE7F0",
                showStem ? "#0F766E" : "#334155");
            DrawPlanRebarOverlay(
                showStem,
                transform,
                minL,
                maxL,
                minT,
                maxT);
            AddPreviewLabel(
                showStem ? "THÂN MỐ" : "BỆ MỐ",
                transform.MapX((minL + maxL) / 2),
                transform.MapY((minT + maxT) / 2),
                showStem ? "#0F766E" : "#334155");
            AddAxisIndicator("L", "T");
            txtPreviewHint.Text =
                $"{(showStem ? "Thân" : "Bệ")}: " +
                $"{ToMillimeters(length):0} × {ToMillimeters(width):0} mm";
        }

        private void DrawFrontElevationPreview()
        {
            bool showStem = SelectedTabIndex == 1;
            double minX = showStem ? _geoInfo.StemMinL : _geoInfo.FootMinL;
            double maxX = showStem ? _geoInfo.StemMaxL : _geoInfo.FootMaxL;
            double minY = showStem ? _geoInfo.MinUpZ : _geoInfo.MinZ;
            double maxY = showStem ? _geoInfo.StemMaxZ : _geoInfo.MinUpZ;
            if (maxX <= minX || maxY <= minY) return;

            txtPreviewTitle.Text = showStem
                ? "CHIẾU ĐỨNG THÂN MỐ (L–Z)"
                : "CHIẾU ĐỨNG BỆ MỐ (L–Z)";

            PreviewTransform transform =
                CreatePreviewTransform(minX, maxX, minY, maxY);

            AddPreviewRectangle(
                transform.MapX(minX),
                transform.MapY(maxY),
                (maxX - minX) * transform.Scale,
                (maxY - minY) * transform.Scale,
                showStem ? "#C7E7DF" : "#DDE7F0",
                showStem ? "#0F766E" : "#334155");
            DrawFrontRebarOverlay(
                showStem,
                transform,
                minX,
                maxX,
                minY,
                maxY);
            AddPreviewLabel(
                showStem ? "THÂN MỐ" : "BỆ MỐ",
                transform.MapX((minX + maxX) / 2),
                transform.MapY((minY + maxY) / 2),
                showStem ? "#0F766E" : "#334155");

            AddAxisIndicator("L", "Z");
            txtPreviewHint.Text =
                $"{(showStem ? "Thân" : "Bệ")}: dài {ToMillimeters(maxX - minX):0} " +
                $"× cao {ToMillimeters(maxY - minY):0} mm";
        }

        private void DrawSideElevationPreview()
        {
            bool showStem = SelectedTabIndex == 1;
            bool hasStemProfile =
                showStem &&
                _geoInfo.StemSideProfile != null &&
                _geoInfo.StemSideProfile.Count >= 3;

            double minX = hasStemProfile
                ? _geoInfo.StemSideProfile.Min(point => point.T)
                : showStem ? _geoInfo.StemMinT : _geoInfo.FootMinT;
            double maxX = hasStemProfile
                ? _geoInfo.StemSideProfile.Max(point => point.T)
                : showStem ? _geoInfo.StemMaxT : _geoInfo.FootMaxT;
            double minY = hasStemProfile
                ? _geoInfo.StemSideProfile.Min(point => point.Z)
                : showStem ? _geoInfo.MinUpZ : _geoInfo.MinZ;
            double maxY = hasStemProfile
                ? _geoInfo.StemSideProfile.Max(point => point.Z)
                : showStem ? _geoInfo.StemMaxZ : _geoInfo.MinUpZ;
            if (maxX <= minX || maxY <= minY) return;

            txtPreviewTitle.Text = showStem
                ? "CHIẾU CẠNH THÂN MỐ (T–Z)"
                : "CHIẾU CẠNH BỆ MỐ (T–Z)";

            PreviewTransform transform =
                CreatePreviewTransform(minX, maxX, minY, maxY);

            if (hasStemProfile)
            {
                AddPreviewPolygon(
                    _geoInfo.StemSideProfile
                        .Select(point => new Point(
                            transform.MapX(point.T),
                            transform.MapY(point.Z))),
                    "#C7E7DF",
                    "#0F766E");
            }
            else
            {
                AddPreviewRectangle(
                    transform.MapX(minX),
                    transform.MapY(maxY),
                    (maxX - minX) * transform.Scale,
                    (maxY - minY) * transform.Scale,
                    showStem ? "#C7E7DF" : "#DDE7F0",
                    showStem ? "#0F766E" : "#334155");
            }

            DrawSideRebarOverlay(
                showStem,
                transform,
                minX,
                maxX,
                minY,
                maxY);
            AddPreviewLabel(
                showStem ? "THÂN MỐ" : "BỆ MỐ",
                transform.MapX((minX + maxX) / 2),
                transform.MapY((minY + maxY) / 2),
                showStem ? "#0F766E" : "#334155");

            AddAxisIndicator("T", "Z");
            txtPreviewHint.Text =
                $"{(showStem ? "Thân" : "Bệ")}: rộng {ToMillimeters(maxX - minX):0} " +
                $"× cao {ToMillimeters(maxY - minY):0} mm";
        }

        private void AddPreviewPolygon(
            System.Collections.Generic.IEnumerable<Point> points,
            string fill,
            string stroke)
        {
            System.Windows.Shapes.Polygon polygon =
                new System.Windows.Shapes.Polygon
                {
                    Points = new PointCollection(points),
                    Fill = ParseBrush(fill),
                    Stroke = ParseBrush(stroke),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round
                };
            previewCanvas.Children.Add(polygon);
        }

        private void UpdatePreviewButtonStyles()
        {
            SetPreviewButtonStyle(
                btnPreviewPlan,
                _previewViewMode == PreviewViewMode.Plan);
            SetPreviewButtonStyle(
                btnPreviewFront,
                _previewViewMode == PreviewViewMode.Front);
            SetPreviewButtonStyle(
                btnPreviewSide,
                _previewViewMode == PreviewViewMode.Side);
        }

        private static void SetPreviewButtonStyle(Button button, bool isSelected)
        {
            if (button == null) return;

            button.Background = ParseBrush(isSelected ? "#1E293B" : "#F1F5F9");
            button.Foreground = ParseBrush(isSelected ? "#FFFFFF" : "#334155");
            button.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
        }

        private PreviewTransform CreatePreviewTransform(
            double minX,
            double maxX,
            double minY,
            double maxY)
        {
            const double padding = 28;
            double modelWidth = Math.Max(maxX - minX, 0.001);
            double modelHeight = Math.Max(maxY - minY, 0.001);
            double availableWidth = Math.Max(1, previewCanvas.ActualWidth - 2 * padding);
            double availableHeight = Math.Max(1, previewCanvas.ActualHeight - 2 * padding);
            double scale = Math.Min(
                availableWidth / modelWidth,
                availableHeight / modelHeight) * _previewZoom;

            double drawingWidth = modelWidth * scale;
            double drawingHeight = modelHeight * scale;
            double offsetX =
                (previewCanvas.ActualWidth - drawingWidth) / 2 + _previewPanX;
            double offsetY =
                (previewCanvas.ActualHeight - drawingHeight) / 2 + _previewPanY;

            return new PreviewTransform(minX, maxY, scale, offsetX, offsetY);
        }

        private void AppendZoomHint()
        {
            if (txtPreviewHint == null) return;

            txtPreviewHint.Text +=
                $"\nZoom: {_previewZoom * 100:0}% — cuộn chuột, nhấp đúp để đặt lại";
        }

        private void AddPreviewRectangle(
            double left,
            double top,
            double width,
            double height,
            string fill,
            string stroke)
        {
            System.Windows.Shapes.Rectangle rectangle =
                new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(1, width),
                    Height = Math.Max(1, height),
                    Fill = ParseBrush(fill),
                    Stroke = ParseBrush(stroke),
                    StrokeThickness = 2
                };

            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            previewCanvas.Children.Add(rectangle);
        }

        private void AddPreviewLabel(
            string text,
            double centerX,
            double centerY,
            string foreground)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Foreground = ParseBrush(foreground),
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, centerX - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centerY - label.DesiredSize.Height / 2);
            previewCanvas.Children.Add(label);
        }

        private void AddAxisIndicator(string horizontalAxis, string verticalAxis)
        {
            const double originX = 22;
            double originY = Math.Max(42, previewCanvas.ActualHeight - 22);

            AddPreviewLine(originX, originY, originX + 34, originY);
            AddPreviewLine(originX, originY, originX, originY - 34);
            AddPreviewLabel(horizontalAxis, originX + 40, originY, "#64748B");
            AddPreviewLabel(verticalAxis, originX, originY - 41, "#64748B");
        }

        private void AddPreviewLine(double x1, double y1, double x2, double y2)
        {
            System.Windows.Shapes.Line line = new System.Windows.Shapes.Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = ParseBrush("#64748B"),
                StrokeThickness = 1.5
            };
            previewCanvas.Children.Add(line);
        }

        private void DrawPreviewMessage(string message)
        {
            if (previewCanvas.ActualWidth < 20 || previewCanvas.ActualHeight < 20)
                return;

            TextBlock text = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                FontSize = 12
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(
                text,
                Math.Max(8, (previewCanvas.ActualWidth - text.DesiredSize.Width) / 2));
            Canvas.SetTop(
                text,
                Math.Max(8, (previewCanvas.ActualHeight - text.DesiredSize.Height) / 2));
            previewCanvas.Children.Add(text);
        }

        private static Brush ParseBrush(string color)
        {
            return (Brush)new BrushConverter().ConvertFromString(color);
        }

        private class PreviewTransform
        {
            private readonly double _minX;
            private readonly double _maxY;
            private readonly double _offsetX;
            private readonly double _offsetY;

            public double Scale { get; }

            public PreviewTransform(
                double minX,
                double maxY,
                double scale,
                double offsetX,
                double offsetY)
            {
                _minX = minX;
                _maxY = maxY;
                Scale = scale;
                _offsetX = offsetX;
                _offsetY = offsetY;
            }

            public double MapX(double value)
            {
                return _offsetX + (value - _minX) * Scale;
            }

            public double MapY(double value)
            {
                return _offsetY + (_maxY - value) * Scale;
            }
        }
    }
}
