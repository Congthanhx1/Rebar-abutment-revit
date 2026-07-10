using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Nice3point.Revit.Extensions;

namespace Vetheprevit.MoCau
{
    public static class RebarNamingService
    {
        public static void ApplyNames(
            Document doc,
            IEnumerable<ElementId> rebarIds,
            IDictionary<string, string> namesByGroup)
        {
            if (doc == null || rebarIds == null || namesByGroup == null) return;

            using (Transaction transaction = new Transaction(doc, "Đặt tên thép mố cầu"))
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
                    if (!namesByGroup.TryGetValue(groupCode, out string rebarName))
                        continue;
                    if (string.IsNullOrWhiteSpace(rebarName)) continue;

                    // Rebar groups intentionally share the same schedule mark.
                    // ALL_MODEL_MARK is an element-identity field and causes
                    // duplicate Mark warnings when several rebar sets use it.
                    Parameter scheduleMark = rebar.FindParameter(
                        BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK);
                    if (scheduleMark != null && !scheduleMark.IsReadOnly)
                        scheduleMark.Set(rebarName.Trim());
                }

                transaction.Commit();
            }
        }
    }
}
