using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Extensions;
using Vetheprevit.TienIch;

namespace Vetheprevit.MoCau
{
    [Transaction(TransactionMode.Manual)]
    public class CmdTiltRebarEnd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            App.RegisterAssemblyResolver();
            Revit.Async.RevitTask.Initialize(commandData.Application);

            try
            {
                TiltRebarWindow currentUI = new TiltRebarWindow();
                currentUI.OnPickRebarA = async ui => await PickRebarAsync(ui, true);
                currentUI.OnPickRebarB = async ui => await PickRebarAsync(ui, false);
                currentUI.OnRunTilt = async ui => await RunTiltAsync(ui);

                try
                {
                    System.Windows.Interop.WindowInteropHelper helper =
                        new System.Windows.Interop.WindowInteropHelper(currentUI);
                    helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                }
                catch (Exception ex)
                {
                    Vetheprevit.TienIch.Logger.LogError("Lỗi khi set Owner handle", ex);
                }

                currentUI.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", ex.Message);
                return Result.Failed;
            }
        }

        private static async Task PickRebarAsync(TiltRebarWindow ui, bool isRebarA)
        {
            ui.Hide();
            try
            {
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    UIDocument uidoc = app.ActiveUIDocument;
                    Document doc = uidoc.Document;
                    string prompt = isRebarA
                        ? "Chọn thanh thép cần uốn nghiêng một đầu (Thanh A)"
                        : "Chọn thanh thép cần né (Thanh B)";

                    Reference pickedRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RebarSelectionFilter(),
                        prompt);

                    Rebar rebar = pickedRef.ElementId.ToElement<Rebar>(doc);
                    if (rebar == null)
                        throw new Exception("Đối tượng được chọn không phải Rebar.");

                    string label = GetRebarLabel(rebar);
                    if (isRebarA)
                        ui.SetRebarA(rebar.Id, label);
                    else
                        ui.SetRebarB(rebar.Id, label);
                });
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ui.SetStatus("Đã hủy chọn thanh thép.");
            }
            catch (Exception ex)
            {
                ui.SetStatus("Lỗi chọn thép: " + ex.Message);
            }
            finally
            {
                ui.Show();
                ui.Activate();
            }
        }

        private static async Task RunTiltAsync(TiltRebarWindow ui)
        {
            try
            {
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    Document doc = app.ActiveUIDocument.Document;
                    TiltSelectedRebar(doc, ui.RebarAId, ui.RebarBId, ui.IsAAboveB);
                    TaskDialog.Show("Thành công", "Đã né thép giao nhau.");
                });

                ui.SetStatus("Đã né thép giao nhau.");
            }
            catch (Exception ex)
            {
                ui.SetStatus("Không thể né thép: " + ex.Message);
            }
        }

        private static string GetRebarLabel(Rebar rebar)
        {
            string name = string.IsNullOrWhiteSpace(rebar.Name) ? "Rebar" : rebar.Name;
            return $"{name} - Id {rebar.Id.Value}";
        }

        private static void TiltSelectedRebar(
            Document doc,
            ElementId rebarAId,
            ElementId rebarBId,
            bool isAAboveB)
        {
            if (rebarAId == null || rebarBId == null)
                throw new Exception("Chưa chọn đủ thanh A và thanh B.");

            Rebar rebarA = rebarAId.ToElement<Rebar>(doc);
            Rebar rebarB = rebarBId.ToElement<Rebar>(doc);
            if (rebarA == null || rebarB == null)
                throw new Exception("Không tìm thấy thanh thép đã chọn. Vui lòng chọn lại.");

            IList<Curve> curvesA = rebarA.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            IList<Curve> curvesB = rebarB.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);

            if (curvesA.Count == 0 || curvesB.Count == 0)
                throw new Exception("Không đọc được đường tâm của một trong hai thanh thép.");

            XYZ ptA = null;
            XYZ ptB = null;
            double minDistance = double.MaxValue;

            foreach (Curve cA in curvesA)
            {
                foreach (Curve cB in curvesB)
                {
                    for (double tA = 0; tA <= 1; tA += 0.05)
                    {
                        XYZ pA = cA.Evaluate(tA, true);
                        for (double tB = 0; tB <= 1; tB += 0.05)
                        {
                            XYZ pB = cB.Evaluate(tB, true);
                            double dist = pA.DistanceTo(pB);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                ptA = pA;
                                ptB = pB;
                            }
                        }
                    }
                }
            }

            if (ptA == null || ptB == null)
                throw new Exception("Không tìm thấy vị trí giao nhau giữa 2 thanh thép.");

            double diaA = rebarA.GetTypeId().ToElement<RebarBarType>(doc) is RebarBarType rtA ? rtA.BarModelDiameter : 0.05;
            double diaB = rebarB.GetTypeId().ToElement<RebarBarType>(doc) is RebarBarType rtB ? rtB.BarModelDiameter : 0.05;
            double offset = (diaA + diaB) / 2.0;

            if (!isAAboveB)
                offset = -offset;

            double currentZDiff = ptB.Z - ptA.Z;
            double totalMoveZ = currentZDiff + offset;

            XYZ end0 = curvesA[0].GetEndPoint(0);
            XYZ end1 = curvesA[curvesA.Count - 1].GetEndPoint(1);

            double d0 = end0.DistanceTo(ptA);
            double d1 = end1.DistanceTo(ptA);

            XYZ endMove = d0 < d1 ? end0 : end1;
            XYZ pivot = d0 < d1 ? end1 : end0;

            XYZ vec = endMove - pivot;
            double length = vec.GetLength();

            if (length < 0.01)
                throw new Exception("Thanh thép quá ngắn.");

            double sinTheta = totalMoveZ / length;
            if (sinTheta > 1) sinTheta = 1;
            if (sinTheta < -1) sinTheta = -1;
            double theta = Math.Asin(sinTheta);

            XYZ axisDir = vec.CrossProduct(XYZ.BasisZ);
            if (axisDir.IsAlmostEqualTo(XYZ.Zero))
                axisDir = XYZ.BasisX;
            axisDir = axisDir.Normalize();

            Transform rotation = Transform.CreateRotationAtPoint(axisDir, theta, pivot);

            List<Curve> newCurves = new List<Curve>();
            foreach (Curve curve in curvesA)
            {
                newCurves.Add(curve.CreateTransformed(rotation));
            }

            using (Transaction t = new Transaction(doc, "Né thép"))
            {
                t.Start();
                try
                {
                    ElementId hostId = rebarA.GetHostId();
                    RebarBarType barType = rebarA.GetTypeId().ToElement<RebarBarType>(doc);
                    if (hostId == null || barType == null)
                        throw new Exception("Không đọc được host hoặc kiểu đường kính của thanh A.");

                    XYZ norm = new XYZ(0, 0, 1);
                    try
                    {
                        norm = rebarA.GetShapeDrivenAccessor().Normal;
                    }
                    catch (Exception ex)
                    {
                        Vetheprevit.TienIch.Logger.LogError("Lỗi lấy Normal thanh thép cũ", ex);
                    }

                    XYZ newNorm = rotation.OfVector(norm).Normalize();
                    Element host = hostId.ToElement(doc);
                    if (host == null)
                        throw new Exception("Không tìm thấy host của thanh A.");

                    Rebar newRebar = Rebar.CreateFromCurves(
                        doc,
                        RebarStyle.Standard,
                        barType,
                        host,
                        newNorm,
                        newCurves,
                        new BarTerminationsData(doc),
                        true,
                        true);

                    CopyRebarData(rebarA, newRebar);
                    doc.Delete(rebarA.Id);
                    t.Commit();
                }
                catch
                {
                    if (t.GetStatus() == TransactionStatus.Started)
                    {
                        t.RollBack();
                    }

                    throw;
                }
            }
        }

        private static void CopyRebarData(Rebar source, Rebar target)
        {
            if (target == null)
                throw new Exception("Không tạo được thanh thép mới.");

            target.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                ?.Set(source.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "");
            target.FindParameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK)
                ?.Set(source.FindParameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK)?.AsString() ?? "");
            target.FindParameter(BuiltInParameter.ALL_MODEL_MARK)
                ?.Set(source.FindParameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "");

            RebarMarkerData marker = RebarMarkerService.GetMarker(source);
            if (marker != null)
            {
                RebarMarkerService.SetMarker(target, new ElementId(marker.HostId), marker.GroupCode);
            }

            try
            {
                RebarShapeDrivenAccessor accNew = target.GetShapeDrivenAccessor();
                RebarLayoutRule rule = source.LayoutRule;

                if (rule == RebarLayoutRule.MaximumSpacing)
                    accNew.SetLayoutAsMaximumSpacing(source.MaxSpacing, source.Quantity, true, true, true);
                else if (rule == RebarLayoutRule.FixedNumber)
                    accNew.SetLayoutAsFixedNumber(source.Quantity, source.MaxSpacing, true, true, true);
                else if (rule == RebarLayoutRule.NumberWithSpacing)
                    accNew.SetLayoutAsNumberWithSpacing(source.Quantity, source.MaxSpacing, true, true, true);
            }
            catch (Exception ex)
            {
                Vetheprevit.TienIch.Logger.LogError("Lỗi set layout thanh thép mới", ex);
            }
        }
    }
}
