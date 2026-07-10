using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace Vetheprevit.MoCau
{
    public static class SpacingSequence
    {
        public static IList<double> GetOffsetsInternal(
            string sequence,
            double availableLength,
            string directionName)
        {
            List<double> offsets = new List<double> { 0 };
            string[] tokens = Regex.Split(sequence?.Trim() ?? "", @"[\s,;]+")
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();

            if (tokens.Length == 0)
                throw new Exception($"Chưa nhập chuỗi kích thước phương {directionName}.");

            double cumulative = 0;
            foreach (string token in tokens)
            {
                if (!double.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double spacingMm) &&
                    !double.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out spacingMm))
                {
                    throw new Exception(
                        $"Kích thước \"{token}\" của phương {directionName} không hợp lệ.");
                }

                if (spacingMm <= 0)
                    throw new Exception(
                        $"Khoảng cách phương {directionName} phải lớn hơn 0.");

                cumulative += UnitUtils.ConvertToInternalUnits(
                    spacingMm,
                    UnitTypeId.Millimeters);
                if (cumulative > availableLength + 0.001)
                {
                    throw new Exception(
                        $"Tổng chuỗi kích thước phương {directionName} vượt quá phạm vi bố trí.");
                }

                offsets.Add(cumulative);
            }

            return offsets;
        }
    }
}
