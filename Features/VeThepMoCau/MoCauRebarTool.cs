using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Nice3point.Revit.Extensions;
using Vetheprevit.TienIch;

namespace Vetheprevit.MoCau
{
    public partial class FootingRebarCreator
    {
        private readonly AbutmentGeometryReader _geometryReader = new AbutmentGeometryReader();

        public AbutmentGeoInfo GetGeoInfo(Document doc, FamilyInstance abutment)
        {
            return _geometryReader.Read(doc, abutment);
        }

        private static double GetBarDiameter(RebarBarType barType)
        {
            return barType?.BarModelDiameter ?? 0;
        }

        private static void EnsureBarType(RebarBarType barType, string groupName)
        {
            if (barType == null)
            {
                throw new Exception($"Chưa chọn đường kính thép cho {groupName}.");
            }
        }

        private void ApplyLayout(Rebar rb, int layoutMode, double spMm, double qty, double arrayLength, double barDiameter, string tag = "")
        {
            const int MaxRebarQuantity = 1000;

            if (rb == null)
            {
                throw new Exception("Khong the tao thep: Rebar rong.");
            }

            if (arrayLength <= Math.Max(barDiameter, 0.001))
            {
                throw new Exception("Chieu dai rai thep khong hop le. Vui long kiem tra kich thuoc be mo va lop bao ve.");
            }

            if (!string.IsNullOrEmpty(tag))
            {
                Parameter p = rb.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (p != null && !p.IsReadOnly) p.Set(tag);
                RebarMarkerService.SetMarker(rb, rb.GetHostId(), tag);
            }
            if (layoutMode == 0)
            {
                if (spMm <= 0)
                {
                    throw new Exception("Khoang cach rai thep phai lon hon 0 mm.");
                }

                double spInternal = UnitUtils.ConvertToInternalUnits(spMm, UnitTypeId.Millimeters);
                int estimatedQuantity = Math.Max(2, (int)Math.Ceiling(arrayLength / spInternal) + 1);
                if (estimatedQuantity > MaxRebarQuantity)
                {
                    throw new Exception($"So thanh thep uoc tinh ({estimatedQuantity}) qua lon. Vui long tang khoang cach rai thep.");
                }

                rb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spInternal, arrayLength, true, true, true);
            }
            else if (layoutMode == 1)
            {
                int quantity = (int)Math.Round(qty);
                if (quantity <= 1)
                {
                    throw new Exception("Số lượng thép tối thiểu phải từ 2 thanh trở lên!");
                }
                
                if (quantity > MaxRebarQuantity)
                {
                    throw new Exception($"So luong thep ({quantity}) qua lon. Vui long giam so luong thep.");
                }

                double calculatedSpacing = arrayLength / (quantity - 1);
                if (calculatedSpacing < barDiameter)
                {
                    throw new Exception($"Số lượng thép ({quantity}) quá dày, khoảng cách tính toán ({(calculatedSpacing * 304.8).ToString("0.0")} mm) nhỏ hơn đường kính thanh thép ({(barDiameter * 304.8).ToString("0.0")} mm). Vui lòng giảm số lượng thép hoặc tăng kích thước bệ mố!");
                }

                rb.GetShapeDrivenAccessor().SetLayoutAsFixedNumber(quantity, arrayLength, true, true, true);
            }
            else if (layoutMode == 2)
            {
                int quantity = (int)Math.Round(qty);
                if (quantity <= 1)
                {
                    throw new Exception("Số lượng thép tối thiểu phải từ 2 thanh trở lên!");
                }
                if (quantity > MaxRebarQuantity)
                {
                    throw new Exception($"So luong thep ({quantity}) qua lon. Vui long giam so luong thep.");
                }
                if (spMm <= 0)
                {
                    throw new Exception("Khoang cach rai thep phai lon hon 0 mm.");
                }

                double spInternal = UnitUtils.ConvertToInternalUnits(spMm, UnitTypeId.Millimeters);
                rb.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(quantity, spInternal, true, true, true);
            }
        }

        private List<double> GetLayoutOffsets(
            int layoutMode,
            double spacingMm,
            double quantityValue,
            double arrayLength)
        {
            List<double> offsets = new List<double>();
            if (arrayLength < 0) return offsets;
            if (arrayLength < 0.001)
            {
                offsets.Add(0);
                return offsets;
            }

            double spacing = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);
            int quantity;

            if (layoutMode == 0)
            {
                if (spacing <= 0) throw new Exception("Khoảng cách thép chống nở phải lớn hơn 0.");
                quantity = Math.Max(2, (int)Math.Ceiling(arrayLength / spacing) + 1);
                spacing = arrayLength / (quantity - 1);
            }
            else if (layoutMode == 1)
            {
                quantity = Math.Max(2, (int)Math.Round(quantityValue));
                spacing = arrayLength / (quantity - 1);
            }
            else
            {
                quantity = Math.Max(1, (int)Math.Round(quantityValue));
                if (spacing <= 0) throw new Exception("Khoảng cách thép chống nở phải lớn hơn 0.");
            }

            for (int i = 0; i < quantity; i++)
            {
                double offset = i * spacing;
                if (offset > arrayLength + 0.001) break;
                offsets.Add(offset);
            }

            return offsets;
        }

    }
}
