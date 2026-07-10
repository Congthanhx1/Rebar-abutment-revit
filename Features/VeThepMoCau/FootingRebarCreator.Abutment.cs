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
        public List<ElementId> CreateAbutmentRebars(Document doc, FamilyInstance abutment, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            AbutmentGeoInfo geo = GetGeoInfo(doc, abutment);

            using (Transaction t = new Transaction(doc, "Tạo Thép Bệ Mố"))
            {
                try
                {
                    t.Start();

                    DeleteOldAbutmentRebars(doc, abutment, config);

                    if (config.Footing.DrawBotRebar)
                        createdRebars.AddRange(CreateBottomMesh(doc, abutment, geo, config));
                    
                    if (config.Footing.DrawTopRebar)
                        createdRebars.AddRange(CreateTopMesh(doc, abutment, geo, config));
                    
                    if (config.Side.DrawVertSideRebar || config.Side.DrawHorizSideRebar)
                        createdRebars.AddRange(CreateSideRebars(doc, abutment, geo, config));
                    
                    if (config.AntiBurst.DrawAntiBurstRebar)
                        createdRebars.AddRange(CreateAntiBurstRebars(doc, abutment, geo, config));

                    if (config.Dowel.DrawDowelRebar)
                        createdRebars.AddRange(CreateDowelRebars(doc, abutment, geo, config));

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

        private void DeleteOldAbutmentRebars(Document doc, FamilyInstance abutment, AbutmentRebarConfig config)
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
                    if (config.Footing.DrawBotRebar)
                    {
                        if (config.Footing.DrawBotLong && comment == "VT_BotLong") shouldDelete = true;
                        if (config.Footing.DrawBotTrans && comment == "VT_BotTrans") shouldDelete = true;
                        if ((config.Footing.DrawBotLong || config.Footing.DrawBotTrans) && comment == "VT_Bot") shouldDelete = true;
                    }
                    if (config.Footing.DrawTopRebar)
                    {
                        if (config.Footing.DrawTopLong && comment == "VT_TopLong") shouldDelete = true;
                        if (config.Footing.DrawTopTrans && comment == "VT_TopTrans") shouldDelete = true;
                        if ((config.Footing.DrawTopLong || config.Footing.DrawTopTrans) && comment == "VT_Top") shouldDelete = true;
                    }
                    if (config.Side.DrawVertSideRebar)
                    {
                        if (config.Side.DrawSideX && comment == "VT_VertSideX") shouldDelete = true;
                        if (config.Side.DrawSideY && comment == "VT_VertSideY") shouldDelete = true;
                        if ((config.Side.DrawSideX || config.Side.DrawSideY) && comment == "VT_VertSide") shouldDelete = true;
                    }
                    if (config.Side.DrawHorizSideRebar)
                    {
                        if (config.Side.DrawHorizSideX && comment == "VT_HorizSideX") shouldDelete = true;
                        if (config.Side.DrawHorizSideY && comment == "VT_HorizSideY") shouldDelete = true;
                        if ((config.Side.DrawHorizSideX || config.Side.DrawHorizSideY) && comment == "VT_HorizSide") shouldDelete = true;
                    }
                    if (config.Dowel.DrawDowelRebar && comment == "VT_Dowel")
                    {
                        shouldDelete = true;
                    }
                    if (config.AntiBurst.DrawAntiBurstRebar)
                    {
                        if (config.AntiBurst.DrawAntiBurstX && comment == "VT_AntiBurstX") shouldDelete = true;
                        if (config.AntiBurst.DrawAntiBurstY && comment == "VT_AntiBurstY") shouldDelete = true;
                        if ((config.AntiBurst.DrawAntiBurstX || config.AntiBurst.DrawAntiBurstY) && comment == "VT_AntiBurst") shouldDelete = true;
                    }

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
                        catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi nội bộ khi xoá thép mố", ex); }
                    }
                }

                if (rebarsToDelete.Count > 0)
                {
                    try { doc.Delete(rebarsToDelete); }
                    catch (Exception ex) { Vetheprevit.TienIch.Logger.LogError("Lỗi khi xoá mảng thép", ex); }
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

        private List<ElementId> CreateBottomMesh(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvB_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZLongMm, UnitTypeId.Millimeters); 
            double cvB_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZTransMm, UnitTypeId.Millimeters); 
            double cvB_L_X = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotLongXMm, UnitTypeId.Millimeters);
            double cvB_L_Y = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotLongYMm, UnitTypeId.Millimeters);
            double cvB_T_X = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotTransXMm, UnitTypeId.Millimeters);
            double cvB_T_Y = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotTransYMm, UnitTypeId.Millimeters); 
            double dB_Long = GetBarDiameter(config.Footing.BarBotLong);
            double dB_Trans = GetBarDiameter(config.Footing.BarBotTrans);
            double botMeshAnchorLong = UnitUtils.ConvertToInternalUnits(config.Footing.BotMeshAnchorLongMm, UnitTypeId.Millimeters);
            double botMeshAnchorTrans = UnitUtils.ConvertToInternalUnits(config.Footing.BotMeshAnchorTransMm, UnitTypeId.Millimeters);
            
            XYZ LDir = geo.StemLongDir;
            XYZ TDir = geo.StemThicknessDir;
            XYZ ZDir = XYZ.BasisZ;
            double rB_Long = dB_Long / 2.0;
            double rB_Trans = dB_Trans / 2.0;
            
            double trueMinL_B_L = geo.FootMinL + cvB_L_X;
            double trueMaxL_B_L = geo.FootMaxL - cvB_L_X;
            double trueMinT_B_L = geo.FootMinT + cvB_L_Y;
            double trueMaxT_B_L = geo.FootMaxT - cvB_L_Y;
            
            double trueMinL_B_T = geo.FootMinL + cvB_T_X;
            double trueMaxL_B_T = geo.FootMaxL - cvB_T_X;
            double trueMinT_B_T = geo.FootMinT + cvB_T_Y;
            double trueMaxT_B_T = geo.FootMaxT - cvB_T_Y;
            
            double layoutL_B = trueMaxL_B_T - trueMinL_B_T;
            double layoutT_B = trueMaxT_B_L - trueMinT_B_L;

            if (config.Footing.DrawBotTrans)
            {
                EnsureBarType(config.Footing.BarBotTrans, "thép đáy phương ngang");
                XYZ pB_Trans1 = geo.AbsBasePt1 + LDir * trueMinL_B_T + TDir * trueMinT_B_T + ZDir * cvB_Z_Trans;
                XYZ pB_Trans2 = geo.AbsBasePt1 + LDir * trueMinL_B_T + TDir * trueMaxT_B_T + ZDir * cvB_Z_Trans;
                List<Curve> curB_Trans = new List<Curve>();
                if (botMeshAnchorTrans > 0.01) curB_Trans.Add(Line.CreateBound(pB_Trans1 + ZDir * (botMeshAnchorTrans - rB_Trans), pB_Trans1));
                curB_Trans.Add(Line.CreateBound(pB_Trans1, pB_Trans2));
                if (botMeshAnchorTrans > 0.01) curB_Trans.Add(Line.CreateBound(pB_Trans2, pB_Trans2 + ZDir * (botMeshAnchorTrans - rB_Trans)));
                Rebar rb_B_Trans = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Footing.BarBotTrans, abutment, LDir, curB_Trans, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_B_Trans, config.Footing.LayoutTBot, config.Footing.SpTBotMm, config.Footing.QtyTBot, layoutL_B, dB_Trans, "VT_BotTrans");
                createdRebars.Add(rb_B_Trans.Id);
            }

            if (config.Footing.DrawBotLong)
            {
                EnsureBarType(config.Footing.BarBotLong, "thép đáy phương dọc");
                XYZ pB_Long1 = geo.AbsBasePt1 + LDir * trueMinL_B_L + TDir * trueMinT_B_L + ZDir * cvB_Z_Long;
                XYZ pB_Long2 = geo.AbsBasePt1 + LDir * trueMaxL_B_L + TDir * trueMinT_B_L + ZDir * cvB_Z_Long;
                List<Curve> curB_Long = new List<Curve>();
                if (botMeshAnchorLong > 0.01) curB_Long.Add(Line.CreateBound(pB_Long1 + ZDir * (botMeshAnchorLong - rB_Long), pB_Long1));
                curB_Long.Add(Line.CreateBound(pB_Long1, pB_Long2));
                if (botMeshAnchorLong > 0.01) curB_Long.Add(Line.CreateBound(pB_Long2, pB_Long2 + ZDir * (botMeshAnchorLong - rB_Long)));
                Rebar rb_B_Long = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Footing.BarBotLong, abutment, TDir, curB_Long, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_B_Long, config.Footing.LayoutLBot, config.Footing.SpLBotMm, config.Footing.QtyLBot, layoutT_B, dB_Long, "VT_BotLong");
                createdRebars.Add(rb_B_Long.Id);
            }
            return createdRebars;
        }

        private List<ElementId> CreateTopMesh(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvT_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZLongMm, UnitTypeId.Millimeters); 
            double cvT_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZTransMm, UnitTypeId.Millimeters); 
            double cvT_L_X = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopLongXMm, UnitTypeId.Millimeters);
            double cvT_L_Y = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopLongYMm, UnitTypeId.Millimeters);
            double cvT_T_X = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopTransXMm, UnitTypeId.Millimeters);
            double cvT_T_Y = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopTransYMm, UnitTypeId.Millimeters); 
            double dT_Long = GetBarDiameter(config.Footing.BarTopLong);
            double dT_Trans = GetBarDiameter(config.Footing.BarTopTrans);
            double topMeshAnchorLong = UnitUtils.ConvertToInternalUnits(config.Footing.TopMeshAnchorLongMm, UnitTypeId.Millimeters);
            double topMeshAnchorTrans = UnitUtils.ConvertToInternalUnits(config.Footing.TopMeshAnchorTransMm, UnitTypeId.Millimeters);

            XYZ LDir = geo.StemLongDir;
            XYZ TDir = geo.StemThicknessDir;
            XYZ ZDir = XYZ.BasisZ;
            double rT_Long = dT_Long / 2.0;
            double rT_Trans = dT_Trans / 2.0;

            double trueMinL_T_L = geo.FootMinL + cvT_L_X;
            double trueMaxL_T_L = geo.FootMaxL - cvT_L_X;
            double trueMinT_T_L = geo.FootMinT + cvT_L_Y;
            double trueMaxT_T_L = geo.FootMaxT - cvT_L_Y;
            
            double trueMinL_T_T = geo.FootMinL + cvT_T_X;
            double trueMaxL_T_T = geo.FootMaxL - cvT_T_X;
            double trueMinT_T_T = geo.FootMinT + cvT_T_Y;
            double trueMaxT_T_T = geo.FootMaxT - cvT_T_Y;
            
            double layoutL_T = trueMaxL_T_T - trueMinL_T_T;
            double layoutT_T = trueMaxT_T_L - trueMinT_T_L;

            if (config.Footing.DrawTopTrans)
            {
                EnsureBarType(config.Footing.BarTopTrans, "thép đỉnh phương ngang");
                XYZ pT_Trans1 = geo.AbsBasePt1 + LDir * trueMinL_T_T + TDir * trueMinT_T_T + ZDir * (geo.FootingHeight - cvT_Z_Trans);
                XYZ pT_Trans2 = geo.AbsBasePt1 + LDir * trueMinL_T_T + TDir * trueMaxT_T_T + ZDir * (geo.FootingHeight - cvT_Z_Trans);
                List<Curve> curT_Trans = new List<Curve>();
                if (topMeshAnchorTrans > 0.01) curT_Trans.Add(Line.CreateBound(pT_Trans1 - ZDir * (topMeshAnchorTrans - rT_Trans), pT_Trans1));
                curT_Trans.Add(Line.CreateBound(pT_Trans1, pT_Trans2));
                if (topMeshAnchorTrans > 0.01) curT_Trans.Add(Line.CreateBound(pT_Trans2, pT_Trans2 - ZDir * (topMeshAnchorTrans - rT_Trans)));
                Rebar rb_T_Trans = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Footing.BarTopTrans, abutment, LDir, curT_Trans, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_T_Trans, config.Footing.LayoutTTop, config.Footing.SpTTopMm, config.Footing.QtyTTop, layoutL_T, dT_Trans, "VT_TopTrans");
                createdRebars.Add(rb_T_Trans.Id);
            }

            if (config.Footing.DrawTopLong)
            {
                EnsureBarType(config.Footing.BarTopLong, "thép đỉnh phương dọc");
                XYZ pT_Long1 = geo.AbsBasePt1 + LDir * trueMinL_T_L + TDir * trueMaxT_T_L + ZDir * (geo.FootingHeight - cvT_Z_Long);
                XYZ pT_Long2 = geo.AbsBasePt1 + LDir * trueMaxL_T_L + TDir * trueMaxT_T_L + ZDir * (geo.FootingHeight - cvT_Z_Long);
                List<Curve> curT_Long = new List<Curve>();
                if (topMeshAnchorLong > 0.01) curT_Long.Add(Line.CreateBound(pT_Long1 - ZDir * (topMeshAnchorLong - rT_Long), pT_Long1));
                curT_Long.Add(Line.CreateBound(pT_Long1, pT_Long2));
                if (topMeshAnchorLong > 0.01) curT_Long.Add(Line.CreateBound(pT_Long2, pT_Long2 - ZDir * (topMeshAnchorLong - rT_Long)));
                Rebar rb_T_Long = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Footing.BarTopLong, abutment, -TDir, curT_Long, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_T_Long, config.Footing.LayoutLTop, config.Footing.SpLTopMm, config.Footing.QtyLTop, layoutT_T, dT_Long, "VT_TopLong");
                createdRebars.Add(rb_T_Long.Id);
            }
            return createdRebars;
        }

        private List<ElementId> CreateSideRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvB_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZLongMm, UnitTypeId.Millimeters); 
            double cvB_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZTransMm, UnitTypeId.Millimeters); 
            double cvT_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZLongMm, UnitTypeId.Millimeters); 
            double cvT_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZTransMm, UnitTypeId.Millimeters); 
            double dB_Long = GetBarDiameter(config.Footing.BarBotLong);
            double dB_Trans = GetBarDiameter(config.Footing.BarBotTrans);
            double dT_Long = GetBarDiameter(config.Footing.BarTopLong);
            double dT_Trans = GetBarDiameter(config.Footing.BarTopTrans);
            double dS_X = GetBarDiameter(config.Side.BarSideX);
            double dS_Y = GetBarDiameter(config.Side.BarSideY);
            
            XYZ LDir = geo.StemLongDir;
            XYZ TDir = geo.StemThicknessDir;
            XYZ ZDir = XYZ.BasisZ;

            if (config.Side.DrawVertSideRebar)
            {
                double zSideBot;
                double zSideTop;

                if (config.Side.AutoSideHeight)
                {
                    double dSideForZ = config.Side.DrawSideX && dS_X > 0 ? dS_X : dS_Y;
                    double botLayerZ_BotMesh = Math.Min(cvB_Z_Trans, cvB_Z_Long);
                    double botLayerDia_BotMesh = (cvB_Z_Trans < cvB_Z_Long) ? dB_Trans : dB_Long;
                    zSideBot = botLayerZ_BotMesh - botLayerDia_BotMesh / 2.0 + dSideForZ / 2.0;

                    double topLayerZ_TopMesh = Math.Max(geo.FootingHeight - cvT_Z_Trans, geo.FootingHeight - cvT_Z_Long);
                    double topLayerDia_TopMesh = ((geo.FootingHeight - cvT_Z_Trans) > (geo.FootingHeight - cvT_Z_Long)) ? dT_Trans : dT_Long;
                    zSideTop = topLayerZ_TopMesh + topLayerDia_TopMesh / 2.0 - dSideForZ / 2.0;
                }
                else
                {
                    zSideBot = UnitUtils.ConvertToInternalUnits(config.Side.CvVertSideZBotMm, UnitTypeId.Millimeters);
                    zSideTop = geo.FootingHeight - UnitUtils.ConvertToInternalUnits(config.Side.CvVertSideZTopMm, UnitTypeId.Millimeters);
                }
                double curSideHeight = zSideTop - zSideBot;

                if (zSideTop > zSideBot + 0.1)
                {
                    double cvVS = UnitUtils.ConvertToInternalUnits(config.Side.CvVertSideMm, UnitTypeId.Millimeters);
                    double offsetSideX_int = UnitUtils.ConvertToInternalUnits(config.Side.OffsetSideXMm, UnitTypeId.Millimeters);
                    double offsetSideY_int = UnitUtils.ConvertToInternalUnits(config.Side.OffsetSideYMm, UnitTypeId.Millimeters);
                    double sMinL = geo.FootMinL + offsetSideX_int;
                    double sMaxL = geo.FootMaxL - offsetSideX_int;
                    double sMinT = geo.FootMinT + offsetSideY_int;
                    double sMaxT = geo.FootMaxT - offsetSideY_int;
                    double layoutLong = sMaxL - sMinL;
                    double layoutTrans = sMaxT - sMinT;
                    
                    if (config.Side.DrawSideX)
                    {
                        EnsureBarType(config.Side.BarSideX, "thép hông đứng phương X");
                        double anchorSideX = UnitUtils.ConvertToInternalUnits(config.Side.VertSideAnchorMm, UnitTypeId.Millimeters);
                        if (anchorSideX > layoutTrans / 2.5) {
                            Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép đứng mặt bên (X) bị giảm từ {config.Side.VertSideAnchorMm} mm xuống {(layoutTrans / 2.5 * 304.8):F1} mm để vừa kích thước bệ mố.");
                            anchorSideX = layoutTrans / 2.5;
                        }
                        anchorSideX -= dS_X / 2;

                        double cvVS_faceX = geo.FootMinT + cvVS;
                        XYZ pL_Top = geo.AbsBasePt1 + LDir * sMinL + TDir * cvVS_faceX + ZDir * (zSideBot + curSideHeight);
                        XYZ pL_Bot = geo.AbsBasePt1 + LDir * sMinL + TDir * cvVS_faceX + ZDir * zSideBot;
                        List<Curve> curSideLeft = new List<Curve> { Line.CreateBound(pL_Top, pL_Bot) };
                        if (anchorSideX > 0.01) {
                            curSideLeft.Insert(0, Line.CreateBound(pL_Top + TDir * anchorSideX, pL_Top));
                            curSideLeft.Add(Line.CreateBound(pL_Bot, pL_Bot + TDir * anchorSideX));
                        }
                        Rebar rb_SideLeft = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarSideX, abutment, LDir, curSideLeft, new BarTerminationsData(doc), true, true);
                        ApplyLayout(rb_SideLeft, config.Side.LayoutVSideX, config.Side.SpVSideXMm, config.Side.QtyVSideX, layoutLong, dS_X, "VT_VertSideX");
                        createdRebars.Add(rb_SideLeft.Id);

                        double cvVS_faceX2 = geo.FootMaxT - cvVS;
                        XYZ pR_Top = geo.AbsBasePt1 + LDir * sMinL + TDir * cvVS_faceX2 + ZDir * (zSideBot + curSideHeight);
                        XYZ pR_Bot = geo.AbsBasePt1 + LDir * sMinL + TDir * cvVS_faceX2 + ZDir * zSideBot;
                        List<Curve> curSideRight = new List<Curve> { Line.CreateBound(pR_Top, pR_Bot) };
                        if (anchorSideX > 0.01) {
                            curSideRight.Insert(0, Line.CreateBound(pR_Top - TDir * anchorSideX, pR_Top));
                            curSideRight.Add(Line.CreateBound(pR_Bot, pR_Bot - TDir * anchorSideX));
                        }
                        Rebar rb_SideRight = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarSideX, abutment, LDir, curSideRight, new BarTerminationsData(doc), true, true);
                        ApplyLayout(rb_SideRight, config.Side.LayoutVSideX, config.Side.SpVSideXMm, config.Side.QtyVSideX, layoutLong, dS_X, "VT_VertSideX");
                        createdRebars.Add(rb_SideRight.Id);
                    }

                    if (config.Side.DrawSideY)
                    {
                        EnsureBarType(config.Side.BarSideY, "thép hông đứng phương Y");
                        double anchorSideY = UnitUtils.ConvertToInternalUnits(config.Side.VertSideAnchorMm, UnitTypeId.Millimeters);
                        if (anchorSideY > layoutLong / 2.5) {
                            Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép đứng mặt bên (Y) bị giảm từ {config.Side.VertSideAnchorMm} mm xuống {(layoutLong / 2.5 * 304.8):F1} mm để vừa kích thước bệ mố.");
                            anchorSideY = layoutLong / 2.5;
                        }
                        anchorSideY -= dS_Y / 2;
                        double cvVS_faceY = geo.FootMinL + cvVS;
                        XYZ pF_Top = geo.AbsBasePt1 + LDir * cvVS_faceY + TDir * sMinT + ZDir * (zSideBot + curSideHeight);
                        XYZ pF_Bot = geo.AbsBasePt1 + LDir * cvVS_faceY + TDir * sMinT + ZDir * zSideBot;
                        List<Curve> curSideFront = new List<Curve> { Line.CreateBound(pF_Top, pF_Bot) };
                        if (anchorSideY > 0.01) {
                            curSideFront.Insert(0, Line.CreateBound(pF_Top + LDir * anchorSideY, pF_Top));
                            curSideFront.Add(Line.CreateBound(pF_Bot, pF_Bot + LDir * anchorSideY));
                        }
                        Rebar rb_SideFront = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarSideY, abutment, TDir, curSideFront, new BarTerminationsData(doc), true, true);
                        ApplyLayout(rb_SideFront, config.Side.LayoutVSideY, config.Side.SpVSideYMm, config.Side.QtyVSideY, layoutTrans, dS_Y, "VT_VertSideY");
                        createdRebars.Add(rb_SideFront.Id);

                        double cvVS_faceY2 = geo.FootMaxL - cvVS;
                        XYZ pB_Top = geo.AbsBasePt1 + LDir * cvVS_faceY2 + TDir * sMinT + ZDir * (zSideBot + curSideHeight);
                        XYZ pB_Bot = geo.AbsBasePt1 + LDir * cvVS_faceY2 + TDir * sMinT + ZDir * zSideBot;
                        List<Curve> curSideBack = new List<Curve> { Line.CreateBound(pB_Top, pB_Bot) };
                        if (anchorSideY > 0.01) {
                            curSideBack.Insert(0, Line.CreateBound(pB_Top - LDir * anchorSideY, pB_Top));
                            curSideBack.Add(Line.CreateBound(pB_Bot, pB_Bot - LDir * anchorSideY));
                        }
                        Rebar rb_SideBack = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarSideY, abutment, TDir, curSideBack, new BarTerminationsData(doc), true, true);
                        ApplyLayout(rb_SideBack, config.Side.LayoutVSideY, config.Side.SpVSideYMm, config.Side.QtyVSideY, layoutTrans, dS_Y, "VT_VertSideY");
                        createdRebars.Add(rb_SideBack.Id);
                    }
                }
            }

            if (config.Side.DrawHorizSideRebar)
            {
                double cvBotHS = UnitUtils.ConvertToInternalUnits(config.Side.CvBotHorizSideMm, UnitTypeId.Millimeters);
                if ((geo.FootMaxL - geo.FootMinL) > 0.5)
                {
                    double cvHS_X = UnitUtils.ConvertToInternalUnits(config.Side.CvHorizSideXMm, UnitTypeId.Millimeters);
                    double cvHS_Y = UnitUtils.ConvertToInternalUnits(config.Side.CvHorizSideYMm, UnitTypeId.Millimeters);
                    double trueMinL_HS = geo.FootMinL + cvHS_X;
                    double trueMaxL_HS = geo.FootMaxL - cvHS_X;
                    double trueMinT_HS = geo.FootMinT + cvHS_Y;
                    double trueMaxT_HS = geo.FootMaxT - cvHS_Y;

                    double layoutL_HS = trueMaxL_HS - trueMinL_HS;
                    double layoutT_HS = trueMaxT_HS - trueMinT_HS;

                    double dX = GetBarDiameter(config.Side.BarHorizSideX);
                    double dY = GetBarDiameter(config.Side.BarHorizSideY);
                    
                    double botX = cvBotHS + dX / 2;
                    double botY = cvBotHS + dY / 2;
                    double topX = geo.FootingHeight - cvBotHS - dX / 2;
                    double topY = geo.FootingHeight - cvBotHS - dY / 2;

                    if (config.Side.DrawHorizSideY)
                    {
                        EnsureBarType(config.Side.BarHorizSideY, "thép dọc hông phương Y");
                        double layoutZY = topY - botY;
                        if (layoutZY > 0.1)
                        {
                            double requestedAnchorL = UnitUtils.ConvertToInternalUnits(config.Side.HorizAnchorMm, UnitTypeId.Millimeters);
                            double maxAnchorL = layoutL_HS / 2;
                            if (requestedAnchorL > maxAnchorL) {
                                Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép ngang mặt bên (Y) bị giảm từ {config.Side.HorizAnchorMm} mm xuống {(maxAnchorL * 304.8):F1} mm để vừa kích thước bệ mố.");
                            }
                            double realAnchorHorizL = Math.Max(0, Math.Min(requestedAnchorL, maxAnchorL) - dY / 2);

                            XYZ pHL_Front = geo.AbsBasePt1 + LDir * trueMinL_HS + TDir * trueMinT_HS + ZDir * botY;
                            XYZ pHL_Back = geo.AbsBasePt1 + LDir * trueMinL_HS + TDir * trueMaxT_HS + ZDir * botY;
                            List<Curve> curHorizLeft = new List<Curve> { Line.CreateBound(pHL_Front, pHL_Back) };
                            if (realAnchorHorizL > 0.01) {
                                curHorizLeft.Insert(0, Line.CreateBound(pHL_Front + LDir * realAnchorHorizL, pHL_Front));
                                curHorizLeft.Add(Line.CreateBound(pHL_Back, pHL_Back + LDir * realAnchorHorizL));
                            }
                            Rebar rb_HorizLeft = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarHorizSideY, abutment, ZDir, curHorizLeft, new BarTerminationsData(doc), true, true);
                            ApplyLayout(rb_HorizLeft, config.Side.LayoutHorizSideY, config.Side.SpHorizSideYMm, config.Side.QtyHorizSideY, layoutZY, dY, "VT_HorizSideY");
                            createdRebars.Add(rb_HorizLeft.Id);

                            XYZ pHR_Back = geo.AbsBasePt1 + LDir * trueMaxL_HS + TDir * trueMaxT_HS + ZDir * botY;
                            XYZ pHR_Front = geo.AbsBasePt1 + LDir * trueMaxL_HS + TDir * trueMinT_HS + ZDir * botY;
                            List<Curve> curHorizRight = new List<Curve> { Line.CreateBound(pHR_Front, pHR_Back) };
                            if (realAnchorHorizL > 0.01) {
                                curHorizRight.Insert(0, Line.CreateBound(pHR_Front - LDir * realAnchorHorizL, pHR_Front));
                                curHorizRight.Add(Line.CreateBound(pHR_Back, pHR_Back - LDir * realAnchorHorizL));
                            }
                            Rebar rb_HorizRight = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarHorizSideY, abutment, ZDir, curHorizRight, new BarTerminationsData(doc), true, true);
                            ApplyLayout(rb_HorizRight, config.Side.LayoutHorizSideY, config.Side.SpHorizSideYMm, config.Side.QtyHorizSideY, layoutZY, dY, "VT_HorizSideY");
                            createdRebars.Add(rb_HorizRight.Id);
                        }
                    }

                    if (config.Side.DrawHorizSideX)
                    {
                        EnsureBarType(config.Side.BarHorizSideX, "thép dọc hông phương X");
                        double layoutZX = topX - botX;
                        if (layoutZX > 0.1)
                        {
                            double requestedAnchorT = UnitUtils.ConvertToInternalUnits(config.Side.HorizAnchorMm, UnitTypeId.Millimeters);
                            double maxAnchorT = layoutT_HS / 2;
                            if (requestedAnchorT > maxAnchorT) {
                                Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép ngang mặt bên (X) bị giảm từ {config.Side.HorizAnchorMm} mm xuống {(maxAnchorT * 304.8):F1} mm để vừa kích thước bệ mố.");
                            }
                            double realAnchorHorizT = Math.Max(0, Math.Min(requestedAnchorT, maxAnchorT) - dX / 2);

                            XYZ pHF_Left = geo.AbsBasePt1 + LDir * trueMinL_HS + TDir * trueMinT_HS + ZDir * botX;
                            XYZ pHF_Right = geo.AbsBasePt1 + LDir * trueMaxL_HS + TDir * trueMinT_HS + ZDir * botX;
                            List<Curve> curHorizFront = new List<Curve> { Line.CreateBound(pHF_Left, pHF_Right) };
                            if (realAnchorHorizT > 0.01) {
                                curHorizFront.Insert(0, Line.CreateBound(pHF_Left + TDir * realAnchorHorizT, pHF_Left));
                                curHorizFront.Add(Line.CreateBound(pHF_Right, pHF_Right + TDir * realAnchorHorizT));
                            }
                            Rebar rb_HorizFront = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarHorizSideX, abutment, ZDir, curHorizFront, new BarTerminationsData(doc), true, true);
                            ApplyLayout(rb_HorizFront, config.Side.LayoutHorizSideX, config.Side.SpHorizSideXMm, config.Side.QtyHorizSideX, layoutZX, dX, "VT_HorizSideX");
                            createdRebars.Add(rb_HorizFront.Id);

                            XYZ pHB_Left = geo.AbsBasePt1 + LDir * trueMinL_HS + TDir * trueMaxT_HS + ZDir * botX;
                            XYZ pHB_Right = geo.AbsBasePt1 + LDir * trueMaxL_HS + TDir * trueMaxT_HS + ZDir * botX;
                            List<Curve> curHorizBack = new List<Curve> { Line.CreateBound(pHB_Left, pHB_Right) };
                            if (realAnchorHorizT > 0.01) {
                                curHorizBack.Insert(0, Line.CreateBound(pHB_Left - TDir * realAnchorHorizT, pHB_Left));
                                curHorizBack.Add(Line.CreateBound(pHB_Right, pHB_Right - TDir * realAnchorHorizT));
                            }
                            Rebar rb_HorizBack = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Side.BarHorizSideX, abutment, ZDir, curHorizBack, new BarTerminationsData(doc), true, true);
                            ApplyLayout(rb_HorizBack, config.Side.LayoutHorizSideX, config.Side.SpHorizSideXMm, config.Side.QtyHorizSideX, layoutZX, dX, "VT_HorizSideX");
                            createdRebars.Add(rb_HorizBack.Id);
                        }
                    }
                }
            }
            return createdRebars;
        }

        private List<ElementId> CreateAntiBurstRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvB_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZLongMm, UnitTypeId.Millimeters); 
            double cvB_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZTransMm, UnitTypeId.Millimeters); 
            double cvT_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZLongMm, UnitTypeId.Millimeters); 
            double cvT_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopZTransMm, UnitTypeId.Millimeters); 

            XYZ LDir = geo.StemLongDir;
            XYZ TDir = geo.StemThicknessDir;
            XYZ ZDir = XYZ.BasisZ;

            double coverX = UnitUtils.ConvertToInternalUnits(config.AntiBurst.CvAntiBurstXMm, UnitTypeId.Millimeters);
            double coverY = UnitUtils.ConvertToInternalUnits(config.AntiBurst.CvAntiBurstYMm, UnitTypeId.Millimeters);
            double coverZ = UnitUtils.ConvertToInternalUnits(config.AntiBurst.CvAntiBurstZMm, UnitTypeId.Millimeters);
            double height = UnitUtils.ConvertToInternalUnits(config.AntiBurst.AntiBurstHeightMm, UnitTypeId.Millimeters);
            double anchor = UnitUtils.ConvertToInternalUnits(config.AntiBurst.AntiBurstAnchorMm, UnitTypeId.Millimeters);
            double sideMinL = geo.FootMinL + coverX;
            double sideMaxL = geo.FootMaxL - coverX;
            double sideMinT = geo.FootMinT + coverY;
            double sideMaxT = geo.FootMaxT - coverY;
            double bottomZ = config.AntiBurst.AutoAntiBurstHeight
                ? Math.Min(cvB_Z_Trans, cvB_Z_Long)
                : coverZ;
            
            double topZAuto = config.AntiBurst.AutoAntiBurstHeight
                ? geo.FootingHeight - Math.Min(cvT_Z_Trans, cvT_Z_Long)
                : 0;

            if (sideMaxL <= sideMinL || sideMaxT <= sideMinT)
                throw new Exception("Thông số thép chống nở hông không phù hợp kích thước bệ mố.");

            if (config.AntiBurst.DrawAntiBurstX)
            {
                EnsureBarType(config.AntiBurst.BarAntiBurstX, "thép chống nở hông X");
                double diameter = GetBarDiameter(config.AntiBurst.BarAntiBurstX);
                double topZ = config.AntiBurst.AutoAntiBurstHeight ? topZAuto : bottomZ + height - diameter;
                if (topZ <= bottomZ) throw new Exception("Chiều cao thép chống nở hông X không hợp lệ.");
                double startL = geo.FootMinL + coverX;
                double layoutLength = Math.Max(0, geo.FootMaxL - diameter / 2 - startL);
                IList<double> offsets = SpacingSequence.GetOffsetsInternal(config.AntiBurst.SpacingSequenceAntiBurstX, layoutLength, "X");
                double maxAnchorX = (sideMaxT - sideMinT) / 2.5;
                if (anchor > maxAnchorX) {
                    Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép chống nở (X) bị giảm từ {config.AntiBurst.AntiBurstAnchorMm} mm xuống {(maxAnchorX * 304.8):F1} mm để vừa kích thước bệ mố.");
                }
                double realAnchor = Math.Max(0.01, Math.Min(anchor, maxAnchorX) - diameter / 2);
                foreach (double offset in offsets)
                {
                    double l = startL + offset;
                    foreach (double sideT in new[] { sideMinT, sideMaxT })
                    {
                        double direction = sideT == sideMinT ? 1 : -1;
                        XYZ bottom = geo.AbsBasePt1 + LDir * l + TDir * sideT + ZDir * bottomZ;
                        XYZ top = geo.AbsBasePt1 + LDir * l + TDir * sideT + ZDir * topZ;
                        List<Curve> curves = new List<Curve> { Line.CreateBound(top, bottom) };
                        if (realAnchor > 0.01) {
                            curves.Insert(0, Line.CreateBound(top + TDir * direction * realAnchor, top));
                            curves.Add(Line.CreateBound(bottom, bottom + TDir * direction * realAnchor));
                        }
                        Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.AntiBurst.BarAntiBurstX, abutment, LDir, curves, new BarTerminationsData(doc), true, true);
                        Parameter comment = rebar.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comment != null && !comment.IsReadOnly) comment.Set("VT_AntiBurstX");
                        RebarMarkerService.SetMarker(rebar, abutment.Id, "VT_AntiBurstX");
                        createdRebars.Add(rebar.Id);
                    }
                }
            }

            if (config.AntiBurst.DrawAntiBurstY)
            {
                EnsureBarType(config.AntiBurst.BarAntiBurstY, "thép chống nở hông Y");
                double diameter = GetBarDiameter(config.AntiBurst.BarAntiBurstY);
                double topZ = config.AntiBurst.AutoAntiBurstHeight ? topZAuto : bottomZ + height - diameter;
                if (topZ <= bottomZ) throw new Exception("Chiều cao thép chống nở hông Y không hợp lệ.");
                double startT = geo.FootMinT + coverY;
                double layoutLength = Math.Max(0, geo.FootMaxT - diameter / 2 - startT);
                IList<double> offsets = SpacingSequence.GetOffsetsInternal(config.AntiBurst.SpacingSequenceAntiBurstY, layoutLength, "Y");
                double maxAnchorY = (sideMaxL - sideMinL) / 2.5;
                if (anchor > maxAnchorY) {
                    Vetheprevit.TienIch.Logger.AddWarning($"⚠ Chiều neo thép chống nở (Y) bị giảm từ {config.AntiBurst.AntiBurstAnchorMm} mm xuống {(maxAnchorY * 304.8):F1} mm để vừa kích thước bệ mố.");
                }
                double realAnchor = Math.Max(0.01, Math.Min(anchor, maxAnchorY) - diameter / 2);
                foreach (double offset in offsets)
                {
                    double t = startT + offset;
                    foreach (double sideL in new[] { sideMinL, sideMaxL })
                    {
                        double direction = sideL == sideMinL ? 1 : -1;
                        XYZ bottom = geo.AbsBasePt1 + LDir * sideL + TDir * t + ZDir * bottomZ;
                        XYZ top = geo.AbsBasePt1 + LDir * sideL + TDir * t + ZDir * topZ;
                        List<Curve> curves = new List<Curve> { Line.CreateBound(top, bottom) };
                        if (realAnchor > 0.01) {
                            curves.Insert(0, Line.CreateBound(top + LDir * direction * realAnchor, top));
                            curves.Add(Line.CreateBound(bottom, bottom + LDir * direction * realAnchor));
                        }
                        Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.AntiBurst.BarAntiBurstY, abutment, TDir, curves, new BarTerminationsData(doc), true, true);
                        Parameter comment = rebar.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comment != null && !comment.IsReadOnly) comment.Set("VT_AntiBurstY");
                        RebarMarkerService.SetMarker(rebar, abutment.Id, "VT_AntiBurstY");
                        createdRebars.Add(rebar.Id);
                    }
                }
            }

            return createdRebars;
        }

        private List<ElementId> CreateDowelRebars(Document doc, FamilyInstance abutment, AbutmentGeoInfo geo, AbutmentRebarConfig config)
        {
            List<ElementId> createdRebars = new List<ElementId>();
            double cvB_Z_Long = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZLongMm, UnitTypeId.Millimeters); 
            double cvB_Z_Trans = UnitUtils.ConvertToInternalUnits(config.Footing.CvBotZTransMm, UnitTypeId.Millimeters); 
            double cvT_T_Y = UnitUtils.ConvertToInternalUnits(config.Footing.CvTopTransYMm, UnitTypeId.Millimeters); 
            double dB_Long = GetBarDiameter(config.Footing.BarBotLong);
            double dB_Trans = GetBarDiameter(config.Footing.BarBotTrans);
            EnsureBarType(config.Dowel.BarDowel, "thép chờ thân mố");
            double dD = GetBarDiameter(config.Dowel.BarDowel);

            XYZ LDir = geo.StemLongDir;
            XYZ TDir = geo.StemThicknessDir;
            XYZ ZDir = XYZ.BasisZ;

            if (geo.StemMinT != double.MaxValue && geo.StemMaxT != double.MinValue && (geo.StemMaxT - geo.StemMinT) > 0.5)
            {
                double dowelHookLength = config.Dowel.DowelAnchorMm / 304.8 - dD / 2;
                double dowelZ_Bottom = geo.MinZ + Math.Max(cvB_Z_Trans, cvB_Z_Long) + Math.Max(dB_Trans, dB_Long);
                double dowelZ_Top = config.Dowel.AutoDowelHeight ? (geo.MaxZ - config.Dowel.DowelHeightMm / 304.8) : (geo.MinUpZ + config.Dowel.DowelHeightMm / 304.8);
                double offsetLong = config.Dowel.DowelLongOffsetMm / 304.8 + dD / 2.0;
                double dowelLayoutLength = Math.Max(0.1, (geo.StemMaxL - geo.StemMinL) - 2 * offsetLong);
                
                XYZ dowelBaseStart = geo.AbsBasePt1 + LDir * (geo.StemMinL + offsetLong);

                XYZ leftBase = dowelBaseStart + TDir * (geo.StemMinT + cvT_T_Y + dD / 2);
                List<Curve> cur_DL = new List<Curve> { Line.CreateBound(new XYZ(leftBase.X, leftBase.Y, dowelZ_Top), new XYZ(leftBase.X, leftBase.Y, dowelZ_Bottom)) };
                if (dowelHookLength > 0.01) cur_DL.Add(Line.CreateBound(new XYZ(leftBase.X, leftBase.Y, dowelZ_Bottom), new XYZ(leftBase.X, leftBase.Y, dowelZ_Bottom) - TDir * dowelHookLength));
                Rebar rb_DL = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Dowel.BarDowel, abutment, LDir, cur_DL, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_DL, config.Dowel.LayoutDowel, config.Dowel.SpDowelMm, config.Dowel.QtyDowel, dowelLayoutLength, dD, "VT_Dowel");
                createdRebars.Add(rb_DL.Id);

                XYZ rightBase = dowelBaseStart + TDir * (geo.StemMaxT - cvT_T_Y - dD / 2);
                List<Curve> cur_DR = new List<Curve> { Line.CreateBound(new XYZ(rightBase.X, rightBase.Y, dowelZ_Top), new XYZ(rightBase.X, rightBase.Y, dowelZ_Bottom)) };
                if (dowelHookLength > 0.01) cur_DR.Add(Line.CreateBound(new XYZ(rightBase.X, rightBase.Y, dowelZ_Bottom), new XYZ(rightBase.X, rightBase.Y, dowelZ_Bottom) + TDir * dowelHookLength));
                Rebar rb_DR = Rebar.CreateFromCurves(doc, RebarStyle.Standard, config.Dowel.BarDowel, abutment, LDir, cur_DR, new BarTerminationsData(doc), true, true);
                ApplyLayout(rb_DR, config.Dowel.LayoutDowel, config.Dowel.SpDowelMm, config.Dowel.QtyDowel, dowelLayoutLength, dD, "VT_Dowel");
                createdRebars.Add(rb_DR.Id);
            }
            return createdRebars;
        }
    }
}
