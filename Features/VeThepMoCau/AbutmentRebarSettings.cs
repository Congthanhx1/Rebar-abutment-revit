using Autodesk.Revit.DB.Structure;

namespace Vetheprevit.MoCau
{
    public class FootingMeshSettings
    {
        public bool DrawBotRebar { get; set; }
        public bool DrawTopRebar { get; set; }
        public bool DrawBotLong { get; set; }
        public bool DrawBotTrans { get; set; }
        public bool DrawTopLong { get; set; }
        public bool DrawTopTrans { get; set; }

        public RebarBarType BarBotLong { get; set; }
        public RebarBarType BarBotTrans { get; set; }
        public double CvBotZLongMm { get; set; }
        public double CvBotZTransMm { get; set; }
        public double CvBotLongXMm { get; set; }
        public double CvBotLongYMm { get; set; }
        public double CvBotTransXMm { get; set; }
        public double CvBotTransYMm { get; set; }
        public double BotMeshAnchorLongMm { get; set; }
        public double BotMeshAnchorTransMm { get; set; }
        public int LayoutLBot { get; set; }
        public double SpLBotMm { get; set; }
        public double QtyLBot { get; set; }
        public int LayoutTBot { get; set; }
        public double SpTBotMm { get; set; }
        public double QtyTBot { get; set; }

        public RebarBarType BarTopLong { get; set; }
        public RebarBarType BarTopTrans { get; set; }
        public double CvTopZLongMm { get; set; }
        public double CvTopZTransMm { get; set; }
        public double CvTopLongXMm { get; set; }
        public double CvTopLongYMm { get; set; }
        public double CvTopTransXMm { get; set; }
        public double CvTopTransYMm { get; set; }
        public double TopMeshAnchorLongMm { get; set; }
        public double TopMeshAnchorTransMm { get; set; }
        public int LayoutLTop { get; set; }
        public double SpLTopMm { get; set; }
        public double QtyLTop { get; set; }
        public int LayoutTTop { get; set; }
        public double SpTTopMm { get; set; }
        public double QtyTTop { get; set; }
    }

    public class SideRebarSettings
    {
        public bool DrawVertSideRebar { get; set; }
        public bool DrawSideX { get; set; }
        public bool DrawSideY { get; set; }
        public bool DrawHorizSideRebar { get; set; }
        public bool DrawHorizSideX { get; set; }
        public bool DrawHorizSideY { get; set; }

        public RebarBarType BarSideX { get; set; }
        public double CvVertSideMm { get; set; }
        public double CvVertSideZBotMm { get; set; }
        public double CvVertSideZTopMm { get; set; }
        public double OffsetSideXMm { get; set; }
        public int LayoutVSideX { get; set; }
        public double SpVSideXMm { get; set; }
        public double QtyVSideX { get; set; }

        public RebarBarType BarSideY { get; set; }
        public double OffsetSideYMm { get; set; }
        public int LayoutVSideY { get; set; }
        public double SpVSideYMm { get; set; }
        public double QtyVSideY { get; set; }

        public bool AutoSideHeight { get; set; }
        public double SideHeightMm { get; set; }
        public double VertSideAnchorMm { get; set; }

        public RebarBarType BarHorizSideX { get; set; }
        public RebarBarType BarHorizSideY { get; set; }
        public double CvHorizSideXMm { get; set; }
        public double CvHorizSideYMm { get; set; }
        public double CvBotHorizSideMm { get; set; }
        public int LayoutHorizSideX { get; set; }
        public double SpHorizSideXMm { get; set; }
        public double QtyHorizSideX { get; set; }
        public int LayoutHorizSideY { get; set; }
        public double SpHorizSideYMm { get; set; }
        public double QtyHorizSideY { get; set; }
        public double HorizAnchorMm { get; set; }
        public int HorizSideTopPos { get; set; }
    }

    public class DowelRebarSettings
    {
        public bool DrawDowelRebar { get; set; }
        public RebarBarType BarDowel { get; set; }
        public int LayoutDowel { get; set; }
        public double SpDowelMm { get; set; }
        public double QtyDowel { get; set; }
        public bool AutoDowelHeight { get; set; }
        public double DowelHeightMm { get; set; }
        public double DowelLongOffsetMm { get; set; }
        public double DowelAnchorMm { get; set; }
    }

    public class AntiBurstSettings
    {
        public bool DrawAntiBurstRebar { get; set; }
        public bool DrawAntiBurstX { get; set; }
        public bool DrawAntiBurstY { get; set; }
        public RebarBarType BarAntiBurstX { get; set; }
        public RebarBarType BarAntiBurstY { get; set; }
        public double CvAntiBurstXMm { get; set; }
        public double CvAntiBurstYMm { get; set; }
        public double CvAntiBurstZMm { get; set; }
        public bool AutoAntiBurstHeight { get; set; }
        public double AntiBurstHeightMm { get; set; }
        public double AntiBurstAnchorMm { get; set; }
        public string SpacingSequenceAntiBurstX { get; set; }
        public string SpacingSequenceAntiBurstY { get; set; }
    }

    public class AbutmentRebarConfig
    {
        public FootingMeshSettings Footing { get; set; }
        public SideRebarSettings Side { get; set; }
        public DowelRebarSettings Dowel { get; set; }
        public AntiBurstSettings AntiBurst { get; set; }
    }

    public class StemRebarConfig
    {
        public bool UseDowelAsStemVert { get; set; }
        public bool DrawStemVertFront { get; set; }
        public bool DrawStemVertBack { get; set; }
        public bool DrawStemHoriz { get; set; }
        public bool DrawStemTie { get; set; }

        public RebarBarType BarVertFront { get; set; }
        public double CvVertFrontMm { get; set; }
        public double CvVertFrontZMm { get; set; }
        public int LayoutVertFront { get; set; }
        public double SpVertFrontMm { get; set; }
        public double QtyVertFront { get; set; }
        public bool AutoVertFrontHeight { get; set; }
        public double VertFrontHeightMm { get; set; }
        public double VertFrontLongOffsetMm { get; set; }
        public double AnchorFrontMm { get; set; }

        public RebarBarType BarVertBack { get; set; }
        public double CvVertBackMm { get; set; }
        public double CvVertBackZMm { get; set; }
        public int LayoutVertBack { get; set; }
        public double SpVertBackMm { get; set; }
        public double QtyVertBack { get; set; }
        public bool AutoVertBackHeight { get; set; }
        public double VertBackHeightMm { get; set; }
        public double VertBackLongOffsetMm { get; set; }
        public double AnchorBackMm { get; set; }

        public RebarBarType BarHoriz { get; set; }
        public int HorizPosRelToVert { get; set; }
        public double CvHorizXMm { get; set; }
        public double CvHorizYMm { get; set; }
        public double CvHorizZMm { get; set; }
        public int LayoutHoriz { get; set; }
        public double SpHorizMm { get; set; }
        public double QtyHoriz { get; set; }

        public RebarBarType BarTie { get; set; }
        public double CvTieYMm { get; set; }
        public int LayoutTieV { get; set; }
        public double SpTieVMm { get; set; }
        public double QtyTieV { get; set; }
        public int LayoutTieH { get; set; }
        public double SpTieHMm { get; set; }
        public double QtyTieH { get; set; }
        public string TieShapeName { get; set; }
        public double TieZMm { get; set; }
        public int TieHookDirection { get; set; }
        public double TieDropMm { get; set; }
    }
}
