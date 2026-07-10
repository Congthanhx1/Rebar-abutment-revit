using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Nice3point.Revit.Extensions;

namespace Vetheprevit.MoCau
{
    public static class RebarShapeService
    {
        public static void ApplyMeshAnchors(
            Document doc,
            IEnumerable<ElementId> rebarIds,
            IDictionary<string, double> anchorLengthsMmByGroup)
        {
            if (doc == null || rebarIds == null || anchorLengthsMmByGroup == null)
                return;

            using (Transaction transaction = new Transaction(doc, "Ap dung chieu ngam luoi thep"))
            {
                try
                {
                    transaction.Start();

                    foreach (ElementId id in rebarIds)
                    {
                        Rebar rebar = id.ToElement<Rebar>(doc);
                        if (rebar == null) continue;

                        string groupCode = rebar
                            .FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                            ?.AsString();
                        if (string.IsNullOrWhiteSpace(groupCode)) continue;
                        if (!anchorLengthsMmByGroup.TryGetValue(groupCode, out double anchorLengthMm))
                            continue;
                        if (anchorLengthMm <= 0) continue;

                        Parameter anchorParameter = rebar.FindParameter("G");
                        if (anchorParameter == null ||
                            anchorParameter.IsReadOnly ||
                            anchorParameter.StorageType != StorageType.Double)
                        {
                            continue;
                        }

                        anchorParameter.Set(UnitUtils.ConvertToInternalUnits(
                            anchorLengthMm,
                            UnitTypeId.Millimeters));
                    }

                    transaction.Commit();
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                    throw;
                }
            }
        }

        public static void ApplyShapes(
            Document doc,
            IEnumerable<ElementId> rebarIds,
            IDictionary<string, string> shapeNamesByGroup)
        {
            if (doc == null || rebarIds == null || shapeNamesByGroup == null)
                return;

            Dictionary<string, RebarShape> shapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .GroupBy(shape => shape.Name)
                .ToDictionary(group => group.Key, group => group.First());

            using (Transaction transaction = new Transaction(doc, "Ap dung hinh dang thep mo cau"))
            {
                try
                {
                    transaction.Start();

                    foreach (ElementId id in rebarIds)
                    {
                        Rebar rebar = id.ToElement<Rebar>(doc);
                        if (rebar == null) continue;

                        string groupCode = rebar
                            .FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                            ?.AsString();
                        if (string.IsNullOrWhiteSpace(groupCode)) continue;

                        if (groupCode == "VT_BotLong" ||
                            groupCode == "VT_BotTrans" ||
                            groupCode == "VT_TopLong" ||
                            groupCode == "VT_TopTrans")
                        {
                            continue;
                        }

                        if (!shapeNamesByGroup.TryGetValue(groupCode, out string shapeName))
                            continue;
                        if (string.IsNullOrWhiteSpace(shapeName)) continue;
                        if (!shapes.TryGetValue(shapeName.Trim(), out RebarShape shape))
                            continue;

                        try
                        {
                            rebar.GetShapeDrivenAccessor().SetRebarShapeId(shape.Id);
                        }
                        catch
                        {
                            // Keep the curve-generated shape when the selected shape is incompatible.
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                    throw;
                }
            }
        }
    }
}
