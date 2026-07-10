using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Vetheprevit.TienIch;
using Nice3point.Revit.Extensions;

namespace Vetheprevit.MoCau
{
    public partial class FootingRebarCreator
    {
        public List<ElementId> CreateStemRebars(Document doc, FamilyInstance abutment, StemRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            AbutmentGeoInfo geo = GetGeoInfo(doc, abutment);

            using (Transaction t = new Transaction(doc, "Tạo Thép Thân Mố"))
            {
                try
                {
                    t.Start();

                    DeleteOldStemRebars(doc, abutment, config);

                    if (config.DrawStemVertFront || config.DrawStemVertBack)
                        createdRebars.AddRange(CreateStemVertRebars(doc, abutment, geo, config));

                    if (config.DrawStemHoriz)
                        createdRebars.AddRange(CreateStemHorizRebars(doc, abutment, geo, config));

                    if (config.DrawStemTie)
                        createdRebars.AddRange(CreateStemTieRebars(doc, abutment, geo, config));

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

            return createdRebars;
        }

        private void DeleteOldStemRebars(Document doc, FamilyInstance abutment, StemRebarConfig config)
        {
            Autodesk.Revit.DB.Structure.RebarHostData hostData = Autodesk.Revit.DB.Structure.RebarHostData.GetRebarHostData(abutment);
            if (hostData != null)
            {
                HashSet<ElementId> groupTypesToCheck = new HashSet<ElementId>();
                List<ElementId> rebarsToDelete = new List<ElementId>();
                IList<Autodesk.Revit.DB.Structure.Rebar> existingRebars = hostData.GetRebarsInHost();
                foreach (Autodesk.Revit.DB.Structure.Rebar rb in existingRebars)
                {
                    RebarMarkerData marker = RebarMarkerService.GetMarker(rb);
                    string comment = marker != null && marker.Creator == "RebarAbutmentTool" && marker.HostId == abutment.Id.Value ? marker.GroupCode : "";
                    bool shouldDelete = false;
                    if (config.DrawStemVertFront && comment == "VT_StemVertFront") shouldDelete = true;
                    else if (config.DrawStemVertBack && comment == "VT_StemVertBack") shouldDelete = true;
                    else if (config.DrawStemHoriz && comment == "VT_StemHoriz") shouldDelete = true;
                    else if (config.DrawStemTie && comment == "VT_StemTie") shouldDelete = true;

                    if (shouldDelete)
                    {
                        try
                        {
                            ElementId groupId = rb.GroupId;
                            if (groupId != ElementId.InvalidElementId)
                            {
                                Group grp = groupId.ToElement<Group>(doc);
                                if (grp != null) 
                                {
                                    groupTypesToCheck.Add(grp.GroupType.Id);
                                    grp.UngroupMembers();
                                }
                            }
                            rebarsToDelete.Add(rb.Id);
                        }
                        catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi nội bộ khi xoá thép thân mố", ex); }
                    }
                }

                if (rebarsToDelete.Count > 0)
                {
                    try { doc.Delete(rebarsToDelete); }
                    catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi khi xoá mảng thép thân mố", ex); }
                }

                List<ElementId> groupTypesToDelete = new List<ElementId>();
                foreach (ElementId typeId in groupTypesToCheck)
                {
                    GroupType gType = typeId.ToElement<GroupType>(doc);
                    if (gType != null && gType.Groups.IsEmpty)
                    {
                        groupTypesToDelete.Add(typeId);
                    }
                }
                
                if (groupTypesToDelete.Count > 0)
                {
                    try { doc.Delete(groupTypesToDelete); }
                    catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi khi xoá mảng group thép", ex); }
                }
            }
        }

        private List<ElementId> CreateStemVertRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, StemRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontMm, UnitTypeId.Millimeters);
            double cvVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackMm, UnitTypeId.Millimeters);
            double cvZVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontZMm, UnitTypeId.Millimeters);
            double cvZVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackZMm, UnitTypeId.Millimeters);
            double dVF = GetBarDiameter(config.BarVertFront);
            double dVB = GetBarDiameter(config.BarVertBack);

            if (config.DrawStemVertFront && !config.UseDowelAsStemVert)
            {
                EnsureBarType(config.BarVertFront, "thép đứng thân mố phía trước");
                double offsetLongFront = config.VertFrontLongOffsetMm / 304.8 + dVF / 2.0;
                XYZ pVF_bottom = geo.AbsBasePt1 
                                 + geo.StemThicknessDir * (geo.StemMinT + cvVF) 
                                 + geo.StemLongDir * (geo.StemMinL + offsetLongFront);
                double zBottomFront = geo.MinZ + cvZVF;
                double topZFront = config.AutoVertFrontHeight
                    ? geo.StemMaxZ - cvZVF
                    : zBottomFront + config.VertFrontHeightMm / 304.8;
                XYZ pVF_top = new XYZ(pVF_bottom.X, pVF_bottom.Y, topZFront);
                
                XYZ pVF_bottomStart = new XYZ(pVF_bottom.X, pVF_bottom.Y, zBottomFront);
                Curve curveVF_Vert = Line.CreateBound(pVF_top, pVF_bottomStart);
                IList<Curve> curvesVF = new List<Curve> { curveVF_Vert };
                double hookFront = config.AnchorFrontMm / 304.8 - dVF / 2;
                if (hookFront > 0.01) curvesVF.Add(Line.CreateBound(pVF_bottomStart, pVF_bottomStart - geo.StemThicknessDir * hookFront));

                double arrayLengthVF = Math.Max(0.1, (geo.StemMaxL - geo.StemMinL) - 2 * offsetLongFront);

                Rebar rbVF = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.BarVertFront, abutment, geo.StemLongDir, curvesVF, new BarTerminationsData(doc), true, true);
                ApplyLayout(rbVF, config.LayoutVertFront, config.SpVertFrontMm, config.QtyVertFront, arrayLengthVF, dVF, "VT_StemVertFront");
                createdRebars.Add(rbVF.Id);
            }

            if (config.DrawStemVertBack && !config.UseDowelAsStemVert)
            {
                EnsureBarType(config.BarVertBack, "thép đứng thân mố phía sau");
                double offsetLongBack = config.VertBackLongOffsetMm / 304.8 + dVB / 2.0;
                XYZ pVB_bottom = geo.AbsBasePt1 
                                 + geo.StemThicknessDir * (geo.StemMaxT - cvVB) 
                                 + geo.StemLongDir * (geo.StemMinL + offsetLongBack);
                double zBottomBack = geo.MinZ + cvZVB;
                double topZBack = config.AutoVertBackHeight
                    ? geo.StemMaxZ - cvZVB
                    : zBottomBack + config.VertBackHeightMm / 304.8;
                XYZ pVB_top = new XYZ(pVB_bottom.X, pVB_bottom.Y, topZBack);
                
                XYZ pVB_bottomStart = new XYZ(pVB_bottom.X, pVB_bottom.Y, zBottomBack);
                Curve curveVB_Vert = Line.CreateBound(pVB_top, pVB_bottomStart);
                IList<Curve> curvesVB = new List<Curve> { curveVB_Vert };
                double hookBack = config.AnchorBackMm / 304.8 - dVB / 2;
                if (hookBack > 0.01) curvesVB.Add(Line.CreateBound(pVB_bottomStart, pVB_bottomStart + geo.StemThicknessDir * hookBack));

                double arrayLengthVB = Math.Max(0.1, (geo.StemMaxL - geo.StemMinL) - 2 * offsetLongBack);

                Rebar rbVB = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.BarVertBack, abutment, geo.StemLongDir, curvesVB, new BarTerminationsData(doc), true, true);
                ApplyLayout(rbVB, config.LayoutVertBack, config.SpVertBackMm, config.QtyVertBack, arrayLengthVB, dVB, "VT_StemVertBack");
                createdRebars.Add(rbVB.Id);
            }
            return createdRebars;
        }

        private List<ElementId> CreateStemHorizRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, StemRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontMm, UnitTypeId.Millimeters);
            double cvVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackMm, UnitTypeId.Millimeters);
            double cvZVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontZMm, UnitTypeId.Millimeters);
            double cvZVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackZMm, UnitTypeId.Millimeters);
            double dVF = GetBarDiameter(config.BarVertFront);
            double dVB = GetBarDiameter(config.BarVertBack);
            EnsureBarType(config.BarHoriz, "thép ngang thân mố");
            double dH = GetBarDiameter(config.BarHoriz);

            double cvHX_Front = UnitUtils.ConvertToInternalUnits(config.CvHorizXMm, UnitTypeId.Millimeters);
            double cvHX_Back = cvHX_Front;

            if (config.HorizPosRelToVert == 0)
            {
                cvHX_Front = cvVF + dVF + dH;
                cvHX_Back = cvVB + dVB + dH;
            }
            else if (config.HorizPosRelToVert == 1)
            {
                cvHX_Front = Math.Max(0, cvVF - dH);
                cvHX_Back = Math.Max(0, cvVB - dH);
            }

            double cvHY = UnitUtils.ConvertToInternalUnits(config.CvHorizYMm, UnitTypeId.Millimeters);
            double cvHZ = UnitUtils.ConvertToInternalUnits(config.CvHorizZMm, UnitTypeId.Millimeters);
            XYZ vecZ = new XYZ(0, 0, 1);

            XYZ pHF_start = geo.AbsBasePt1 
                             + geo.StemThicknessDir * (geo.StemMinT + cvHX_Front) 
                             + geo.StemLongDir * (geo.StemMinL + cvHY + dH/2) 
                             + vecZ * (geo.MinUpZ + cvHZ);
            double lengthH = Math.Max(0.01, geo.StemLength - 2 * cvHY - dH);
            XYZ pHF_end = pHF_start + geo.StemLongDir * lengthH;
            Curve curveHF = Line.CreateBound(pHF_start, pHF_end);
            IList<Curve> curvesHF = new List<Curve> { curveHF };

            double zBottomFront = geo.MinZ + cvZVF;
            double zBottomBack = geo.MinZ + cvZVB;

            double vertFrontZ_Top = config.UseDowelAsStemVert
                ? (config.AutoVertFrontHeight ? geo.StemMaxZ - config.VertFrontHeightMm / 304.8 : geo.MinUpZ + config.VertFrontHeightMm / 304.8)
                : (config.AutoVertFrontHeight ? geo.StemMaxZ - cvZVF : zBottomFront + config.VertFrontHeightMm / 304.8);
            double vertBackZ_Top = config.UseDowelAsStemVert
                ? (config.AutoVertBackHeight ? geo.StemMaxZ - config.VertBackHeightMm / 304.8 : geo.MinUpZ + config.VertBackHeightMm / 304.8)
                : (config.AutoVertBackHeight ? geo.StemMaxZ - cvZVB : zBottomBack + config.VertBackHeightMm / 304.8);
            
            double maxHorizZ = Math.Min(vertFrontZ_Top, vertBackZ_Top) - dH / 2.0;
            double maxAllowedArrayLengthH = maxHorizZ - (geo.MinUpZ + cvHZ);
            double arrayLengthH = Math.Max(0.1, maxAllowedArrayLengthH);

            Rebar rbHF = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.BarHoriz, abutment, vecZ, curvesHF, new BarTerminationsData(doc), true, true);
            ApplyLayout(rbHF, config.LayoutHoriz, config.SpHorizMm, config.QtyHoriz, arrayLengthH, dH, "VT_StemHoriz");
            createdRebars.Add(rbHF.Id);

            XYZ pHB_start = geo.AbsBasePt1 
                             + geo.StemThicknessDir * (geo.StemMaxT - cvHX_Back) 
                             + geo.StemLongDir * (geo.StemMinL + cvHY + dH/2) 
                             + vecZ * (geo.MinUpZ + cvHZ);
            XYZ pHB_end = pHB_start + geo.StemLongDir * lengthH;
            Curve curveHB = Line.CreateBound(pHB_start, pHB_end);
            IList<Curve> curvesHB = new List<Curve> { curveHB };

            Rebar rbHB = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.BarHoriz, abutment, vecZ, curvesHB, new BarTerminationsData(doc), true, true);
            ApplyLayout(rbHB, config.LayoutHoriz, config.SpHorizMm, config.QtyHoriz, arrayLengthH, dH, "VT_StemHoriz");
            createdRebars.Add(rbHB.Id);

            return createdRebars;
        }

        private List<ElementId> CreateStemTieRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, StemRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontMm, UnitTypeId.Millimeters);
            double cvVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackMm, UnitTypeId.Millimeters);
            double cvZVF = UnitUtils.ConvertToInternalUnits(config.CvVertFrontZMm, UnitTypeId.Millimeters);
            double cvZVB = UnitUtils.ConvertToInternalUnits(config.CvVertBackZMm, UnitTypeId.Millimeters);
            double dH = GetBarDiameter(config.BarHoriz);
            XYZ vecZ = new XYZ(0, 0, 1);

            double zBottomFront = geo.MinZ + cvZVF;
            double zBottomBack = geo.MinZ + cvZVB;

            double vertFrontZ_Top = config.UseDowelAsStemVert
                ? (config.AutoVertFrontHeight ? geo.StemMaxZ - config.VertFrontHeightMm / 304.8 : geo.MinUpZ + config.VertFrontHeightMm / 304.8)
                : (config.AutoVertFrontHeight ? geo.StemMaxZ - cvZVF : zBottomFront + config.VertFrontHeightMm / 304.8);
            double vertBackZ_Top = config.UseDowelAsStemVert
                ? (config.AutoVertBackHeight ? geo.StemMaxZ - config.VertBackHeightMm / 304.8 : geo.MinUpZ + config.VertBackHeightMm / 304.8)
                : (config.AutoVertBackHeight ? geo.StemMaxZ - cvZVB : zBottomBack + config.VertBackHeightMm / 304.8);
            
            double cvHZ = UnitUtils.ConvertToInternalUnits(config.CvHorizZMm, UnitTypeId.Millimeters);
            double maxHorizZ = Math.Min(vertFrontZ_Top, vertBackZ_Top) - dH / 2.0;
            double actualLastHorizZ = maxHorizZ;
            if (config.LayoutHoriz == 2 && config.QtyHoriz >= 1) {
                double spInternal = UnitUtils.ConvertToInternalUnits(config.SpHorizMm, UnitTypeId.Millimeters);
                actualLastHorizZ = (geo.MinUpZ + cvHZ) + (config.QtyHoriz - 1) * spInternal;
            }

            EnsureBarType(config.BarTie, "thép đai thân mố");
            double dTie = GetBarDiameter(config.BarTie);
            double spTieV_int = UnitUtils.ConvertToInternalUnits(config.SpTieVMm, UnitTypeId.Millimeters);
            double tieZ_int = UnitUtils.ConvertToInternalUnits(config.TieZMm, UnitTypeId.Millimeters);
            double cvTieY_int = UnitUtils.ConvertToInternalUnits(config.CvTieYMm, UnitTypeId.Millimeters);
            
            XYZ pTie_start_base = geo.AbsBasePt1 
                             + geo.StemThicknessDir * (geo.StemMinT + cvVF - dTie/2.0) 
                             + geo.StemLongDir * (geo.StemMinL + cvTieY_int + dTie/2.0);
            XYZ pTie_end_raw = geo.AbsBasePt1
                             + geo.StemThicknessDir * (geo.StemMaxT - cvVB + dTie/2.0)
                             + geo.StemLongDir * (geo.StemMinL + cvTieY_int + dTie/2.0);
            
            double tieLength = Math.Max(0.01, pTie_start_base.DistanceTo(pTie_end_raw));
            XYZ pTie_end_base = pTie_start_base + geo.StemThicknessDir * tieLength;

            RebarShape tieShape = null;
            if (!string.IsNullOrEmpty(config.TieShapeName)) {
                tieShape = new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().FirstOrDefault(s => s.Name == config.TieShapeName);
            }

            double maxTieZ = actualLastHorizZ + (dH / 2.0 + dTie / 2.0);
            double maxAllowedArrayLengthTieV = maxTieZ - (geo.MinUpZ + tieZ_int);
            double arrayLengthTieV = Math.Max(0.1, maxAllowedArrayLengthTieV);
            int numZ = 0;
            if (config.LayoutTieV == 0) { 
                numZ = (int)(arrayLengthTieV / spTieV_int) + 1;
            } else if (config.LayoutTieV == 1) { 
                numZ = (int)Math.Max(1, config.QtyTieV);
                if (numZ > 1) spTieV_int = arrayLengthTieV / (numZ - 1);
            } else { 
                numZ = (int)Math.Max(1, config.QtyTieV);
            }
            
            if (numZ > 0)
            {
                RebarHookType hook135 = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>().FirstOrDefault(h => h.Name.Contains("135"));
                Action<double> drawTieLevel = (tieZOffset) => {
                    XYZ currentZOffset = vecZ * tieZOffset;
                    Curve tCurve;
                    if (config.TieHookDirection == 1) {
                        tCurve = Line.CreateBound(pTie_start_base + currentZOffset, pTie_end_base + currentZOffset);
                    } else {
                        tCurve = Line.CreateBound(pTie_end_base + currentZOffset, pTie_start_base + currentZOffset);
                    }
                    IList<Curve> tCurves = new List<Curve> { tCurve };

                    Rebar rbTie = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.BarTie, abutment, geo.StemLongDir, tCurves, new BarTerminationsData(doc), true, true);
                    bool shapeApplied = false;
                    if (tieShape != null) {
                        try {
                            rbTie.GetShapeDrivenAccessor().SetRebarShapeId(tieShape.Id);
                            shapeApplied = true;
                        } catch (Exception ex) { 
                            Vetheprevit.TienIch.Logger.LogError("Lỗi khi gán shape thép đai", ex); 
                            Vetheprevit.TienIch.Logger.AddWarning($"⚠ Shape {tieShape.Name} không tương thích, đã dùng Shape tự động.");
                        }
                    }
                    if (!shapeApplied) {
                        if (hook135 != null) {
                            rbTie.SetHookTypeId(0, hook135.Id);
                            rbTie.SetHookTypeId(1, hook135.Id);
                        }
                    }
                    
                    double arrayLengthTieH = Math.Max(0.1, geo.StemLength - 2 * cvTieY_int - dTie);
                    ApplyLayout(rbTie, config.LayoutTieH, config.SpTieHMm, config.QtyTieH, arrayLengthTieH, dTie, "VT_StemTie");
                    createdRebars.Add(rbTie.Id);
                };

                double tieDrop = config.TieDropMm / 304.8;
                double lastTieZOffset = -1;
                for (int i = 0; i < numZ; i++)
                {
                    double tieZOffset = geo.MinUpZ + tieZ_int + i * spTieV_int;
                    if (config.LayoutTieV == 2 && i == numZ - 1) {
                        tieZOffset = maxTieZ - tieDrop;
                    }
                    if (tieZOffset > maxTieZ - tieDrop + 0.001) break;
                    
                    lastTieZOffset = tieZOffset;
                    try { drawTieLevel(tieZOffset); } catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi tạo thép đai thân mố", ex); }
                }
                
                if (config.LayoutTieV != 2 && lastTieZOffset >= 0 && (maxTieZ - tieDrop - lastTieZOffset) > 10.0 / 304.8) {
                    try { drawTieLevel(maxTieZ - tieDrop); } catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi tạo thép đai hoàn thiện thân mố", ex); }
                }
            }
            return createdRebars;
        }
    }
}
