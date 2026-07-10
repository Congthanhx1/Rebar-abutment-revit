using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vetheprevit.MoCau
{
    public partial class MoCauRebarUI
    {
        private const string BottomRebarColor = "#2563EB";
        private const string TopRebarColor = "#DC2626";
        private const string FrontRebarColor = "#E11D48";
        private const string BackRebarColor = "#7C3AED";
        private const string HorizontalRebarColor = "#D97706";

        private void AttachPreviewEvents()
        {
            TextBox[] textBoxes =
            {
                txtCoverBotZLong, txtCoverBotZTrans, txtCoverBotLongX,
                txtBotMeshAnchorLong, txtBotMeshAnchorTrans,
                txtSpaceLongBot, txtQtyLongBot, txtSpaceTransBot, txtQtyTransBot,
                txtCoverTopZLong, txtCoverTopZTrans, txtCoverTopLongX,
                txtTopMeshAnchorLong, txtTopMeshAnchorTrans,
                txtSpaceLongTop, txtQtyLongTop, txtSpaceTransTop, txtQtyTransTop,
                txtCoverAntiBurstX, txtCoverAntiBurstY, txtCoverAntiBurstZ,
                txtAntiBurstHeight, txtAntiBurstAnchor,
                txtSpacingSequenceAntiBurstX,
                txtSpacingSequenceAntiBurstY,
                txtCoverStemVertFront, txtCoverStemVertFrontZ, txtSpaceStemVertFront, txtQtyStemVertFront,
                txtCoverStemVertBack, txtCoverStemVertBackZ, txtSpaceStemVertBack, txtQtyStemVertBack,
                txtCoverStemHorizX, txtCoverStemHorizY, txtCoverStemHorizZ,
                txtSpaceStemHoriz, txtQtyStemHoriz
            };

            foreach (TextBox textBox in textBoxes)
            {
                if (textBox != null)
                    textBox.TextChanged += (sender, args) => DrawPreview2D();
            }

            ComboBox[] comboBoxes =
            {
                cboLayoutLBot, cboLayoutTBot,
                cboLayoutLTop, cboLayoutTTop,
                cboLayoutStemVertFront, cboLayoutStemVertBack,
                cboLayoutStemHoriz
            };

            foreach (ComboBox comboBox in comboBoxes)
            {
                if (comboBox != null)
                    comboBox.SelectionChanged += (sender, args) => DrawPreview2D();
            }

            CheckBox[] checkBoxes =
            {
                chkDrawBotRebar, chkDrawTopRebar, chkSameTop,
                chkDrawHorizSideRebar, chkDrawHorizSideX, chkDrawHorizSideY,
                chkDrawAntiBurstRebar, chkDrawAntiBurstX, chkDrawAntiBurstY,
                chkDrawStemVertFront, chkDrawStemVertBack, chkDrawStemHoriz
            };

            foreach (CheckBox checkBox in checkBoxes)
            {
                if (checkBox == null) continue;
                checkBox.Checked += (sender, args) => DrawPreview2D();
                checkBox.Unchecked += (sender, args) => DrawPreview2D();
            }
        }

        private void DrawPlanRebarOverlay(
            bool showStem,
            PreviewTransform transform,
            double minL,
            double maxL,
            double minT,
            double maxT)
        {
            if (showStem)
            {
                DrawStemPlanRebar(transform, minL, maxL, minT, maxT);
                return;
            }

            if (chkDrawBotRebar?.IsChecked == true)
            {
                DrawFootingPlanGrid(
                    transform,
                    minL,
                    maxL,
                    minT,
                    maxT,
                    ToInternal(txtCoverBotLongX),
                    cboLayoutLBot,
                    txtSpaceLongBot,
                    txtQtyLongBot,
                    cboLayoutTBot,
                    txtSpaceTransBot,
                    txtQtyTransBot,
                    BottomRebarColor,
                    false);
            }

            if (chkDrawTopRebar?.IsChecked == true)
            {
                DrawFootingPlanGrid(
                    transform,
                    minL,
                    maxL,
                    minT,
                    maxT,
                    ToInternal(txtCoverTopLongX),
                    cboLayoutLTop,
                    txtSpaceLongTop,
                    txtQtyLongTop,
                    cboLayoutTTop,
                    txtSpaceTransTop,
                    txtQtyTransTop,
                    TopRebarColor,
                    true);
            }

            DrawAntiBurstPlanOverlay(
                transform,
                minL,
                maxL,
                minT,
                maxT);
        }

        private void DrawFootingPlanGrid(
            PreviewTransform transform,
            double minL,
            double maxL,
            double minT,
            double maxT,
            double cover,
            ComboBox layoutAlongL,
            TextBox spacingAlongL,
            TextBox quantityAlongL,
            ComboBox layoutAlongT,
            TextBox spacingAlongT,
            TextBox quantityAlongT,
            string color,
            bool dashed)
        {
            double trueMinL = minL + cover;
            double trueMaxL = maxL - cover;
            double trueMinT = minT + cover;
            double trueMaxT = maxT - cover;
            if (trueMaxL <= trueMinL || trueMaxT <= trueMinT) return;

            foreach (double t in GetLayoutPositions(
                trueMinT,
                trueMaxT,
                layoutAlongT,
                spacingAlongT,
                quantityAlongT))
            {
                AddModelLine(
                    transform,
                    trueMinL,
                    t,
                    trueMaxL,
                    t,
                    color,
                    1.2,
                    dashed);
            }

            foreach (double l in GetLayoutPositions(
                trueMinL,
                trueMaxL,
                layoutAlongL,
                spacingAlongL,
                quantityAlongL))
            {
                AddModelLine(
                    transform,
                    l,
                    trueMinT,
                    l,
                    trueMaxT,
                    color,
                    1.2,
                    dashed);
            }
        }

        private void DrawStemPlanRebar(
            PreviewTransform transform,
            double minL,
            double maxL,
            double minT,
            double maxT)
        {
            if (chkDrawStemVertFront?.IsChecked == true)
            {
                double t = minT + ToInternal(txtCoverStemVertFront);
                foreach (double l in GetLayoutPositions(
                    minL,
                    maxL,
                    cboLayoutStemVertFront,
                    txtSpaceStemVertFront,
                    txtQtyStemVertFront))
                {
                    AddModelCircle(transform, l, t, FrontRebarColor, 3.2);
                }
            }

            if (chkDrawStemVertBack?.IsChecked == true)
            {
                double t = maxT - ToInternal(txtCoverStemVertBack);
                foreach (double l in GetLayoutPositions(
                    minL,
                    maxL,
                    cboLayoutStemVertBack,
                    txtSpaceStemVertBack,
                    txtQtyStemVertBack))
                {
                    AddModelCircle(transform, l, t, BackRebarColor, 3.2);
                }
            }

            if (chkDrawStemHoriz?.IsChecked == true)
            {
                double edgeOffset = ToInternal(txtCoverStemHorizY);
                double startL = minL + edgeOffset;
                double endL = maxL - edgeOffset;
                double faceOffset = ToInternal(txtCoverStemHorizX);
                if (endL > startL)
                {
                    AddModelLine(
                        transform,
                        startL,
                        minT + faceOffset,
                        endL,
                        minT + faceOffset,
                        HorizontalRebarColor,
                        2,
                        false);
                    AddModelLine(
                        transform,
                        startL,
                        maxT - faceOffset,
                        endL,
                        maxT - faceOffset,
                        HorizontalRebarColor,
                        2,
                        false);
                }
            }
        }

        private void DrawFrontRebarOverlay(
            bool showStem,
            PreviewTransform transform,
            double minL,
            double maxL,
            double minZ,
            double maxZ)
        {
            if (showStem)
            {
                if (chkDrawStemVertFront?.IsChecked == true)
                {
                    foreach (double l in GetLayoutPositions(
                        minL,
                        maxL,
                        cboLayoutStemVertFront,
                        txtSpaceStemVertFront,
                        txtQtyStemVertFront))
                    {
                        AddModelLine(
                            transform,
                            l,
                            minZ,
                            l,
                            maxZ - ToInternal(txtCoverStemVertFrontZ),
                            FrontRebarColor,
                            1.5,
                            false);
                    }
                }

                if (chkDrawStemVertBack?.IsChecked == true)
                {
                    foreach (double l in GetLayoutPositions(
                        minL,
                        maxL,
                        cboLayoutStemVertBack,
                        txtSpaceStemVertBack,
                        txtQtyStemVertBack))
                    {
                        AddModelLine(
                            transform,
                            l,
                            minZ,
                            l,
                            maxZ - ToInternal(txtCoverStemVertBackZ),
                            BackRebarColor,
                            1.2,
                            true);
                    }
                }

                if (chkDrawStemHoriz?.IsChecked == true)
                {
                    double startZ = minZ + ToInternal(txtCoverStemHorizZ);
                    foreach (double z in GetLayoutPositions(
                        startZ,
                        maxZ,
                        cboLayoutStemHoriz,
                        txtSpaceStemHoriz,
                        txtQtyStemHoriz))
                    {
                        AddModelLine(
                            transform,
                            minL + ToInternal(txtCoverStemHorizY),
                            z,
                            maxL - ToInternal(txtCoverStemHorizY),
                            z,
                            HorizontalRebarColor,
                            1.5,
                            false);
                    }
                }
                return;
            }

            DrawFootingElevationRebar(
                transform,
                minL,
                maxL,
                minZ,
                maxZ,
                true);
            DrawAntiBurstElevationOverlay(
                transform,
                minL,
                maxL,
                minZ,
                maxZ,
                true);
        }

        private void DrawSideRebarOverlay(
            bool showStem,
            PreviewTransform transform,
            double minT,
            double maxT,
            double minZ,
            double maxZ)
        {
            if (showStem)
            {
                if (chkDrawStemVertFront?.IsChecked == true)
                {
                    double t = minT + ToInternal(txtCoverStemVertFront);
                    AddModelLine(
                        transform,
                        t,
                        minZ,
                        t,
                        maxZ - ToInternal(txtCoverStemVertFrontZ),
                        FrontRebarColor,
                        2,
                        false);
                }

                if (chkDrawStemVertBack?.IsChecked == true)
                {
                    double t = maxT - ToInternal(txtCoverStemVertBack);
                    AddModelLine(
                        transform,
                        t,
                        minZ,
                        t,
                        maxZ - ToInternal(txtCoverStemVertBackZ),
                        BackRebarColor,
                        2,
                        false);
                }

                if (chkDrawStemHoriz?.IsChecked == true)
                {
                    double frontT = minT + ToInternal(txtCoverStemHorizX);
                    double backT = maxT - ToInternal(txtCoverStemHorizX);
                    double startZ = minZ + ToInternal(txtCoverStemHorizZ);
                    foreach (double z in GetLayoutPositions(
                        startZ,
                        maxZ,
                        cboLayoutStemHoriz,
                        txtSpaceStemHoriz,
                        txtQtyStemHoriz))
                    {
                        AddModelCircle(
                            transform,
                            frontT,
                            z,
                            HorizontalRebarColor,
                            3);
                        AddModelCircle(
                            transform,
                            backT,
                            z,
                            HorizontalRebarColor,
                            3);
                    }
                }
                return;
            }

            DrawFootingElevationRebar(
                transform,
                minT,
                maxT,
                minZ,
                maxZ,
                false);
            DrawAntiBurstElevationOverlay(
                transform,
                minT,
                maxT,
                minZ,
                maxZ,
                false);
        }

        private void DrawAntiBurstPlanOverlay(
            PreviewTransform transform,
            double minL,
            double maxL,
            double minT,
            double maxT)
        {
            if (chkDrawAntiBurstRebar?.IsChecked != true) return;

            double coverX = ToInternal(txtCoverAntiBurstX);
            double coverY = ToInternal(txtCoverAntiBurstY);
            double startL = minL + coverX;
            double endL = maxL;
            double startT = minT + coverY;
            double endT = maxT;
            if (endL <= startL || endT < startT) return;

            if (chkDrawAntiBurstX?.IsChecked == true)
            {
                foreach (double l in GetSequencePositions(
                    startL,
                    endL,
                    txtSpacingSequenceAntiBurstX,
                    "X"))
                {
                    AddModelCircle(transform, l, startT, "#0891B2", 3);
                    AddModelCircle(transform, l, endT, "#0891B2", 3);
                }
            }

            if (chkDrawAntiBurstY?.IsChecked == true)
            {
                foreach (double t in GetSequencePositions(
                    startT,
                    endT,
                    txtSpacingSequenceAntiBurstY,
                    "Y"))
                {
                    AddModelCircle(transform, startL, t, "#06B6D4", 3);
                    AddModelCircle(transform, endL, t, "#06B6D4", 3);
                }
            }
        }

        private void DrawAntiBurstElevationOverlay(
            PreviewTransform transform,
            double minX,
            double maxX,
            double minZ,
            double maxZ,
            bool frontView)
        {
            if (chkDrawAntiBurstRebar?.IsChecked != true) return;

            bool drawDirection = frontView
                ? chkDrawAntiBurstX?.IsChecked == true
                : chkDrawAntiBurstY?.IsChecked == true;
            if (!drawDirection) return;

            TextBox sequence = frontView
                ? txtSpacingSequenceAntiBurstX
                : txtSpacingSequenceAntiBurstY;

            double coverHorizontal = ToInternal(
                frontView ? txtCoverAntiBurstX : txtCoverAntiBurstY);
            double coverZ = ToInternal(txtCoverAntiBurstZ);
            double startX = minX + coverHorizontal;
            double endX = maxX;
            bool autoHeight = chkAutoAntiBurstHeight?.IsChecked == true;
            double startZ = autoHeight
                ? minZ + Math.Min(
                    ToInternal(txtCoverBotZLong),
                    ToInternal(txtCoverBotZTrans))
                : minZ + coverZ;
            double endZ = autoHeight
                ? maxZ - Math.Min(
                    ToInternal(txtCoverTopZLong),
                    ToInternal(txtCoverTopZTrans))
                : Math.Min(maxZ - coverZ, startZ + ToInternal(txtAntiBurstHeight));
            if (endX <= startX || endZ <= startZ) return;

            foreach (double x in GetSequencePositions(
                startX,
                endX,
                sequence,
                frontView ? "X" : "Y"))
            {
                AddModelLine(
                    transform,
                    x,
                    startZ,
                    x,
                    endZ,
                    "#0891B2",
                    1.5,
                    false);
            }
        }

        private void DrawFootingElevationRebar(
            PreviewTransform transform,
            double minX,
            double maxX,
            double minZ,
            double maxZ,
            bool frontView)
        {
            if (chkDrawBotRebar?.IsChecked == true)
            {
                double zLine = minZ + ToInternal(
                    frontView ? txtCoverBotZLong : txtCoverBotZTrans);
                double startX = minX + ToInternal(txtCoverBotLongX);
                TextBox anchorTextBox = frontView
                    ? txtBotMeshAnchorLong
                    : txtBotMeshAnchorTrans;
                AddModelLine(
                    transform,
                    startX,
                    zLine,
                    maxX - ToInternal(txtCoverBotLongX),
                    zLine,
                    BottomRebarColor,
                    2,
                    false);
                AddModelLine(
                    transform,
                    startX,
                    zLine,
                    startX,
                    Math.Min(maxZ, zLine + ToInternal(anchorTextBox)),
                    BottomRebarColor,
                    2,
                    false);
            }

            if (chkDrawTopRebar?.IsChecked == true)
            {
                double zLine = maxZ - ToInternal(
                    frontView ? txtCoverTopZLong : txtCoverTopZTrans);
                double startX = minX + ToInternal(txtCoverTopLongX);
                TextBox anchorTextBox;
                if (chkSameTop?.IsChecked == true)
                {
                    anchorTextBox = frontView
                        ? txtBotMeshAnchorLong
                        : txtBotMeshAnchorTrans;
                }
                else
                {
                    anchorTextBox = frontView
                        ? txtTopMeshAnchorLong
                        : txtTopMeshAnchorTrans;
                }
                AddModelLine(
                    transform,
                    startX,
                    zLine,
                    maxX - ToInternal(txtCoverTopLongX),
                    zLine,
                    TopRebarColor,
                    2,
                    false);
                AddModelLine(
                    transform,
                    startX,
                    zLine,
                    startX,
                    Math.Max(minZ, zLine - ToInternal(anchorTextBox)),
                    TopRebarColor,
                    2,
                    false);
            }
        }

        private IEnumerable<double> GetLayoutPositions(
            double minimum,
            double maximum,
            ComboBox layoutCombo,
            TextBox spacingTextBox,
            TextBox quantityTextBox)
        {
            const int maxPreviewBars = 80;
            if (maximum < minimum) yield break;

            double length = maximum - minimum;
            int layoutMode = layoutCombo?.SelectedIndex ?? 0;
            double spacing = Math.Max(ToInternal(spacingTextBox), 1.0 / 304.8);
            int quantity = Math.Max(1, (int)Math.Round(ReadDouble(quantityTextBox, 2)));

            if (layoutMode == 0)
            {
                quantity = Math.Max(2, (int)Math.Ceiling(length / spacing) + 1);
                quantity = Math.Min(quantity, maxPreviewBars);
                spacing = quantity > 1 ? length / (quantity - 1) : 0;
            }
            else if (layoutMode == 1)
            {
                quantity = Math.Min(Math.Max(2, quantity), maxPreviewBars);
                spacing = quantity > 1 ? length / (quantity - 1) : 0;
            }
            else
            {
                quantity = Math.Min(quantity, maxPreviewBars);
            }

            for (int i = 0; i < quantity; i++)
            {
                double position = minimum + i * spacing;
                if (position > maximum + 0.001) yield break;
                yield return position;
            }
        }

        private IEnumerable<double> GetSequencePositions(
            double minimum,
            double maximum,
            TextBox sequenceTextBox,
            string directionName)
        {
            if (maximum < minimum) yield break;

            IList<double> offsets;
            try
            {
                offsets = SpacingSequence.GetOffsetsInternal(
                    sequenceTextBox?.Text,
                    maximum - minimum,
                    directionName);
            }
            catch
            {
                yield break;
            }

            foreach (double offset in offsets)
                yield return minimum + offset;
        }

        private void AddModelLine(
            PreviewTransform transform,
            double x1,
            double y1,
            double x2,
            double y2,
            string color,
            double thickness,
            bool dashed)
        {
            System.Windows.Shapes.Line line = new System.Windows.Shapes.Line
            {
                X1 = transform.MapX(x1),
                Y1 = transform.MapY(y1),
                X2 = transform.MapX(x2),
                Y2 = transform.MapY(y2),
                Stroke = ParseBrush(color),
                StrokeThickness = thickness,
                Opacity = 0.9
            };
            if (dashed)
                line.StrokeDashArray = new DoubleCollection { 4, 3 };
            previewCanvas.Children.Add(line);
        }

        private void AddModelCircle(
            PreviewTransform transform,
            double x,
            double y,
            string color,
            double radius)
        {
            System.Windows.Shapes.Ellipse circle =
                new System.Windows.Shapes.Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = ParseBrush(color),
                    Stroke = Brushes.White,
                    StrokeThickness = 0.5
                };
            Canvas.SetLeft(circle, transform.MapX(x) - radius);
            Canvas.SetTop(circle, transform.MapY(y) - radius);
            previewCanvas.Children.Add(circle);
        }

        private static double ToInternal(TextBox textBox)
        {
            return ReadDouble(textBox, 0) / 304.8;
        }

        private static double ReadDouble(TextBox textBox, double fallback)
        {
            string text = textBox?.Text;
            if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out double value))
            {
                return value;
            }

            if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return fallback;
        }
    }
}


