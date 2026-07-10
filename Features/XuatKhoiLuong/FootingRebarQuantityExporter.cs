using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Vetheprevit.TienIch;

namespace Vetheprevit.MoCau
{
    public class FootingRebarQuantityRow
    {
        public string ComponentName { get; set; }
        public string BarMark { get; set; }
        public string ShapeAndDimensions { get; set; }
        public double DiameterMm { get; set; }
        public double UnitLengthMm { get; set; }
        public int ComponentCount { get; set; }
        public int BarsPerComponent { get; set; }
        public double TotalLengthM { get; set; }
        public double TotalWeightKg { get; set; }
        public byte[] ShapeImagePng { get; set; }
    }

    public static class FootingRebarQuantityExporter
    {
        private static readonly HashSet<string> FootingGroupCodes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "VT_Bot", "VT_BotLong", "VT_BotTrans",
                "VT_Top", "VT_TopLong", "VT_TopTrans",
                "VT_VertSide", "VT_VertSideX", "VT_VertSideY",
                "VT_HorizSide", "VT_Dowel",
                "VT_AntiBurst", "VT_AntiBurstX", "VT_AntiBurstY"
            };

        public static IList<FootingRebarQuantityRow> Collect(
            Document doc,
            FamilyInstance abutment,
            ISet<string> selectedGroupCodes = null,
            IDictionary<string, string> namesByGroup = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (abutment == null) throw new ArgumentNullException(nameof(abutment));

            RebarHostData hostData = RebarHostData.GetRebarHostData(abutment);
            if (hostData == null)
                throw new Exception("Family mố không phải Rebar Host.");

            List<RawRebarQuantity> rawItems = new List<RawRebarQuantity>();
            foreach (Rebar rebar in hostData.GetRebarsInHost())
            {
                RebarMarkerData marker = RebarMarkerService.GetMarker(rebar);
                if (marker == null || marker.Creator != "RebarAbutmentTool") continue;
                string groupCode = marker.GroupCode;
                
                if (!FootingGroupCodes.Contains(groupCode)) continue;
                if (selectedGroupCodes != null &&
                    selectedGroupCodes.Count > 0 &&
                    !selectedGroupCodes.Contains(groupCode))
                {
                    continue;
                }

                string scheduleMark = GetString(
                    rebar,
                    BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK);
                if (namesByGroup != null &&
                    namesByGroup.TryGetValue(groupCode, out string configuredName) &&
                    !string.IsNullOrWhiteSpace(configuredName))
                {
                    scheduleMark = configuredName.Trim();
                }
                if (string.IsNullOrWhiteSpace(scheduleMark))
                    scheduleMark = groupCode;

                double diameterInternal = GetDouble(
                    rebar,
                    BuiltInParameter.REBAR_INSTANCE_BAR_MODEL_DIAMETER);
                if (diameterInternal <= 0)
                {
                    diameterInternal = GetDouble(
                        rebar,
                        BuiltInParameter.REBAR_INSTANCE_BAR_DIAMETER);
                }

                double unitLengthInternal = GetDouble(
                    rebar,
                    BuiltInParameter.REBAR_ELEM_LENGTH);
                int quantity = Math.Max(
                    1,
                    GetInteger(
                        rebar,
                        BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS));
                string shapeName = GetString(
                    rebar,
                    BuiltInParameter.REBAR_SHAPE);
                double unitLengthMm = UnitUtils.ConvertFromInternalUnits(
                    unitLengthInternal,
                    UnitTypeId.Millimeters);

                double measuredDiameterMm =
                    UnitUtils.ConvertFromInternalUnits(
                        diameterInternal,
                        UnitTypeId.Millimeters);
                double nominalDiameterMm =
                    GetNominalDiameterMm(
                        rebar,
                        measuredDiameterMm);

                rawItems.Add(new RawRebarQuantity
                {
                    GroupCode = groupCode,
                    ScheduleMark = scheduleMark,
                    ShapeName = shapeName,
                    DiameterMm = nominalDiameterMm,
                    UnitLengthMm = unitLengthMm,
                    Quantity = quantity,
                    ShapeImagePng = RenderRebarShape(
                        rebar,
                        unitLengthMm)
                });
            }

            string componentName =
                $"{abutment.Name} (ID {abutment.Id.Value})";
            return rawItems
                .GroupBy(item => new
                {
                    item.ScheduleMark,
                    item.GroupCode,
                    item.ShapeName,
                    Diameter = Math.Round(item.DiameterMm, 1),
                    Length = Math.Round(item.UnitLengthMm, 0)
                })
                .OrderBy(group => group.Key.ScheduleMark)
                .ThenBy(group => group.Key.GroupCode)
                .Select(group =>
                {
                    int totalBars = group.Sum(item => item.Quantity);
                    double totalLengthM =
                        group.Sum(item => item.UnitLengthMm * item.Quantity) / 1000.0;
                    double diameterMm = group.First().DiameterMm;
                    byte[] representativeImage = group
                        .Select(item => item.ShapeImagePng)
                        .FirstOrDefault(image => image != null && image.Length > 0);
                    return new FootingRebarQuantityRow
                    {
                        ComponentName = componentName,
                        BarMark = group.Key.ScheduleMark,
                        ShapeAndDimensions = representativeImage == null
                            ? BuildShapeDescription(
                                group.Key.ShapeName,
                                group.First().UnitLengthMm)
                            : "",
                        DiameterMm = diameterMm,
                        UnitLengthMm = group.First().UnitLengthMm,
                        ComponentCount = 1,
                        BarsPerComponent = totalBars,
                        TotalLengthM = totalLengthM,
                        TotalWeightKg =
                            totalLengthM * diameterMm * diameterMm / 162.0,
                        ShapeImagePng = representativeImage
                    };
                })
                .ToList();
        }

        public static void ExportXlsx(
            string filePath,
            IList<FootingRebarQuantityRow> rows)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn xuất Excel không hợp lệ.");
            if (rows == null || rows.Count == 0)
                throw new Exception("Không có thép bệ mố do tool tạo để thống kê.");

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None))
            using (ZipArchive archive = new ZipArchive(
                stream,
                ZipArchiveMode.Create))
            {
                List<FootingRebarQuantityRow> imageRows = rows
                    .Where(row => row.ShapeImagePng != null &&
                                  row.ShapeImagePng.Length > 0)
                    .ToList();
                AddEntry(
                    archive,
                    "[Content_Types].xml",
                    BuildContentTypesXml(imageRows.Count > 0));
                AddEntry(archive, "_rels/.rels", RootRelationshipsXml);
                AddEntry(archive, "xl/workbook.xml", WorkbookXml);
                AddEntry(
                    archive,
                    "xl/_rels/workbook.xml.rels",
                    WorkbookRelationshipsXml);
                AddEntry(archive, "xl/styles.xml", StylesXml);
                AddEntry(
                    archive,
                    "xl/worksheets/sheet1.xml",
                    BuildWorksheetXml(rows, imageRows.Count > 0));
                if (imageRows.Count > 0)
                {
                    AddEntry(
                        archive,
                        "xl/worksheets/_rels/sheet1.xml.rels",
                        SheetRelationshipsXml);
                    AddEntry(
                        archive,
                        "xl/drawings/drawing1.xml",
                        BuildDrawingXml(rows));
                    AddEntry(
                        archive,
                        "xl/drawings/_rels/drawing1.xml.rels",
                        BuildDrawingRelationshipsXml(imageRows.Count));

                    int imageIndex = 1;
                    foreach (FootingRebarQuantityRow row in rows)
                    {
                        if (row.ShapeImagePng == null ||
                            row.ShapeImagePng.Length == 0)
                        {
                            continue;
                        }

                        AddBinaryEntry(
                            archive,
                            $"xl/media/image{imageIndex++}.png",
                            row.ShapeImagePng);
                    }
                }
            }
        }

        private static byte[] RenderRebarShape(
            Rebar rebar,
            double unitLengthMm)
        {
            try
            {
                IList<Curve> curves = rebar.GetCenterlineCurves(
                    false,
                    false,
                    false,
                    MultiplanarOption.IncludeOnlyPlanarCurves,
                    0);
                List<XYZ> points = new List<XYZ>();
                foreach (Curve curve in curves)
                {
                    foreach (XYZ point in curve.Tessellate())
                    {
                        if (points.Count == 0 ||
                            !points[points.Count - 1].IsAlmostEqualTo(point))
                        {
                            points.Add(point);
                        }
                    }
                }

                if (points.Count < 2) return null;

                XYZ origin = points[0];
                XYZ axisU = null;
                XYZ normal = null;
                for (int i = 1; i < points.Count && normal == null; i++)
                {
                    XYZ first = points[i] - origin;
                    if (first.GetLength() < 1e-6) continue;
                    axisU = first.Normalize();
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        XYZ second = points[j] - origin;
                        XYZ cross = first.CrossProduct(second);
                        if (cross.GetLength() > 1e-6)
                        {
                            normal = cross.Normalize();
                            break;
                        }
                    }
                }

                if (axisU == null) axisU = XYZ.BasisX;
                if (normal == null)
                {
                    normal = Math.Abs(axisU.DotProduct(XYZ.BasisZ)) < 0.9
                        ? axisU.CrossProduct(XYZ.BasisZ).Normalize()
                        : axisU.CrossProduct(XYZ.BasisX).Normalize();
                }
                XYZ axisV = normal.CrossProduct(axisU).Normalize();

                List<System.Windows.Point> projected = points
                    .Select(point =>
                    {
                        XYZ relative = point - origin;
                        return new System.Windows.Point(
                            relative.DotProduct(axisU),
                            relative.DotProduct(axisV));
                    })
                    .ToList();

                double minX = projected.Min(point => point.X);
                double maxX = projected.Max(point => point.X);
                double minY = projected.Min(point => point.Y);
                double maxY = projected.Max(point => point.Y);

                // Standardize schedule symbols so the bar's longest direction
                // is displayed horizontally, matching conventional bar schedules.
                if ((maxY - minY) > (maxX - minX))
                {
                    projected = projected
                        .Select(point => new System.Windows.Point(
                            point.Y,
                            -point.X))
                        .ToList();
                    minX = projected.Min(point => point.X);
                    maxX = projected.Max(point => point.X);
                    minY = projected.Min(point => point.Y);
                    maxY = projected.Max(point => point.Y);
                }

                double width = Math.Max(maxX - minX, 0.001);
                double height = Math.Max(maxY - minY, 0.001);
                const int pixelWidth = 300;
                const int pixelHeight = 105;
                const double horizontalPadding = 12;
                const double shapeTop = 7;
                const double shapeHeight = 56;
                double scale = Math.Min(
                    (pixelWidth - 2 * horizontalPadding) / width,
                    shapeHeight / height);
                double renderedWidth = width * scale;
                double renderedHeight = height * scale;
                double offsetX = (pixelWidth - renderedWidth) / 2.0;
                double offsetY =
                    shapeTop + (shapeHeight - renderedHeight) / 2.0;
                List<System.Windows.Point> mappedPoints = projected
                    .Select(point => MapShapePoint(
                        point,
                        minX,
                        maxY,
                        scale,
                        offsetX,
                        offsetY))
                    .ToList();

                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext context = visual.RenderOpen())
                {
                    context.DrawRectangle(
                        Brushes.White,
                        null,
                        new Rect(0, 0, pixelWidth, pixelHeight));
                    Pen pen = new Pen(
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 83, 110)),
                        2.2);
                    pen.StartLineCap = PenLineCap.Round;
                    pen.EndLineCap = PenLineCap.Round;
                    pen.LineJoin = PenLineJoin.Round;

                    for (int i = 0; i < projected.Count - 1; i++)
                    {
                        context.DrawLine(
                            pen,
                            mappedPoints[i],
                            mappedPoints[i + 1]);
                    }

                    DrawOverallLengthDimension(
                        context,
                        mappedPoints,
                        unitLengthMm,
                        pixelWidth);
                }

                RenderTargetBitmap bitmap = new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(visual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static System.Windows.Point MapShapePoint(
            System.Windows.Point point,
            double minX,
            double maxY,
            double scale,
            double offsetX,
            double offsetY)
        {
            return new System.Windows.Point(
                offsetX + (point.X - minX) * scale,
                offsetY + (maxY - point.Y) * scale);
        }

        private static void DrawOverallLengthDimension(
            DrawingContext context,
            IList<System.Windows.Point> shapePoints,
            double unitLengthMm,
            double pixelWidth)
        {
            if (context == null ||
                shapePoints == null ||
                shapePoints.Count < 2 ||
                unitLengthMm <= 0)
            {
                return;
            }

            const double dimensionY = 75;
            double left = Math.Max(
                12,
                shapePoints.Min(point => point.X));
            double right = Math.Min(
                pixelWidth - 12,
                shapePoints.Max(point => point.X));
            if (right - left < 30)
            {
                left = 24;
                right = pixelWidth - 24;
            }

            System.Windows.Point leftShapePoint = shapePoints
                .OrderBy(point => point.X)
                .First();
            System.Windows.Point rightShapePoint = shapePoints
                .OrderByDescending(point => point.X)
                .First();

            Brush dimensionBrush = new SolidColorBrush(
                System.Windows.Media.Color.FromRgb(71, 85, 105));
            Pen dimensionPen = new Pen(dimensionBrush, 1.0);
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(left, dimensionY),
                new System.Windows.Point(right, dimensionY));
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(left, leftShapePoint.Y + 3),
                new System.Windows.Point(left, dimensionY + 3));
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(right, rightShapePoint.Y + 3),
                new System.Windows.Point(right, dimensionY + 3));

            const double arrowLength = 5;
            const double arrowHalfHeight = 2.7;
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(left, dimensionY),
                new System.Windows.Point(
                    left + arrowLength,
                    dimensionY - arrowHalfHeight));
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(left, dimensionY),
                new System.Windows.Point(
                    left + arrowLength,
                    dimensionY + arrowHalfHeight));
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(right, dimensionY),
                new System.Windows.Point(
                    right - arrowLength,
                    dimensionY - arrowHalfHeight));
            context.DrawLine(
                dimensionPen,
                new System.Windows.Point(right, dimensionY),
                new System.Windows.Point(
                    right - arrowLength,
                    dimensionY + arrowHalfHeight));

            string dimensionText =
                $"L = {Math.Round(unitLengthMm):0} mm";
            System.Windows.Media.FormattedText formattedText =
                new System.Windows.Media.FormattedText(
                dimensionText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10.5,
                dimensionBrush,
                1.0);
            double textX =
                (pixelWidth - formattedText.WidthIncludingTrailingWhitespace) / 2.0;
            double textY = 82;
            context.DrawRectangle(
                Brushes.White,
                null,
                new Rect(
                    textX - 3,
                    textY - 1,
                    formattedText.WidthIncludingTrailingWhitespace + 6,
                    formattedText.Height + 2));
            context.DrawText(
                formattedText,
                new System.Windows.Point(textX, textY));
        }

        private static string BuildShapeDescription(
            string shapeName,
            double unitLengthMm)
        {
            string shape = string.IsNullOrWhiteSpace(shapeName)
                ? "Theo hình học thanh"
                : shapeName;
            return $"{shape} — L={unitLengthMm:0} mm";
        }

        private static string GetString(
            Element element,
            BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter?.AsString() ??
                   parameter?.AsValueString() ??
                   "";
        }

        private static double GetDouble(
            Element element,
            BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter?.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0;
        }

        private static int GetInteger(
            Element element,
            BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter?.StorageType == StorageType.Integer
                ? parameter.AsInteger()
                : 0;
        }

        private static double GetNominalDiameterMm(
            Rebar rebar,
            double measuredDiameterMm)
        {
            string typeName = "";
            try
            {
                Element typeElement =
                    rebar?.Document?.GetElement(rebar.GetTypeId());
                typeName = typeElement?.Name ?? "";
            }
            catch
            {
                typeName = "";
            }

            List<double> candidates = Regex
                .Matches(typeName, @"\d+(?:[.,]\d+)?")
                .Cast<Match>()
                .Select(match =>
                {
                    double.TryParse(
                        match.Value.Replace(',', '.'),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value);
                    return value;
                })
                .Where(value => value >= 4 && value <= 100)
                .OrderBy(value => Math.Abs(value - measuredDiameterMm))
                .ToList();

            if (candidates.Count > 0)
            {
                double candidate = candidates[0];
                double tolerance = Math.Max(
                    2.0,
                    measuredDiameterMm * 0.2);
                if (Math.Abs(candidate - measuredDiameterMm) <= tolerance)
                    return candidate;
            }

            double rounded = Math.Round(measuredDiameterMm);
            return Math.Abs(measuredDiameterMm - rounded) <= 0.65
                ? rounded
                : measuredDiameterMm;
        }

        private static string BuildWorksheetXml(
            IList<FootingRebarQuantityRow> rows,
            bool hasDrawing)
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            xml.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            xml.Append("<pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/>");
            xml.Append("</sheetView></sheetViews>");
            xml.Append("<cols>");
            int[] widths = { 22, 12, 34, 14, 18, 12, 18, 18, 18 };
            for (int i = 0; i < widths.Length; i++)
            {
                xml.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "<col min=\"{0}\" max=\"{0}\" width=\"{1}\" customWidth=\"1\"/>",
                    i + 1,
                    widths[i]);
            }
            xml.Append("</cols><sheetData>");

            AddRow(
                xml,
                1,
                1,
                "BẢNG THỐNG KÊ KHỐI LƯỢNG THÉP BỆ MỐ");
            string[] headers =
            {
                "TÊN CẤU KIỆN",
                "SỐ HIỆU",
                "HÌNH DẠNG - KÍCH THƯỚC",
                "ĐƯỜNG KÍNH (mm)",
                "CHIỀU DÀI 1 THANH (mm)",
                "SỐ CẤU KIỆN",
                "SỐ THANH / CẤU KIỆN",
                "TỔNG CHIỀU DÀI (m)",
                "TỔNG TRỌNG LƯỢNG (kg)"
            };
            AddRow(xml, 2, 2, headers);

            int rowIndex = 3;
            foreach (FootingRebarQuantityRow row in rows)
            {
                AddRow(
                    xml,
                    rowIndex++,
                    3,
                    row.ComponentName,
                    row.BarMark,
                    row.ShapeAndDimensions,
                    row.DiameterMm,
                    row.UnitLengthMm,
                    row.ComponentCount,
                    row.BarsPerComponent,
                    row.TotalLengthM,
                    row.TotalWeightKg);
            }

            AddRow(
                xml,
                rowIndex,
                4,
                "TỔNG CỘNG",
                "",
                "",
                "",
                "",
                "",
                "",
                rows.Sum(row => row.TotalLengthM),
                rows.Sum(row => row.TotalWeightKg));

            xml.Append("</sheetData>");
            xml.Append("<autoFilter ref=\"A2:I2\"/>");
            xml.AppendFormat(
                CultureInfo.InvariantCulture,
                "<mergeCells count=\"2\"><mergeCell ref=\"A1:I1\"/><mergeCell ref=\"A{0}:G{0}\"/></mergeCells>",
                rowIndex);
            if (hasDrawing)
                xml.Append("<drawing r:id=\"rId1\"/>");
            xml.Append("</worksheet>");
            return xml.ToString();
        }

        private static void AddRow(
            StringBuilder xml,
            int rowIndex,
            int styleIndex,
            params object[] values)
        {
            xml.AppendFormat(
                CultureInfo.InvariantCulture,
                rowIndex >= 3 && styleIndex == 3
                    ? "<row r=\"{0}\" ht=\"68\" customHeight=\"1\">"
                    : "<row r=\"{0}\">",
                rowIndex);
            for (int columnIndex = 0; columnIndex < values.Length; columnIndex++)
            {
                string cellReference =
                    GetColumnName(columnIndex + 1) + rowIndex;
                object value = values[columnIndex];
                int cellStyleIndex =
                    rowIndex >= 3 &&
                    styleIndex == 3 &&
                    columnIndex == 3
                        ? 5
                        : styleIndex;
                if (value is int || value is long ||
                    value is double || value is float || value is decimal)
                {
                    xml.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "<c r=\"{0}\" s=\"{1}\"><v>{2}</v></c>",
                        cellReference,
                        cellStyleIndex,
                        Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    string text = SecurityElement.Escape(
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                    xml.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "<c r=\"{0}\" s=\"{1}\" t=\"inlineStr\"><is><t>{2}</t></is></c>",
                        cellReference,
                        cellStyleIndex,
                        text);
                }
            }
            xml.Append("</row>");
        }

        private static string GetColumnName(int index)
        {
            StringBuilder result = new StringBuilder();
            while (index > 0)
            {
                index--;
                result.Insert(0, (char)('A' + index % 26));
                index /= 26;
            }
            return result.ToString();
        }

        private static void AddEntry(
            ZipArchive archive,
            string path,
            string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                path,
                CompressionLevel.Optimal);
            using (StreamWriter writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static void AddBinaryEntry(
            ZipArchive archive,
            string path,
            byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                path,
                CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            {
                stream.Write(content, 0, content.Length);
            }
        }

        private static string BuildContentTypesXml(bool hasDrawing)
        {
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                (hasDrawing
                    ? "<Default Extension=\"png\" ContentType=\"image/png\"/>"
                    : "") +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                (hasDrawing
                    ? "<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>"
                    : "") +
                "</Types>";
        }

        private static string BuildDrawingXml(
            IList<FootingRebarQuantityRow> rows)
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

            int imageIndex = 1;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                FootingRebarQuantityRow row = rows[rowIndex];
                if (row.ShapeImagePng == null || row.ShapeImagePng.Length == 0)
                    continue;

                int excelRowZeroBased = rowIndex + 2;
                xml.Append("<xdr:oneCellAnchor>");
                xml.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "<xdr:from><xdr:col>2</xdr:col><xdr:colOff>60000</xdr:colOff><xdr:row>{0}</xdr:row><xdr:rowOff>50000</xdr:rowOff></xdr:from>",
                    excelRowZeroBased);
                // Column C is 34 characters wide (~2.27M EMU). Keep the image
                // inside that cell so its white background never covers the
                // diameter value in column D.
                xml.Append("<xdr:ext cx=\"2150000\" cy=\"750000\"/>");
                xml.Append("<xdr:pic>");
                xml.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "<xdr:nvPicPr><xdr:cNvPr id=\"{0}\" name=\"Hình thép {0}\"/><xdr:cNvPicPr/></xdr:nvPicPr>",
                    imageIndex);
                xml.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "<xdr:blipFill><a:blip r:embed=\"rId{0}\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>",
                    imageIndex);
                xml.Append("<xdr:spPr><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></xdr:spPr>");
                xml.Append("</xdr:pic><xdr:clientData/></xdr:oneCellAnchor>");
                imageIndex++;
            }

            xml.Append("</xdr:wsDr>");
            return xml.ToString();
        }

        private static string BuildDrawingRelationshipsXml(int imageCount)
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= imageCount; i++)
            {
                xml.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "<Relationship Id=\"rId{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image{0}.png\"/>",
                    i);
            }
            xml.Append("</Relationships>");
            return xml.ToString();
        }

        private class RawRebarQuantity
        {
            public string GroupCode { get; set; }
            public string ScheduleMark { get; set; }
            public string ShapeName { get; set; }
            public double DiameterMm { get; set; }
            public double UnitLengthMm { get; set; }
            public int Quantity { get; set; }
            public byte[] ShapeImagePng { get; set; }
        }

        private const string RootRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Thép bệ mố\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        private const string WorkbookRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        private const string SheetRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>" +
            "</Relationships>";

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"14\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"4\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF203864\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\"><border/><border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"2\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}
