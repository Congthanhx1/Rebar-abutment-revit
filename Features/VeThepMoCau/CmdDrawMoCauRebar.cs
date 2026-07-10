using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;
using Nice3point.Revit.Extensions;

namespace Vetheprevit.MoCau
{


    [Transaction(TransactionMode.Manual)]
    public class CmdDrawMoCauRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            App.RegisterAssemblyResolver();
            Revit.Async.RevitTask.Initialize(commandData.Application);

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            MoCauRebarUI currentUI = new MoCauRebarUI(doc);
            
                // Nạp thông số từ Extensible Storage (nếu có)
            var savedSettings = RebarSettingsStorage.LoadSettings(doc);
            currentUI.ApplySettingsFromDictionary(savedSettings);

            currentUI.OnDrawRebar = async (ui) =>
            {
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    UIDocument appUidoc = app.ActiveUIDocument;
                    Document appDoc = appUidoc.Document;
                    if (ui.SelectedAbutment != null)
                    {
                        using (TransactionGroup tGroup = new TransactionGroup(appDoc, "Vẽ Thép Mố Hoàn Chỉnh"))
                        {
                            tGroup.Start();
                            try
                            {
                                Vetheprevit.TienIch.Logger.ClearWarnings();

                                // 1. Lưu thông số
                                RebarSettingsStorage.SaveSettings(appDoc, ui.GetSettingsToDictionary());

                                // 2. Tạo thép
                                FootingRebarCreator creator = new FootingRebarCreator();
                                List<ElementId> createdRebars = new List<ElementId>();

                                if (ui.SelectedTabIndex == 0)
                                {
                                    AbutmentRebarConfig abutmentConfig = ui.GetAbutmentConfig();
                                    createdRebars = creator.CreateAbutmentRebars(appDoc, ui.SelectedAbutment, abutmentConfig);
                                }
                                else if (ui.SelectedTabIndex == 1)
                                {
                                    StemRebarConfig stemConfig = ui.GetStemConfig();
                                    createdRebars = creator.CreateStemRebars(appDoc, ui.SelectedAbutment, stemConfig);
                                }

                                if (createdRebars.Count > 0)
                                {
                                    // 3. Gán shape
                                    RebarShapeService.ApplyShapes(
                                        appDoc,
                                        createdRebars,
                                        ui.GetRebarShapesByGroup());
                                    
                                    // 4. Gán neo L
                                    RebarShapeService.ApplyMeshAnchors(
                                        appDoc,
                                        createdRebars,
                                        new Dictionary<string, double>
                                        {
                                            ["VT_BotLong"] = ui.BotMeshAnchorLongMm,
                                            ["VT_BotTrans"] = ui.BotMeshAnchorTransMm,
                                            ["VT_TopLong"] = ui.TopMeshAnchorLongMm,
                                            ["VT_TopTrans"] = ui.TopMeshAnchorTransMm
                                        });

                                    // 5. Đặt tên thép
                                    RebarNamingService.ApplyNames(
                                        appDoc,
                                        createdRebars,
                                        ui.GetRebarNamesByGroup());

                                    // 6. Nhóm thép
                                    List<ElementId> antiBurstIds = new List<ElementId>();
                                    List<ElementId> horizSideIds = new List<ElementId>();
                                    List<ElementId> vertSideIds = new List<ElementId>();

                                    foreach (ElementId rbId in createdRebars)
                                    {
                                        Rebar rb = rbId.ToElement<Rebar>(appDoc);
                                        if (rb != null)
                                        {
                                            Parameter p = rb.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                            if (p != null)
                                            {
                                                string comment = p.AsString();
                                                if (comment == "VT_AntiBurstX" || comment == "VT_AntiBurstY")
                                                    antiBurstIds.Add(rbId);
                                                else if (comment == "VT_HorizSideX" || comment == "VT_HorizSideY")
                                                    horizSideIds.Add(rbId);
                                                else if (comment == "VT_VertSideX" || comment == "VT_VertSideY")
                                                    vertSideIds.Add(rbId);
                                            }
                                        }
                                    }

                                    using (Transaction tGrp = new Transaction(appDoc, "Group Rebars"))
                                    {
                                        try
                                        {
                                            tGrp.Start();
                                            if (antiBurstIds.Count > 1) appDoc.Create.NewGroup(antiBurstIds);
                                            if (horizSideIds.Count > 1) appDoc.Create.NewGroup(horizSideIds);
                                            if (vertSideIds.Count > 1) appDoc.Create.NewGroup(vertSideIds);
                                            tGrp.Commit();
                                        }
                                        catch (Exception ex)
                                        {
                                            if (tGrp.GetStatus() == TransactionStatus.Started)
                                            {
                                                tGrp.RollBack();
                                            }

                                            Vetheprevit.TienIch.Logger.LogError("Loi khi nhom thep mo cau", ex);
                                            Vetheprevit.TienIch.Logger.AddWarning(
                                                "Da tao thep thanh cong, nhung khong the nhom mot so thanh thep. Vui long Group thu cong neu can.");
                                        }
                                    }
                                }

                                // 7. Nếu mọi thứ thành công, gộp các Transaction lại thành 1
                                tGroup.Assimilate();

                                var warnings = Vetheprevit.TienIch.Logger.GetWarnings();
                                if (warnings.Count > 0)
                                {
                                    TaskDialog td = new TaskDialog("Cảnh báo tạo thép");
                                    td.MainInstruction = "Quá trình tạo thép có một số cảnh báo:";
                                    td.MainContent = string.Join("\n\n", warnings);
                                    td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                                    td.Show();
                                }

                                // Tô sáng các thanh thép vừa tạo và Zoom tới
                                if (createdRebars.Count > 0)
                                {
                                    appUidoc.Selection.SetElementIds(createdRebars);
                                    appUidoc.ShowElements(createdRebars);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Nếu có bất kỳ lỗi nào, hoàn tác TOÀN BỘ (xóa thép, hủy gán shape, v.v.)
                                if (tGroup.GetStatus() == TransactionStatus.Started)
                                {
                                    tGroup.RollBack();
                                }
                                TaskDialog.Show("Lỗi", "Không thể vẽ thép: " + ex.Message);
                            }
                        }
                    }
                });
            };

            currentUI.OnPickAbutment = async (ui) =>
            {
                ui.Hide();
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    UIDocument appUidoc = app.ActiveUIDocument;
                    try
                    {
                        Reference r = appUidoc.Selection.PickObject(
                            ObjectType.Element,
                            new AbutmentSelectionFilter(),
                        "Vui lòng chọn Family mố cầu");

                        FamilyInstance selectedAbutment = r.ElementId.ToElement<FamilyInstance>(appUidoc.Document);

                        // Validate the complete Family before assigning it to the UI.
                        new AbutmentGeometryReader().Read(
                            appUidoc.Document,
                            selectedAbutment);

                        ui.SelectedAbutment = selectedAbutment;
                        ui.UpdateStatus();
                        ui.Show(); // Show dialog back after picking
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // User pressed ESC
                        ui.Show();
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show(
                            "Family mố không hợp lệ",
                            "Không thể nhận dạng bệ mố và thân mố trong Family đã chọn.\n\n" +
                            ex.Message);
                        ui.Show();
                    }
                });
            };

            currentUI.OnPickHorizSideTopRebar = async (ui) =>
            {
                ui.Hide();
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    UIDocument appUidoc = app.ActiveUIDocument;
                    Document doc = appUidoc.Document;
                    try
                    {
                        Reference r = appUidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "Vui lòng chọn một thanh thép (X hoặc Y) để nằm ngoài");
                        if (r.ElementId.ToElement(doc) is Rebar rb)
                        {
                            Transform t = ui.SelectedAbutment.GetTransform();
                            XYZ xDir = t.BasisX.Normalize();
                            XYZ yDir = t.BasisY.Normalize();
                            
                            IList<Curve> curves = rb.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
                            double maxLength = -1;
                            Curve longestCurve = null;
                            foreach (Curve c in curves)
                            {
                                if (c.Length > maxLength)
                                {
                                    maxLength = c.Length;
                                    longestCurve = c;
                                }
                            }
                            
                            if (longestCurve != null)
                            {
                                XYZ curveDir = (longestCurve.GetEndPoint(1) - longestCurve.GetEndPoint(0)).Normalize();
                                if (Math.Abs(curveDir.DotProduct(xDir)) > Math.Abs(curveDir.DotProduct(yDir)))
                                {
                                    ui.cboHorizSideTopPos.SelectedIndex = 0;
                                    TaskDialog.Show("Thành công", "Đã nhận diện thanh thép phương dọc (X). Phương X sẽ nằm ngoài (trên).");
                                }
                                else
                                {
                                    ui.cboHorizSideTopPos.SelectedIndex = 1;
                                    TaskDialog.Show("Thành công", "Đã nhận diện thanh thép phương ngang (Y). Phương Y sẽ nằm ngoài (trên).");
                                }
                            }
                        }
                        ui.Show();
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        ui.Show();
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Lỗi", "Lỗi khi nhận diện thép: " + ex.Message);
                        ui.Show();
                    }
                });
            };

            currentUI.OnSaveSettings = async (ui) =>
            {
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    try
                    {
                        RebarSettingsStorage.SaveSettings(app.ActiveUIDocument.Document, ui.GetSettingsToDictionary());
                        TaskDialog.Show("Thành công", "Đã lưu thông số thành công vào mô hình!");
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Lỗi", "Không thể lưu thông số: " + ex.Message);
                    }
                });
            };

            currentUI.OnExportFootingExcel = async (ui) =>
            {
                await Revit.Async.RevitTask.RunAsync(app =>
                {
                    try
                    {
                        Document appDoc = app.ActiveUIDocument.Document;
                        IList<FootingRebarQuantityRow> rows =
                            FootingRebarQuantityExporter.Collect(
                                appDoc,
                                ui.SelectedAbutment,
                                ui.ExportFootingGroupCodes,
                                ui.GetRebarNamesByGroup());
                        FootingRebarQuantityExporter.ExportXlsx(
                            ui.ExportFootingExcelPath,
                            rows);
                        TaskDialog.Show(
                            "Xuất Excel thành công",
                            $"Đã xuất {rows.Count} dòng thống kê thép bệ mố:\n" +
                            ui.ExportFootingExcelPath);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show(
                            "Không thể xuất Excel",
                            ex.Message);
                    }
                });
            };

            // Set Window owner to Revit MainWindow
            try
            {
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(currentUI);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
            catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("L?i khi set Owner handle", ex); }

            currentUI.UpdateStatus();
            currentUI.Show();

            return Result.Succeeded;
        }
    }

    public class AbutmentSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (!(elem is FamilyInstance familyInstance)) return false;

            try
            {
                return RebarHostData.GetRebarHostData(familyInstance) != null;
            }
            catch
            {
                return false;
            }
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    public class RebarSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Autodesk.Revit.DB.Structure.Rebar;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}

