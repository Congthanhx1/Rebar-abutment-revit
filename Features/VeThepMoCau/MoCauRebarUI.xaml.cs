using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace Vetheprevit.MoCau
{
    public enum UIAction { None, PickAbutment, DrawRebar, Cancel }


    public partial class MoCauRebarUI : Window
    {
        private void ChkDrawBotLong_CheckedChanged(object sender, RoutedEventArgs e) { if (gridBotLong != null) gridBotLong.IsEnabled = (chkDrawBotLong.IsChecked == true); }
        private void ChkDrawBotTrans_CheckedChanged(object sender, RoutedEventArgs e) { if (gridBotTrans != null) gridBotTrans.IsEnabled = (chkDrawBotTrans.IsChecked == true); }
        private void ChkDrawTopLong_CheckedChanged(object sender, RoutedEventArgs e) { if (gridTopLong != null) gridTopLong.IsEnabled = (chkDrawTopLong.IsChecked == true); }
        private void ChkDrawTopTrans_CheckedChanged(object sender, RoutedEventArgs e) { if (gridTopTrans != null) gridTopTrans.IsEnabled = (chkDrawTopTrans.IsChecked == true); }
        private const string AutoRebarShapeText = "Tự động (Shape mặc định của tool)";

        private sealed class RebarTypeOption
        {
            public RebarBarType RebarType { get; set; }
            public string RevitName { get; set; }
            public string DisplayName { get; set; }
        }

        private Document _doc;
        private AbutmentGeoInfo _geoInfo;
        private readonly bool _isStandalonePreview;
        public UIAction UserAction { get; private set; } = UIAction.Cancel;
        public Func<MoCauRebarUI, Task> OnDrawRebar;
        public Func<MoCauRebarUI, Task> OnPickAbutment;
        public Func<MoCauRebarUI, Task> OnPickHorizSideTopRebar;
        public Action<MoCauRebarUI> OnSaveSettings;
        public Action<MoCauRebarUI> OnExportFootingExcel;
        public string ExportFootingExcelPath { get; private set; }
        public ISet<string> ExportFootingGroupCodes { get; private set; }

        public Dictionary<string, string> RebarNamesConfig { get; set; } = new Dictionary<string, string>
        {
            { "VT_BotLong", "B1" }, { "VT_BotTrans", "B2" },
            { "VT_TopLong", "B3" }, { "VT_TopTrans", "B4" },
            { "VT_VertSideX", "B5" }, { "VT_VertSideY", "B6" },
            { "VT_HorizSide", "B7" }, { "VT_Dowel", "B8" },
            { "VT_AntiBurstX", "B9" }, { "VT_AntiBurstY", "B10" },
            { "VT_StemVertFront", "T1" }, { "VT_StemVertBack", "T2" },
            { "VT_StemHoriz", "T3" }, { "VT_StemTie", "T4" }
        };
        
        private FamilyInstance _selectedAbutment;
        public FamilyInstance SelectedAbutment 
        { 
            get { return _selectedAbutment; }
            set 
            { 
                _selectedAbutment = value;
                if (txtSelectedAbutment != null)
                {
                    if (_selectedAbutment != null)
                        txtSelectedAbutment.Text = $"Đang chọn: {_selectedAbutment.Name} (ID: {_selectedAbutment.Id.Value})";
                    else
                        txtSelectedAbutment.Text = "Đang chọn: (Chưa có)";
                }
            }
        }
        
        // Properties for Rebar Types
        public int SelectedTabIndex => tabMain != null ? tabMain.SelectedIndex : 0;

        public RebarBarType RebarBotLong { get; private set; }
        public RebarBarType RebarBotTrans { get; private set; }
        public RebarBarType RebarTopLong { get; private set; }
        public RebarBarType RebarTopTrans { get; private set; }
        public RebarBarType RebarSideX { get; private set; }
        public RebarBarType RebarSideY { get; private set; }
        public RebarBarType RebarDowel { get; private set; }
        public RebarBarType RebarAntiBurstX { get; private set; }
        public RebarBarType RebarAntiBurstY { get; private set; }

        // Properties for Spacing and Cover
        public double CoverBotZLongMm { get; private set; }
        public double CoverBotZTransMm { get; private set; }
        public double CoverBotLongXMm { get; private set; }
        public double CoverBotLongYMm { get; private set; }
        public double CoverBotTransXMm { get; private set; }
        public double CoverBotTransYMm { get; private set; }
        public bool DrawBotLong { get; private set; }
        public bool DrawBotTrans { get; private set; }
        public double BotMeshAnchorLongMm { get; private set; } = 500;
        public double BotMeshAnchorTransMm { get; private set; } = 500;
        public double SpaceLongBotMm { get; private set; }
        public double SpaceTransBotMm { get; private set; }

        public double CoverTopZLongMm { get; private set; }
        public double CoverTopZTransMm { get; private set; }
        public double CoverTopLongXMm { get; private set; }
        public double CoverTopLongYMm { get; private set; }
        public double CoverTopTransXMm { get; private set; }
        public double CoverTopTransYMm { get; private set; }
        public bool DrawTopLong { get; private set; }
        public bool DrawTopTrans { get; private set; }
        public double TopMeshAnchorLongMm { get; private set; } = 500;
        public double TopMeshAnchorTransMm { get; private set; } = 500;
        public double SpaceLongTopMm { get; private set; }
        public double SpaceTransTopMm { get; private set; }
        
        public int LayoutDowel { get; private set; }
        public double SpaceDowelMm { get; private set; }
        public double QtyDowelMm { get; private set; }
        public bool IsAutoDowelHeight { get; private set; }
        public double DowelHeightMm { get; private set; }
        public double DowelLongOffsetMm { get; private set; }
        public double DowelAnchorMm { get; private set; } = 200;

        public bool DrawBotRebar { get; private set; }
        public bool DrawTopRebar { get; private set; }
        public bool DrawVertSideRebar { get; private set; }
        public bool DrawSideX { get; private set; }
        public bool DrawSideY { get; private set; }
        public bool DrawDowelRebar { get; private set; }
        public bool DrawHorizSideRebar { get; private set; }
        public bool DrawHorizSideX { get; private set; }
        public bool DrawHorizSideY { get; private set; }
        public int HorizSideTopPos { get; private set; }
        public bool DrawAntiBurstRebar { get; private set; }

        public bool DrawAntiBurstX { get; private set; }
        public bool DrawAntiBurstY { get; private set; }
        public double CoverAntiBurstXMm { get; private set; }
        public double CoverAntiBurstYMm { get; private set; }
        public double CoverAntiBurstZMm { get; private set; }
        public bool IsAutoAntiBurstHeight { get; private set; }
        public double AntiBurstHeightMm { get; private set; }
        public double AntiBurstAnchorMm { get; private set; }
        public string SpacingSequenceAntiBurstX { get; private set; }
        public string SpacingSequenceAntiBurstY { get; private set; }

        // --- STEM REBAR PROPERTIES ---
        public bool DrawStemVertFront { get; private set; }
        public bool DrawStemVertBack { get; private set; }
        public bool DrawStemHoriz { get; private set; }
        public bool DrawStemTie { get; private set; }

        public bool UseDowelAsStemVert { get; private set; }
        public RebarBarType RebarStemVertFront { get; private set; }
        public double CoverStemVertFrontMm { get; private set; }
        public double CoverStemVertFrontZMm { get; private set; }
        public int LayoutStemVertFront { get; private set; }
        public double SpaceStemVertFrontMm { get; private set; }
        public double QtyStemVertFront { get; private set; }
        public bool IsAutoStemVertFrontHeight { get; private set; }
        public double StemVertFrontHeightMm { get; private set; }
        public double StemVertFrontLongOffsetMm { get; private set; }
        public double StemVertFrontAnchorMm { get; private set; } = 200;

        public RebarBarType RebarStemVertBack { get; private set; }
        public double CoverStemVertBackMm { get; private set; }
        public double CoverStemVertBackZMm { get; private set; }
        public int LayoutStemVertBack { get; private set; }
        public double SpaceStemVertBackMm { get; private set; }
        public double QtyStemVertBack { get; private set; }
        public bool IsAutoStemVertBackHeight { get; private set; }
        public double StemVertBackHeightMm { get; private set; }
        public double StemVertBackLongOffsetMm { get; private set; }
        public double StemVertBackAnchorMm { get; private set; } = 200;

        public RebarBarType RebarStemHoriz { get; private set; }
        public int HorizPosRelToVert { get; private set; }
        public double CoverStemHorizXMm { get; private set; }
        public double CoverStemHorizYMm { get; private set; }
        public double CoverStemHorizZMm { get; private set; }
        public int LayoutStemHoriz { get; private set; }
        public double SpaceStemHorizMm { get; private set; }
        public double QtyStemHoriz { get; private set; }

        public RebarBarType RebarStemTie { get; private set; }
        public double CoverStemTieYMm { get; private set; }
        public int LayoutStemTieV { get; private set; }
        public double SpaceStemTieVMm { get; private set; }
        public double QtyStemTieV { get; private set; }
        public int LayoutStemTieH { get; private set; }
        public double SpaceStemTieHMm { get; private set; }
        public double QtyStemTieH { get; private set; }
        public string StemTieShapeName { get; private set; } = "M_01";
        public double StemTieZMm { get; private set; } = 50;
        public double StemTieDropMm { get; private set; } = 0;
        public int TieHookDirection { get; private set; } = 0;
        
        public double SpaceVertSideXMm { get; private set; }
        public double SpaceVertSideYMm { get; private set; }
        public double CoverVertSideMm { get; private set; }
        public double CoverVertSideZBotMm { get; private set; }
        public double CoverVertSideZTopMm { get; private set; }
        public double OffsetSideXMm { get; private set; }
        public double OffsetSideYMm { get; private set; }
        public bool IsAutoSideHeight { get; private set; }
        public double SideHeightMm { get; private set; }
        public double VertSideAnchorMm { get; private set; }

        public RebarBarType RebarHorizSideX { get; private set; }
        public RebarBarType RebarHorizSideY { get; private set; }
        public double SpaceHorizSideXMm { get; private set; }
        public double SpaceHorizSideYMm { get; private set; }
        public double CoverHorizSideXMm { get; private set; }
        public double CoverHorizSideYMm { get; private set; }
        public double CoverBotHorizSideMm { get; private set; }
        public double HorizAnchorMm { get; private set; }

        public double QtyLongBotMm { get; private set; }
        public double QtyTransBotMm { get; private set; }
        public double QtyLongTopMm { get; private set; }
        public double QtyTransTopMm { get; private set; }
        public double QtyVertSideXMm { get; private set; }
        public double QtyVertSideYMm { get; private set; }
        public double QtyHorizSideXMm { get; private set; }
        public double QtyHorizSideYMm { get; private set; }

        public int LayoutLBot { get; private set; }
        public int LayoutTBot { get; private set; }
        public int LayoutLTop { get; private set; }
        public int LayoutTTop { get; private set; }
        public int LayoutVSideX { get; private set; }
        public int LayoutVSideY { get; private set; }
        public int LayoutHorizSideX { get; private set; }
        public int LayoutHorizSideY { get; private set; }

        public MoCauRebarUI(Document doc)
        {
            InitializeComponent();
            Width = 1060;
            MinWidth = 960;
            MinHeight = 640;
            _doc = doc;
            LoadRebarTypes();
            AttachEvents();
            AttachPreviewEvents();
            chkAutoAntiBurstHeight_CheckedChanged(null, null);
            chkAutoSideHeight_CheckedChanged(null, null);
        }

        /// <summary>
        /// Opens the real tool window with sample data, without requiring an
        /// active Revit document. This constructor is only used by the
        /// companion Preview application.
        /// </summary>
        public MoCauRebarUI(bool standalonePreview)
        {
            _isStandalonePreview = standalonePreview;
            InitializeComponent();
            Width = 1060;
            MinWidth = 960;
            MinHeight = 640;

            LoadPreviewChoices();
            AttachEvents();
            AttachPreviewEvents();
            LoadStandalonePreviewGeometry();
            ConfigureStandalonePreviewActions();
            chkAutoAntiBurstHeight_CheckedChanged(null, null);
            chkAutoSideHeight_CheckedChanged(null, null);
        }

        private void LoadPreviewChoices()
        {
            string[] rebarTypes =
            {
                "D10", "D12", "D14", "D16", "D18", "D20", "D22", "D25", "D28", "D32"
            };

            ComboBox[] rebarCombos =
            {
                cboRebarBotLong, cboRebarBotTrans,
                cboRebarTopLong, cboRebarTopTrans,
                cboRebarSideX, cboRebarSideY,
                cboRebarDowel, cboRebarAntiBurstX, cboRebarAntiBurstY,
                cboRebarHorizSideX, cboRebarHorizSideY,
                cboRebarStemVertFront, cboRebarStemVertBack,
                cboRebarStemHoriz, cboRebarStemTie
            };

            foreach (ComboBox comboBox in rebarCombos)
            {
                if (comboBox == null) continue;
                comboBox.DisplayMemberPath = "";
                comboBox.ItemsSource = rebarTypes;
                comboBox.SelectedItem = "D16";
            }

            string[] shapes = { "M_01", "M_02", "M_17", "M_21" };
            if (cboTieShape != null)
            {
                cboTieShape.ItemsSource = shapes;
                cboTieShape.SelectedItem = "M_02";
            }

            ComboBox[] footingShapeCombos =
            {
                cboShapeBot, cboShapeTop, cboShapeVertSide,
                cboShapeAntiBurst, cboShapeDowel, cboShapeHorizSide
            };

            foreach (ComboBox comboBox in footingShapeCombos)
            {
                if (comboBox == null) continue;
                comboBox.ItemsSource = shapes;
                comboBox.SelectedIndex = -1;
                comboBox.Text = AutoRebarShapeText;
            }

            if (cboShapeBot != null) cboShapeBot.SelectedItem = "M_02";
            if (cboShapeTop != null) cboShapeTop.SelectedItem = "M_02";
        }

        private void LoadStandalonePreviewGeometry()
        {
            static double Ft(double millimeters) => millimeters / 304.8;

            _geoInfo = new AbutmentGeoInfo
            {
                SourceSolidCount = 4,
                MinZ = Ft(0),
                MinUpZ = Ft(2000),
                MaxZ = Ft(10000),
                StemMaxZ = Ft(10000),
                FootMinL = Ft(0),
                FootMaxL = Ft(8000),
                FootMinT = Ft(0),
                FootMaxT = Ft(5000),
                StemMinL = Ft(400),
                StemMaxL = Ft(7600),
                StemMinT = Ft(1800),
                StemMaxT = Ft(3200),
                StemSideProfile = new List<AbutmentProfilePoint>
                {
                    new AbutmentProfilePoint(Ft(1800), Ft(2000)),
                    new AbutmentProfilePoint(Ft(1800), Ft(10000)),
                    new AbutmentProfilePoint(Ft(2300), Ft(10000)),
                    new AbutmentProfilePoint(Ft(2300), Ft(8600)),
                    new AbutmentProfilePoint(Ft(3200), Ft(8200)),
                    new AbutmentProfilePoint(Ft(3200), Ft(2000))
                }
            };

            if (txtSelectedAbutment != null)
            txtSelectedAbutment.Text = "Dữ liệu mẫu: Mố cầu 8000 × 5000 × 10000 mm";
            if (txtStatus != null)
            {
            txtStatus.Text = "PREVIEW độc lập — không ghi dữ liệu vào Revit";
                txtStatus.Foreground = Brushes.DarkOrange;
            }
        }

        private void ConfigureStandalonePreviewActions()
        {
            Title += " [PREVIEW]";

            if (BtnPick != null)
            {
                BtnPick.Content = "DỮ LIỆU HÌNH HỌC MẪU";
                BtnPick.IsEnabled = false;
            }

            if (BtnDrawRebar != null)
            {
                BtnDrawRebar.Content = "CHẾ ĐỘ PREVIEW";
                BtnDrawRebar.IsEnabled = false;
            }

            if (BtnExportExcel != null)
                BtnExportExcel.IsEnabled = false;
            if (BtnSaveSettings != null)
                BtnSaveSettings.IsEnabled = false;
        }

        private void LoadRebarTypes()
        {
            var rebarTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(r => r.Name)
                .ToList();
            List<RebarTypeOption> rebarTypeOptions = rebarTypes
                .Select(rebarType => new RebarTypeOption
                {
                    RebarType = rebarType,
                    RevitName = rebarType.Name,
                    DisplayName = GetRebarDisplayName(rebarType.Name)
                })
                .ToList();

            cboRebarBotLong.ItemsSource = rebarTypeOptions;
            cboRebarBotTrans.ItemsSource = rebarTypeOptions;
            cboRebarTopLong.ItemsSource = rebarTypeOptions;
            cboRebarTopTrans.ItemsSource = rebarTypeOptions;
            cboRebarSideX.ItemsSource = rebarTypeOptions;
            cboRebarSideY.ItemsSource = rebarTypeOptions;
            cboRebarDowel.ItemsSource = rebarTypeOptions;
            if (cboRebarAntiBurstX != null) cboRebarAntiBurstX.ItemsSource = rebarTypeOptions;
            if (cboRebarAntiBurstY != null) cboRebarAntiBurstY.ItemsSource = rebarTypeOptions;
            if (cboRebarHorizSideX != null) cboRebarHorizSideX.ItemsSource = rebarTypeOptions;
            if (cboRebarHorizSideY != null) cboRebarHorizSideY.ItemsSource = rebarTypeOptions;

            if (cboRebarStemVertFront != null) cboRebarStemVertFront.ItemsSource = rebarTypeOptions;
            if (cboRebarStemVertBack != null) cboRebarStemVertBack.ItemsSource = rebarTypeOptions;
            if (cboRebarStemHoriz != null) cboRebarStemHoriz.ItemsSource = rebarTypeOptions;
            if (cboRebarStemTie != null) cboRebarStemTie.ItemsSource = rebarTypeOptions;

            if (rebarTypes.Count > 0)
            {
                cboRebarBotLong.SelectedIndex = 0;
                cboRebarBotTrans.SelectedIndex = 0;
                cboRebarTopLong.SelectedIndex = 0;
                cboRebarTopTrans.SelectedIndex = 0;
                cboRebarSideX.SelectedIndex = 0;
                cboRebarSideY.SelectedIndex = 0;
                cboRebarDowel.SelectedIndex = 0;
                if (cboRebarAntiBurstX != null) cboRebarAntiBurstX.SelectedIndex = 0;
                if (cboRebarAntiBurstY != null) cboRebarAntiBurstY.SelectedIndex = 0;
                if (cboRebarHorizSideX != null) cboRebarHorizSideX.SelectedIndex = 0;
                if (cboRebarHorizSideY != null) cboRebarHorizSideY.SelectedIndex = 0;

                if (cboRebarStemVertFront != null) cboRebarStemVertFront.SelectedIndex = 0;
                if (cboRebarStemVertBack != null) cboRebarStemVertBack.SelectedIndex = 0;
                if (cboRebarStemHoriz != null) cboRebarStemHoriz.SelectedIndex = 0;
                if (cboRebarStemTie != null) cboRebarStemTie.SelectedIndex = 0;
            }

            var rebarShapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(r => r.Name)
                .ToList();

            if (cboTieShape != null)
            {
                cboTieShape.ItemsSource = rebarShapes;
                if (rebarShapes.Count > 0)
                {
                    var m01 = rebarShapes.FirstOrDefault(s => s.Name == "M_01");
                    if (m01 != null) cboTieShape.SelectedItem = m01;
                    else cboTieShape.SelectedIndex = 0;
                }
            }

            ComboBox[] footingShapeCombos =
            {
                cboShapeBot,
                cboShapeTop,
                cboShapeVertSide,
                cboShapeAntiBurst,
                cboShapeDowel,
                cboShapeHorizSide
            };
            foreach (ComboBox comboBox in footingShapeCombos)
            {
                if (comboBox == null) continue;

                comboBox.ItemsSource = rebarShapes;
                comboBox.SelectedIndex = -1;
                comboBox.Text = AutoRebarShapeText;
            }

            SelectMeshShapeM02(cboShapeBot, rebarShapes);
            SelectMeshShapeM02(cboShapeTop, rebarShapes);
        }

        private static void SelectMeshShapeM02(
            ComboBox comboBox,
            IEnumerable<RebarShape> rebarShapes)
        {
            if (comboBox == null) return;

            RebarShape m02 = rebarShapes?.FirstOrDefault(
                shape => string.Equals(
                    shape.Name?.Replace("_", ""),
                    "M02",
                    StringComparison.OrdinalIgnoreCase));
            if (m02 != null)
            {
                comboBox.SelectedItem = m02;
            }
            else
            {
                comboBox.SelectedIndex = -1;
                comboBox.Text = "M_02";
            }
        }

        private void chkUseDowelAsStemVert_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (spStemVert != null && chkUseDowelAsStemVert != null)
            {
                spStemVert.Visibility = chkUseDowelAsStemVert.IsChecked == true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }

        private void chkAutoStemVertFrontHeight_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (txtStemVertFrontHeight != null && chkAutoStemVertFrontHeight != null)
            {
                txtStemVertFrontHeight.IsEnabled = chkAutoStemVertFrontHeight.IsChecked == false;
            }
        }

        private void chkAutoStemVertBackHeight_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (txtStemVertBackHeight != null && chkAutoStemVertBackHeight != null)
            {
                txtStemVertBackHeight.IsEnabled = chkAutoStemVertBackHeight.IsChecked == false;
            }
        }

        private void cboHorizPosRelToVert_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtCoverStemHorizX != null && cboHorizPosRelToVert != null)
            {
                if (cboHorizPosRelToVert.SelectedIndex == 0) // TRONG
                {
                    double cvVF = 50;
                    double.TryParse(txtCoverStemVertFront?.Text ?? "50", out cvVF);
                    double dVF = GetRebarTypeFromCombo(cboRebarStemVertFront)?.BarModelDiameter * 304.8 ?? 10;
                    double dH = GetRebarTypeFromCombo(cboRebarStemHoriz)?.BarModelDiameter * 304.8 ?? 10;
                    txtCoverStemHorizX.Text = Math.Round(cvVF + dVF + dH, 1).ToString();
                    txtCoverStemHorizX.IsEnabled = false;
                }
            else if (cboHorizPosRelToVert.SelectedIndex == 1) // NGOÀI
                {
                    double cvVF = 50;
                    double.TryParse(txtCoverStemVertFront?.Text ?? "50", out cvVF);
                    double dH = GetRebarTypeFromCombo(cboRebarStemHoriz)?.BarModelDiameter * 304.8 ?? 10;
                    txtCoverStemHorizX.Text = Math.Round(Math.Max(0, cvVF - dH), 1).ToString();
                    txtCoverStemHorizX.IsEnabled = false;
                }
                else
                {
                    txtCoverStemHorizX.IsEnabled = true;
                }
            }
        }

        private void Cbo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        public void RestoreState(MoCauRebarUI previousUI)
        {
            if (previousUI == null) return;
            var dict = previousUI.GetSettingsToDictionary();
            this.ApplySettingsFromDictionary(dict);
            this.SelectedAbutment = previousUI.SelectedAbutment;
        }

        public IDictionary<string, string> GetSettingsToDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (SelectedAbutment != null) dict["AbutmentId"] = SelectedAbutment.Id.Value.ToString();

            foreach (var entry in GetRebarNamesByGroup())
                dict["RebarName_" + entry.Key] = entry.Value;
            foreach (var entry in GetRebarShapesByGroup())
                dict["RebarShape_" + entry.Key] = entry.Value;

            dict["RebarBotLongName"] = cboRebarBotLong.Text;
            dict["RebarBotTransName"] = cboRebarBotTrans.Text;
            dict["CoverBotZLong"] = txtCoverBotZLong.Text;
            dict["CoverBotZTrans"] = txtCoverBotZTrans.Text;
            dict["CoverBotLongX"] = txtCoverBotLongX.Text;
            dict["CoverBotLongY"] = txtCoverBotLongY.Text;
            dict["CoverBotTransX"] = txtCoverBotTransX.Text;
            dict["CoverBotTransY"] = txtCoverBotTransY.Text;
            dict["DrawBotLong"] = chkDrawBotLong.IsChecked.ToString();
            dict["DrawBotTrans"] = chkDrawBotTrans.IsChecked.ToString();
            dict["BotMeshAnchorLong"] = txtBotMeshAnchorLong.Text;
            dict["BotMeshAnchorTrans"] = txtBotMeshAnchorTrans.Text;
            dict["SpaceLongBot"] = txtSpaceLongBot.Text;
            dict["SpaceTransBot"] = txtSpaceTransBot.Text;
            dict["LayoutLBot"] = cboLayoutLBot.SelectedIndex.ToString();
            dict["QtyLongBot"] = txtQtyLongBot.Text;
            dict["LayoutTBot"] = cboLayoutTBot.SelectedIndex.ToString();
            dict["QtyTransBot"] = txtQtyTransBot.Text;

            dict["SameTop"] = chkSameTop.IsChecked.ToString();
            dict["RebarTopLongName"] = cboRebarTopLong.Text;
            dict["RebarTopTransName"] = cboRebarTopTrans.Text;
            dict["CoverTopZLong"] = txtCoverTopZLong.Text;
            dict["CoverTopZTrans"] = txtCoverTopZTrans.Text;
            dict["CoverTopLongX"] = txtCoverTopLongX.Text;
            dict["CoverTopLongY"] = txtCoverTopLongY.Text;
            dict["CoverTopTransX"] = txtCoverTopTransX.Text;
            dict["CoverTopTransY"] = txtCoverTopTransY.Text;
            dict["DrawTopLong"] = chkDrawTopLong.IsChecked.ToString();
            dict["DrawTopTrans"] = chkDrawTopTrans.IsChecked.ToString();
            dict["TopMeshAnchorLong"] = txtTopMeshAnchorLong.Text;
            dict["TopMeshAnchorTrans"] = txtTopMeshAnchorTrans.Text;
            dict["SpaceLongTop"] = txtSpaceLongTop.Text;
            dict["SpaceTransTop"] = txtSpaceTransTop.Text;
            dict["LayoutLTop"] = cboLayoutLTop.SelectedIndex.ToString();
            dict["QtyLongTop"] = txtQtyLongTop.Text;
            dict["LayoutTTop"] = cboLayoutTTop.SelectedIndex.ToString();
            dict["QtyTransTop"] = txtQtyTransTop.Text;

            dict["RebarSideXName"] = cboRebarSideX.Text;
            if (txtCoverVertSide != null) dict["CoverVertSide"] = txtCoverVertSide.Text;
            if (txtCoverVertSideZBot != null) dict["CoverVertSideZBot"] = txtCoverVertSideZBot.Text;
            if (txtCoverVertSideZTop != null) dict["CoverVertSideZTop"] = txtCoverVertSideZTop.Text;
            dict["SpaceVertSideX"] = txtSpaceVertSideX.Text;
            dict["OffsetSideX"] = txtOffsetSideX.Text;
            dict["LayoutVSideX"] = cboLayoutVSideX.SelectedIndex.ToString();
            dict["QtyVertSideX"] = txtQtyVertSideX.Text;
            
            dict["RebarSideYName"] = cboRebarSideY.Text;
            dict["SpaceVertSideY"] = txtSpaceVertSideY.Text;
            dict["OffsetSideY"] = txtOffsetSideY.Text;
            dict["LayoutVSideY"] = cboLayoutVSideY.SelectedIndex.ToString();
            dict["QtyVertSideY"] = txtQtyVertSideY.Text;
            dict["AutoSideHeight"] = chkAutoSideHeight.IsChecked.ToString();
            dict["SideHeight"] = txtSideHeight.Text;
            dict["VertSideAnchor"] = txtVertSideAnchor.Text;

            dict["RebarDowelName"] = cboRebarDowel.Text;
            dict["SpaceDowel"] = txtSpaceDowel.Text;
            dict["LayoutDowel"] = cboLayoutDowel.SelectedIndex.ToString();
            dict["QtyDowel"] = txtQtyDowel.Text;
            if (chkAutoDowelHeight != null) dict["AutoDowelHeight"] = chkAutoDowelHeight.IsChecked.ToString();
            dict["DowelHeight"] = txtDowelHeight.Text;
            if (txtDowelLongOffset != null) dict["DowelLongOffset"] = txtDowelLongOffset.Text;
            if (txtDowelAnchor != null) dict["DowelAnchor"] = txtDowelAnchor.Text;

            if (cboRebarAntiBurstX != null) dict["RebarAntiBurstXName"] = cboRebarAntiBurstX.Text;
            if (cboRebarAntiBurstY != null) dict["RebarAntiBurstYName"] = cboRebarAntiBurstY.Text;
            if (txtCoverAntiBurstX != null) dict["CoverAntiBurstX"] = txtCoverAntiBurstX.Text;
            if (txtCoverAntiBurstY != null) dict["CoverAntiBurstY"] = txtCoverAntiBurstY.Text;
            if (txtCoverAntiBurstZ != null) dict["CoverAntiBurstZ"] = txtCoverAntiBurstZ.Text;
            if (chkAutoAntiBurstHeight != null) dict["AutoAntiBurstHeight"] = (chkAutoAntiBurstHeight.IsChecked ?? true).ToString();
            if (txtAntiBurstHeight != null) dict["AntiBurstHeight"] = txtAntiBurstHeight.Text;
            if (txtAntiBurstAnchor != null) dict["AntiBurstAnchor"] = txtAntiBurstAnchor.Text;
            if (chkDrawAntiBurstX != null) dict["DrawAntiBurstX"] = (chkDrawAntiBurstX.IsChecked ?? true).ToString();
            if (chkDrawAntiBurstY != null) dict["DrawAntiBurstY"] = (chkDrawAntiBurstY.IsChecked ?? true).ToString();
            if (txtSpacingSequenceAntiBurstX != null) dict["SpacingSequenceAntiBurstX"] = txtSpacingSequenceAntiBurstX.Text;
            if (txtSpacingSequenceAntiBurstY != null) dict["SpacingSequenceAntiBurstY"] = txtSpacingSequenceAntiBurstY.Text;

            // Stem
            if (chkUseDowelAsStemVert != null) dict["UseDowelAsStemVert"] = (chkUseDowelAsStemVert.IsChecked == true).ToString();
            if (cboRebarStemVertFront != null) dict["RebarStemVertFrontName"] = cboRebarStemVertFront.Text;
            if (txtCoverStemVertFront != null) dict["CoverStemVertFront"] = txtCoverStemVertFront.Text;
            if (txtCoverStemVertFrontZ != null) dict["CoverStemVertFrontZ"] = txtCoverStemVertFrontZ.Text;
            if (cboLayoutStemVertFront != null) dict["LayoutStemVertFront"] = cboLayoutStemVertFront.SelectedIndex.ToString();
            if (txtSpaceStemVertFront != null) dict["SpaceStemVertFront"] = txtSpaceStemVertFront.Text;
            if (txtQtyStemVertFront != null) dict["QtyStemVertFront"] = txtQtyStemVertFront.Text;
            if (chkAutoStemVertFrontHeight != null) dict["AutoStemVertFrontHeight"] = (chkAutoStemVertFrontHeight.IsChecked == true).ToString();
            if (txtStemVertFrontHeight != null) dict["StemVertFrontHeight"] = txtStemVertFrontHeight.Text;
            if (txtStemVertFrontLongOffset != null) dict["StemVertFrontLongOffset"] = txtStemVertFrontLongOffset.Text;
            if (txtStemVertFrontAnchor != null) dict["StemVertFrontAnchor"] = txtStemVertFrontAnchor.Text;

            if (cboRebarStemVertBack != null) dict["RebarStemVertBackName"] = cboRebarStemVertBack.Text;
            if (txtCoverStemVertBack != null) dict["CoverStemVertBack"] = txtCoverStemVertBack.Text;
            if (txtCoverStemVertBackZ != null) dict["CoverStemVertBackZ"] = txtCoverStemVertBackZ.Text;
            if (cboLayoutStemVertBack != null) dict["LayoutStemVertBack"] = cboLayoutStemVertBack.SelectedIndex.ToString();
            if (txtSpaceStemVertBack != null) dict["SpaceStemVertBack"] = txtSpaceStemVertBack.Text;
            if (txtQtyStemVertBack != null) dict["QtyStemVertBack"] = txtQtyStemVertBack.Text;
            if (chkAutoStemVertBackHeight != null) dict["AutoStemVertBackHeight"] = (chkAutoStemVertBackHeight.IsChecked == true).ToString();
            if (txtStemVertBackHeight != null) dict["StemVertBackHeight"] = txtStemVertBackHeight.Text;
            if (txtStemVertBackLongOffset != null) dict["StemVertBackLongOffset"] = txtStemVertBackLongOffset.Text;
            if (txtStemVertBackAnchor != null) dict["StemVertBackAnchor"] = txtStemVertBackAnchor.Text;

            if (cboRebarStemHoriz != null) dict["RebarStemHorizName"] = cboRebarStemHoriz.Text;
            if (cboHorizPosRelToVert != null) dict["HorizPosRelToVert"] = cboHorizPosRelToVert.SelectedIndex.ToString();
            if (txtCoverStemHorizX != null) dict["CoverStemHorizX"] = txtCoverStemHorizX.Text;
            if (txtCoverStemHorizY != null) dict["CoverStemHorizY"] = txtCoverStemHorizY.Text;
            if (txtCoverStemHorizZ != null) dict["CoverStemHorizZ"] = txtCoverStemHorizZ.Text;
            if (cboLayoutStemHoriz != null) dict["LayoutStemHoriz"] = cboLayoutStemHoriz.SelectedIndex.ToString();
            if (txtSpaceStemHoriz != null) dict["SpaceStemHoriz"] = txtSpaceStemHoriz.Text;
            if (txtQtyStemHoriz != null) dict["QtyStemHoriz"] = txtQtyStemHoriz.Text;
            if (txtQtyHorizSideX != null) dict["QtyHorizSideX"] = txtQtyHorizSideX.Text;
            if (txtQtyHorizSideY != null) dict["QtyHorizSideY"] = txtQtyHorizSideY.Text;
            if (txtHorizAnchor != null) dict["HorizAnchor"] = txtHorizAnchor.Text;
            if (cboHorizSideTopPos != null) dict["HorizSideTopPos"] = cboHorizSideTopPos.SelectedIndex.ToString();

            if (cboRebarStemTie != null) dict["RebarStemTieName"] = cboRebarStemTie.Text;
            if (txtCoverStemTieY != null) dict["CoverStemTieY"] = txtCoverStemTieY.Text;
            if (chkDrawStemTie != null) dict["DrawStemTie"] = (chkDrawStemTie.IsChecked ?? true).ToString();
            
            if (chkDrawBotRebar != null) dict["DrawBotRebar"] = (chkDrawBotRebar.IsChecked ?? true).ToString();
            if (chkDrawTopRebar != null) dict["DrawTopRebar"] = (chkDrawTopRebar.IsChecked ?? true).ToString();
            if (chkDrawVertSideRebar != null) dict["DrawVertSideRebar"] = (chkDrawVertSideRebar.IsChecked ?? true).ToString();
            if (chkDrawSideX != null) dict["DrawSideX"] = (chkDrawSideX.IsChecked ?? true).ToString();
            if (chkDrawSideY != null) dict["DrawSideY"] = (chkDrawSideY.IsChecked ?? true).ToString();
            if (chkDrawDowelRebar != null) dict["DrawDowelRebar"] = (chkDrawDowelRebar.IsChecked ?? true).ToString();
            if (chkDrawHorizSideRebar != null) dict["DrawHorizSideRebar"] = (chkDrawHorizSideRebar.IsChecked ?? true).ToString();
            if (chkDrawHorizSideX != null) dict["DrawHorizSideX"] = (chkDrawHorizSideX.IsChecked ?? true).ToString();
            if (chkDrawHorizSideY != null) dict["DrawHorizSideY"] = (chkDrawHorizSideY.IsChecked ?? true).ToString();
            if (chkDrawAntiBurstRebar != null) dict["DrawAntiBurstRebar"] = (chkDrawAntiBurstRebar.IsChecked ?? true).ToString();
            
            if (chkDrawStemVertFront != null) dict["DrawStemVertFront"] = (chkDrawStemVertFront.IsChecked ?? true).ToString();
            if (chkDrawStemVertBack != null) dict["DrawStemVertBack"] = (chkDrawStemVertBack.IsChecked ?? true).ToString();
            if (chkDrawStemHoriz != null) dict["DrawStemHoriz"] = (chkDrawStemHoriz.IsChecked ?? true).ToString();
            
            if (cboLayoutStemTieV != null) dict["LayoutStemTieV"] = cboLayoutStemTieV.SelectedIndex.ToString();
            if (txtSpaceStemTieV != null) dict["SpaceStemTieV"] = txtSpaceStemTieV.Text;
            if (txtQtyStemTieV != null) dict["QtyStemTieV"] = txtQtyStemTieV.Text;
            if (cboLayoutStemTieH != null) dict["LayoutStemTieH"] = cboLayoutStemTieH.SelectedIndex.ToString();
            if (txtSpaceStemTieH != null) dict["SpaceStemTieH"] = txtSpaceStemTieH.Text;
            if (txtQtyStemTieH != null) dict["QtyStemTieH"] = txtQtyStemTieH.Text;
            if (cboTieShape != null && cboTieShape.SelectedItem is RebarShape rbShape) dict["StemTieShapeName"] = rbShape.Name;
            if (txtStemTieZ != null) dict["StemTieZ"] = txtStemTieZ.Text;
            if (txtStemTieDrop != null) dict["StemTieDrop"] = txtStemTieDrop.Text;
            if (cboTieHookDirection != null) dict["TieHookDirection"] = cboTieHookDirection.SelectedIndex.ToString();

            if (cboRebarHorizSideX != null) dict["RebarHorizSideXName"] = cboRebarHorizSideX.Text;
            if (cboRebarHorizSideY != null) dict["RebarHorizSideYName"] = cboRebarHorizSideY.Text;
            if (txtCoverHorizSideX != null) dict["CoverHorizSideX"] = txtCoverHorizSideX.Text;
            if (txtCoverBotHorizSide != null) dict["CoverBotHorizSide"] = txtCoverBotHorizSide.Text;
            if (txtSpaceHorizSideX != null) dict["SpaceHorizSideX"] = txtSpaceHorizSideX.Text;
            if (txtSpaceHorizSideY != null) dict["SpaceHorizSideY"] = txtSpaceHorizSideY.Text;
            if (cboLayoutHorizSideX != null) dict["LayoutHorizSideX"] = cboLayoutHorizSideX.SelectedIndex.ToString();
            if (cboLayoutHorizSideY != null) dict["LayoutHorizSideY"] = cboLayoutHorizSideY.SelectedIndex.ToString();
            if (txtQtyHorizSideX != null) dict["QtyHorizSideX"] = txtQtyHorizSideX.Text;
            if (txtQtyHorizSideY != null) dict["QtyHorizSideY"] = txtQtyHorizSideY.Text;
            if (txtHorizAnchor != null) dict["HorizAnchor"] = txtHorizAnchor.Text;

            return dict;
        }

        public IDictionary<string, string> GetRebarNamesByGroup()
        {
            var dict = new Dictionary<string, string>(RebarNamesConfig);
            
            // Map the unified HorizSide name to the specific X and Y groups as well
            if (dict.TryGetValue("VT_HorizSide", out string horizSideName))
            {
                dict["VT_HorizSideX"] = horizSideName;
                dict["VT_HorizSideY"] = horizSideName;
            }
            
            return dict;
        }

        public IDictionary<string, string> GetRebarShapesByGroup()
        {
            string GetMeshShapeM02Name(ComboBox comboBox)
            {
                if (comboBox?.SelectedItem is RebarShape selectedShape &&
                    string.Equals(
                        selectedShape.Name?.Replace("_", ""),
                        "M02",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return selectedShape.Name;
                }

                string text = comboBox?.Text?.Trim();
                return !string.IsNullOrWhiteSpace(text) &&
                       string.Equals(
                           text.Replace("_", ""),
                           "M02",
                           StringComparison.OrdinalIgnoreCase)
                    ? text
                    : "M_02";
            }

            string GetSelectedShapeName(ComboBox comboBox)
            {
                if (comboBox == null ||
                    string.IsNullOrWhiteSpace(comboBox.Text) ||
                    comboBox.Text == AutoRebarShapeText)
                {
                    return "";
                }

                return comboBox.Text.Trim();
            }

            return new Dictionary<string, string>
            {
                ["VT_BotLong"] = GetMeshShapeM02Name(cboShapeBot),
                ["VT_BotTrans"] = GetMeshShapeM02Name(cboShapeBot),
                ["VT_TopLong"] = GetMeshShapeM02Name(cboShapeTop),
                ["VT_TopTrans"] = GetMeshShapeM02Name(cboShapeTop),
                ["VT_VertSideX"] = GetSelectedShapeName(cboShapeVertSide),
                ["VT_VertSideY"] = GetSelectedShapeName(cboShapeVertSide),
                ["VT_AntiBurstX"] = GetSelectedShapeName(cboShapeAntiBurst),
                ["VT_AntiBurstY"] = GetSelectedShapeName(cboShapeAntiBurst),
                ["VT_Dowel"] = GetSelectedShapeName(cboShapeDowel),
                ["VT_HorizSide"] = GetSelectedShapeName(cboShapeHorizSide),
                ["VT_HorizSideX"] = GetSelectedShapeName(cboShapeHorizSide),
                ["VT_HorizSideY"] = GetSelectedShapeName(cboShapeHorizSide)
            };
        }

        public void ApplySettingsFromDictionary(IDictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return;

            if (_doc != null &&
                dict.TryGetValue("AbutmentId", out string idStr) &&
                long.TryParse(idStr, out long idVal))
            {
                Element elem = _doc.GetElement(new ElementId(idVal));
                if (elem is FamilyInstance fi) SelectedAbutment = fi;
            }

            var keys = RebarNamesConfig.Keys.ToList();
            foreach (var key in keys)
            {
                if (dict.TryGetValue("RebarName_" + key, out string savedName) && !string.IsNullOrEmpty(savedName))
                {
                    RebarNamesConfig[key] = savedName;
                }
            }

            void SetShapeByName(ComboBox comboBox, string groupCode)
            {
                if (comboBox == null)
                {
                    return;
                }

                if (!dict.TryGetValue("RebarShape_" + groupCode, out string shapeName) ||
                    string.IsNullOrWhiteSpace(shapeName))
                {
                    comboBox.SelectedIndex = -1;
                    comboBox.Text = AutoRebarShapeText;
                    return;
                }

                comboBox.Text = shapeName;
                foreach (object item in comboBox.Items)
                {
                    if (item is RebarShape shape && shape.Name == shapeName)
                    {
                        comboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            SetShapeByName(cboShapeBot, "VT_BotLong");
            SetShapeByName(cboShapeTop, "VT_TopLong");
            SetShapeByName(cboShapeVertSide, "VT_VertSideX");
            SetShapeByName(cboShapeAntiBurst, "VT_AntiBurstX");
            SetShapeByName(cboShapeDowel, "VT_Dowel");
            SetShapeByName(cboShapeHorizSide, "VT_HorizSide");

            if (_doc != null)
            {
                List<RebarShape> meshShapes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RebarShape))
                    .Cast<RebarShape>()
                    .ToList();
                SelectMeshShapeM02(cboShapeBot, meshShapes);
                SelectMeshShapeM02(cboShapeTop, meshShapes);
            }
            else
            {
                if (cboShapeBot != null) cboShapeBot.SelectedItem = "M_02";
                if (cboShapeTop != null) cboShapeTop.SelectedItem = "M_02";
            }

            void SetComboByRebarName(ComboBox cbo, string key)
            {
                if (dict.TryGetValue(key, out string name))
                {
                    foreach (var item in cbo.Items)
                    {
                        if (item is RebarTypeOption option &&
                            (option.RevitName.Equals(
                                 name,
                                 StringComparison.OrdinalIgnoreCase) ||
                             option.DisplayName.Equals(
                                 name,
                                 StringComparison.OrdinalIgnoreCase)))
                        {
                            cbo.SelectedItem = item;
                            return;
                        }
                        if (item is RebarBarType rb &&
                            rb.Name.Equals(
                                name,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            cbo.SelectedItem = item;
                            return;
                        }
                    }
                    cbo.Text = GetRebarDisplayName(name);
                }
            }

            SetComboByRebarName(cboRebarBotLong, "RebarBotLongName");
            SetComboByRebarName(cboRebarBotTrans, "RebarBotTransName");
            if (dict.TryGetValue("CoverBotZLong", out string val)) txtCoverBotZLong.Text = val;
            if (dict.TryGetValue("CoverBotZTrans", out val)) txtCoverBotZTrans.Text = val;
            if (dict.TryGetValue("CoverBotLongX", out val)) txtCoverBotLongX.Text = val;
            if (dict.TryGetValue("CoverBotLongY", out val)) txtCoverBotLongY.Text = val;
            if (dict.TryGetValue("CoverBotTransX", out val)) txtCoverBotTransX.Text = val;
            if (dict.TryGetValue("CoverBotTransY", out val)) txtCoverBotTransY.Text = val;
            if (dict.TryGetValue("DrawBotLong", out val)) chkDrawBotLong.IsChecked = bool.Parse(val);
            if (dict.TryGetValue("DrawBotTrans", out val)) chkDrawBotTrans.IsChecked = bool.Parse(val);
            if (dict.TryGetValue("BotMeshAnchorLong", out val))
                txtBotMeshAnchorLong.Text = val;
            else if (dict.TryGetValue("BotMeshAnchor", out val))
                txtBotMeshAnchorLong.Text = val;
            if (dict.TryGetValue("BotMeshAnchorTrans", out val))
                txtBotMeshAnchorTrans.Text = val;
            else if (dict.TryGetValue("BotMeshAnchor", out val))
                txtBotMeshAnchorTrans.Text = val;
            if (dict.TryGetValue("SpaceLongBot", out val)) txtSpaceLongBot.Text = val;
            if (dict.TryGetValue("SpaceTransBot", out val)) txtSpaceTransBot.Text = val;
            if (dict.TryGetValue("LayoutLBot", out val) && int.TryParse(val, out int iVal)) cboLayoutLBot.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyLongBot", out val)) txtQtyLongBot.Text = val;
            if (dict.TryGetValue("LayoutTBot", out val) && int.TryParse(val, out iVal)) cboLayoutTBot.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyTransBot", out val)) txtQtyTransBot.Text = val;

            if (dict.TryGetValue("SameTop", out val) && bool.TryParse(val, out bool bVal)) chkSameTop.IsChecked = bVal;
            SetComboByRebarName(cboRebarTopLong, "RebarTopLongName");
            SetComboByRebarName(cboRebarTopTrans, "RebarTopTransName");
            if (dict.TryGetValue("CoverTopZLong", out val)) txtCoverTopZLong.Text = val;
            if (dict.TryGetValue("CoverTopZTrans", out val)) txtCoverTopZTrans.Text = val;
            if (dict.TryGetValue("CoverTopLongX", out val)) txtCoverTopLongX.Text = val;
            if (dict.TryGetValue("CoverTopLongY", out val)) txtCoverTopLongY.Text = val;
            if (dict.TryGetValue("CoverTopTransX", out val)) txtCoverTopTransX.Text = val;
            if (dict.TryGetValue("CoverTopTransY", out val)) txtCoverTopTransY.Text = val;
            if (dict.TryGetValue("DrawTopLong", out val)) chkDrawTopLong.IsChecked = bool.Parse(val);
            if (dict.TryGetValue("DrawTopTrans", out val)) chkDrawTopTrans.IsChecked = bool.Parse(val);
            if (dict.TryGetValue("TopMeshAnchorLong", out val))
                txtTopMeshAnchorLong.Text = val;
            else if (dict.TryGetValue("TopMeshAnchor", out val))
                txtTopMeshAnchorLong.Text = val;
            if (dict.TryGetValue("TopMeshAnchorTrans", out val))
                txtTopMeshAnchorTrans.Text = val;
            else if (dict.TryGetValue("TopMeshAnchor", out val))
                txtTopMeshAnchorTrans.Text = val;
            if (dict.TryGetValue("SpaceLongTop", out val)) txtSpaceLongTop.Text = val;
            if (dict.TryGetValue("SpaceTransTop", out val)) txtSpaceTransTop.Text = val;
            if (dict.TryGetValue("LayoutLTop", out val) && int.TryParse(val, out iVal)) cboLayoutLTop.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyLongTop", out val)) txtQtyLongTop.Text = val;
            if (dict.TryGetValue("LayoutTTop", out val) && int.TryParse(val, out iVal)) cboLayoutTTop.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyTransTop", out val)) txtQtyTransTop.Text = val;

            SetComboByRebarName(cboRebarSideX, "RebarSideXName");
            if (txtCoverVertSide != null && dict.TryGetValue("CoverVertSide", out val)) txtCoverVertSide.Text = val;
            if (txtCoverVertSideZBot != null && dict.TryGetValue("CoverVertSideZBot", out val)) txtCoverVertSideZBot.Text = val;
            if (txtCoverVertSideZTop != null && dict.TryGetValue("CoverVertSideZTop", out val)) txtCoverVertSideZTop.Text = val;
            if (dict.TryGetValue("SpaceVertSideX", out val)) txtSpaceVertSideX.Text = val;
            if (txtOffsetSideX != null && dict.TryGetValue("OffsetSideX", out val)) txtOffsetSideX.Text = val;
            if (dict.TryGetValue("LayoutVSideX", out val) && int.TryParse(val, out iVal)) cboLayoutVSideX.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyVertSideX", out val)) txtQtyVertSideX.Text = val;
            
            SetComboByRebarName(cboRebarSideY, "RebarSideYName");
            if (dict.TryGetValue("SpaceVertSideY", out val)) txtSpaceVertSideY.Text = val;
            if (txtOffsetSideY != null && dict.TryGetValue("OffsetSideY", out val)) txtOffsetSideY.Text = val;
            if (dict.TryGetValue("LayoutVSideY", out val) && int.TryParse(val, out iVal)) cboLayoutVSideY.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyVertSideY", out val)) txtQtyVertSideY.Text = val;
            if (chkAutoSideHeight != null && dict.TryGetValue("AutoSideHeight", out val) && bool.TryParse(val, out bVal)) chkAutoSideHeight.IsChecked = bVal;
            if (txtSideHeight != null && dict.TryGetValue("SideHeight", out val)) txtSideHeight.Text = val;
            if (txtVertSideAnchor != null && dict.TryGetValue("VertSideAnchor", out val)) txtVertSideAnchor.Text = val;

            SetComboByRebarName(cboRebarDowel, "RebarDowelName");
            if (dict.TryGetValue("SpaceDowel", out val)) txtSpaceDowel.Text = val;
            if (dict.TryGetValue("LayoutDowel", out val) && int.TryParse(val, out iVal)) cboLayoutDowel.SelectedIndex = iVal;
            if (dict.TryGetValue("QtyDowel", out val)) txtQtyDowel.Text = val;
            
            if (chkAutoDowelHeight != null && dict.TryGetValue("AutoDowelHeight", out val) && bool.TryParse(val, out bVal))
            {
                chkAutoDowelHeight.IsChecked = bVal;
                chkAutoDowelHeight_Click(null, null);
            }

            if (dict.TryGetValue("DowelHeight", out val)) txtDowelHeight.Text = val;
            if (txtDowelLongOffset != null && dict.TryGetValue("DowelLongOffset", out val)) txtDowelLongOffset.Text = val;
            if (txtDowelAnchor != null && dict.TryGetValue("DowelAnchor", out val)) txtDowelAnchor.Text = val;
            SetComboByRebarName(cboRebarAntiBurstX, "RebarAntiBurstXName");
            SetComboByRebarName(cboRebarAntiBurstY, "RebarAntiBurstYName");
            if (txtCoverAntiBurstX != null && dict.TryGetValue("CoverAntiBurstX", out val)) txtCoverAntiBurstX.Text = val;
            if (txtCoverAntiBurstY != null && dict.TryGetValue("CoverAntiBurstY", out val)) txtCoverAntiBurstY.Text = val;
            if (txtCoverAntiBurstZ != null && dict.TryGetValue("CoverAntiBurstZ", out val)) txtCoverAntiBurstZ.Text = val;
            if (chkAutoAntiBurstHeight != null && dict.TryGetValue("AutoAntiBurstHeight", out val) && bool.TryParse(val, out bVal)) chkAutoAntiBurstHeight.IsChecked = bVal;
            chkAutoAntiBurstHeight_CheckedChanged(null, null);
            if (txtAntiBurstHeight != null && dict.TryGetValue("AntiBurstHeight", out val)) txtAntiBurstHeight.Text = val;
            if (txtAntiBurstAnchor != null && dict.TryGetValue("AntiBurstAnchor", out val)) txtAntiBurstAnchor.Text = val;
            if (chkDrawAntiBurstX != null && dict.TryGetValue("DrawAntiBurstX", out val) && bool.TryParse(val, out bVal)) chkDrawAntiBurstX.IsChecked = bVal;
            if (chkDrawAntiBurstY != null && dict.TryGetValue("DrawAntiBurstY", out val) && bool.TryParse(val, out bVal)) chkDrawAntiBurstY.IsChecked = bVal;
            if (txtSpacingSequenceAntiBurstX != null && dict.TryGetValue("SpacingSequenceAntiBurstX", out val)) txtSpacingSequenceAntiBurstX.Text = val;
            if (txtSpacingSequenceAntiBurstY != null && dict.TryGetValue("SpacingSequenceAntiBurstY", out val)) txtSpacingSequenceAntiBurstY.Text = val;
                
            if (dict.TryGetValue("DrawBotRebar", out val) && bool.TryParse(val, out bVal)) chkDrawBotRebar.IsChecked = bVal;
            if (dict.TryGetValue("DrawTopRebar", out val) && bool.TryParse(val, out bVal)) chkDrawTopRebar.IsChecked = bVal;
            if (dict.TryGetValue("DrawVertSideRebar", out val) && bool.TryParse(val, out bVal)) chkDrawVertSideRebar.IsChecked = bVal;
            if (dict.TryGetValue("DrawSideX", out val) && bool.TryParse(val, out bVal)) chkDrawSideX.IsChecked = bVal;
            if (dict.TryGetValue("DrawSideY", out val) && bool.TryParse(val, out bVal)) chkDrawSideY.IsChecked = bVal;
            if (dict.TryGetValue("DrawDowelRebar", out val) && bool.TryParse(val, out bVal)) chkDrawDowelRebar.IsChecked = bVal;
            if (dict.TryGetValue("DrawHorizSideRebar", out val) && bool.TryParse(val, out bVal)) chkDrawHorizSideRebar.IsChecked = bVal;
            if (dict.TryGetValue("DrawHorizSideX", out val) && bool.TryParse(val, out bVal))
                chkDrawHorizSideX.IsChecked = bVal;
            else if (dict.TryGetValue("DrawHorizSideRebar", out val) && bool.TryParse(val, out bVal))
                chkDrawHorizSideX.IsChecked = bVal;
            if (dict.TryGetValue("DrawHorizSideY", out val) && bool.TryParse(val, out bVal))
                chkDrawHorizSideY.IsChecked = bVal;
            else if (dict.TryGetValue("DrawHorizSideRebar", out val) && bool.TryParse(val, out bVal))
                chkDrawHorizSideY.IsChecked = bVal;
            if (chkDrawAntiBurstRebar != null && dict.TryGetValue("DrawAntiBurstRebar", out val) && bool.TryParse(val, out bVal)) chkDrawAntiBurstRebar.IsChecked = bVal;
            if (chkDrawStemVertFront != null && dict.TryGetValue("DrawStemVertFront", out val)) { bool b; if(bool.TryParse(val, out b)) chkDrawStemVertFront.IsChecked = b; }
            if (chkDrawStemVertBack != null && dict.TryGetValue("DrawStemVertBack", out val)) { bool b; if(bool.TryParse(val, out b)) chkDrawStemVertBack.IsChecked = b; }
            if (chkDrawStemHoriz != null && dict.TryGetValue("DrawStemHoriz", out val)) { bool b; if(bool.TryParse(val, out b)) chkDrawStemHoriz.IsChecked = b; }
            if (chkDrawStemTie != null && dict.TryGetValue("DrawStemTie", out val)) { bool b; if(bool.TryParse(val, out b)) chkDrawStemTie.IsChecked = b; }

            if (dict.ContainsKey("RebarHorizSideXName"))
                SetComboByRebarName(cboRebarHorizSideX, "RebarHorizSideXName");
            else
                SetComboByRebarName(cboRebarHorizSideX, "RebarHorizSideName");
            if (dict.ContainsKey("RebarHorizSideYName"))
                SetComboByRebarName(cboRebarHorizSideY, "RebarHorizSideYName");
            else
                SetComboByRebarName(cboRebarHorizSideY, "RebarHorizSideName");
            if (txtCoverHorizSideX != null && dict.TryGetValue("CoverHorizSideX", out val)) txtCoverHorizSideX.Text = val;
            if (txtCoverHorizSideY != null && dict.TryGetValue("CoverHorizSideY", out val)) txtCoverHorizSideY.Text = val;
            if (txtCoverBotHorizSide != null && dict.TryGetValue("CoverBotHorizSide", out val)) txtCoverBotHorizSide.Text = val;
            if (cboRebarStemHoriz != null) SetComboByRebarName(cboRebarStemHoriz, "RebarStemHorizName");
            if (cboHorizPosRelToVert != null && dict.TryGetValue("HorizPosRelToVert", out val) && int.TryParse(val, out iVal)) cboHorizPosRelToVert.SelectedIndex = iVal;
            if (txtCoverStemHorizX != null && dict.TryGetValue("CoverStemHorizX", out val)) txtCoverStemHorizX.Text = val;
            if (txtCoverStemHorizY != null && dict.TryGetValue("CoverStemHorizY", out val)) txtCoverStemHorizY.Text = val;
            if (txtCoverStemHorizZ != null && dict.TryGetValue("CoverStemHorizZ", out val)) txtCoverStemHorizZ.Text = val;
            if (cboLayoutStemHoriz != null && dict.TryGetValue("LayoutStemHoriz", out val) && int.TryParse(val, out iVal)) cboLayoutStemHoriz.SelectedIndex = iVal;
            if (txtSpaceStemHoriz != null && dict.TryGetValue("SpaceStemHoriz", out val)) txtSpaceStemHoriz.Text = val;
            if (txtQtyStemHoriz != null && dict.TryGetValue("QtyStemHoriz", out val)) txtQtyStemHoriz.Text = val;
            if (txtSpaceHorizSideX != null && dict.TryGetValue("SpaceHorizSideX", out val)) txtSpaceHorizSideX.Text = val;
            if (txtSpaceHorizSideY != null && dict.TryGetValue("SpaceHorizSideY", out val)) txtSpaceHorizSideY.Text = val;
            if (cboLayoutHorizSideX != null && dict.TryGetValue("LayoutHorizSideX", out val) && int.TryParse(val, out iVal)) cboLayoutHorizSideX.SelectedIndex = iVal;
            if (cboLayoutHorizSideY != null && dict.TryGetValue("LayoutHorizSideY", out val) && int.TryParse(val, out iVal)) cboLayoutHorizSideY.SelectedIndex = iVal;
            if (txtQtyHorizSideX != null && dict.TryGetValue("QtyHorizSideX", out val)) txtQtyHorizSideX.Text = val;
            if (txtQtyHorizSideY != null && dict.TryGetValue("QtyHorizSideY", out val)) txtQtyHorizSideY.Text = val;
            if (txtHorizAnchor != null && dict.TryGetValue("HorizAnchor", out val)) txtHorizAnchor.Text = val;
            if (cboHorizSideTopPos != null && dict.TryGetValue("HorizSideTopPos", out val) && int.TryParse(val, out iVal)) cboHorizSideTopPos.SelectedIndex = iVal;
            
            if (chkUseDowelAsStemVert != null && dict.TryGetValue("UseDowelAsStemVert", out val) && bool.TryParse(val, out bVal))
            {
                chkUseDowelAsStemVert.IsChecked = bVal;
                chkUseDowelAsStemVert_CheckedChanged(null, null);
            }

            if (cboRebarStemVertFront != null) SetComboByRebarName(cboRebarStemVertFront, "RebarStemVertFrontName");
            if (txtCoverStemVertFront != null && dict.TryGetValue("CoverStemVertFront", out val)) txtCoverStemVertFront.Text = val;
            if (txtCoverStemVertFrontZ != null && dict.TryGetValue("CoverStemVertFrontZ", out val)) txtCoverStemVertFrontZ.Text = val;
            if (cboLayoutStemVertFront != null && dict.TryGetValue("LayoutStemVertFront", out val) && int.TryParse(val, out iVal)) cboLayoutStemVertFront.SelectedIndex = iVal;
            if (txtSpaceStemVertFront != null && dict.TryGetValue("SpaceStemVertFront", out val)) txtSpaceStemVertFront.Text = val;
            if (txtQtyStemVertFront != null && dict.TryGetValue("QtyStemVertFront", out val)) txtQtyStemVertFront.Text = val;
            if (chkAutoStemVertFrontHeight != null && dict.TryGetValue("AutoStemVertFrontHeight", out val) && bool.TryParse(val, out bVal)) {
                chkAutoStemVertFrontHeight.IsChecked = bVal;
                chkAutoStemVertFrontHeight_CheckedChanged(null, null);
            }
            if (txtStemVertFrontHeight != null && dict.TryGetValue("StemVertFrontHeight", out val)) txtStemVertFrontHeight.Text = val;
            if (txtStemVertFrontLongOffset != null && dict.TryGetValue("StemVertFrontLongOffset", out val)) txtStemVertFrontLongOffset.Text = val;
            
            if (cboRebarStemVertBack != null) SetComboByRebarName(cboRebarStemVertBack, "RebarStemVertBackName");
            if (txtCoverStemVertBack != null && dict.TryGetValue("CoverStemVertBack", out val)) txtCoverStemVertBack.Text = val;
            if (txtCoverStemVertBackZ != null && dict.TryGetValue("CoverStemVertBackZ", out val)) txtCoverStemVertBackZ.Text = val;
            if (cboLayoutStemVertBack != null && dict.TryGetValue("LayoutStemVertBack", out val) && int.TryParse(val, out iVal)) cboLayoutStemVertBack.SelectedIndex = iVal;
            if (txtSpaceStemVertBack != null && dict.TryGetValue("SpaceStemVertBack", out val)) txtSpaceStemVertBack.Text = val;
            if (txtQtyStemVertBack != null && dict.TryGetValue("QtyStemVertBack", out val)) txtQtyStemVertBack.Text = val;
            if (chkAutoStemVertBackHeight != null && dict.TryGetValue("AutoStemVertBackHeight", out val) && bool.TryParse(val, out bVal)) {
                chkAutoStemVertBackHeight.IsChecked = bVal;
                chkAutoStemVertBackHeight_CheckedChanged(null, null);
            }
            if (txtStemVertBackHeight != null && dict.TryGetValue("StemVertBackHeight", out val)) txtStemVertBackHeight.Text = val;
            if (txtStemVertBackLongOffset != null && dict.TryGetValue("StemVertBackLongOffset", out val)) txtStemVertBackLongOffset.Text = val;

            if (cboRebarStemTie != null) SetComboByRebarName(cboRebarStemTie, "RebarStemTieName");
            if (txtCoverStemTieY != null && dict.TryGetValue("CoverStemTieY", out val)) txtCoverStemTieY.Text = val;
            if (cboLayoutStemTieV != null && dict.TryGetValue("LayoutStemTieV", out val)) cboLayoutStemTieV.SelectedIndex = int.Parse(val);
            if (txtSpaceStemTieV != null && dict.TryGetValue("SpaceStemTieV", out val)) txtSpaceStemTieV.Text = val;
            if (txtQtyStemTieV != null && dict.TryGetValue("QtyStemTieV", out val)) txtQtyStemTieV.Text = val;
            if (cboLayoutStemTieH != null && dict.TryGetValue("LayoutStemTieH", out val)) cboLayoutStemTieH.SelectedIndex = int.Parse(val);
            if (txtSpaceStemTieH != null && dict.TryGetValue("SpaceStemTieH", out val)) txtSpaceStemTieH.Text = val;
            if (txtQtyStemTieH != null && dict.TryGetValue("QtyStemTieH", out val)) txtQtyStemTieH.Text = val;
            
            if (cboTieShape != null && dict.TryGetValue("StemTieShapeName", out val))
            {
                for (int i = 0; i < cboTieShape.Items.Count; i++)
                {
                    if ((cboTieShape.Items[i] as RebarShape)?.Name == val)
                    {
                        cboTieShape.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (txtStemTieZ != null && dict.TryGetValue("StemTieZ", out val)) txtStemTieZ.Text = val;
            if (cboTieHookDirection != null && dict.TryGetValue("TieHookDirection", out val)) cboTieHookDirection.SelectedIndex = int.Parse(val);

            if (txtStemTieZ != null && dict.TryGetValue("StemTieZ", out val)) txtStemTieZ.Text = val;
            if (txtStemTieDrop != null && dict.TryGetValue("StemTieDrop", out val)) txtStemTieDrop.Text = val;
            if (txtStemVertFrontAnchor != null && dict.TryGetValue("StemVertFrontAnchor", out val)) txtStemVertFrontAnchor.Text = val;
            if (txtStemVertBackAnchor != null && dict.TryGetValue("StemVertBackAnchor", out val)) txtStemVertBackAnchor.Text = val;
        }

        private RebarBarType GetRebarTypeFromCombo(ComboBox cbo)
        {
            if (cbo == null) return null;

            // ComboBox cho phép nhập trực tiếp, vì vậy Text đang hiển thị phải
            // được ưu tiên hơn SelectedItem (SelectedItem có thể vẫn là type cũ).
            string txt = cbo.Text?.Trim();
            if (string.IsNullOrWhiteSpace(txt)) return null;

            foreach (var item in cbo.Items)
            {
                if (item is RebarTypeOption option &&
                    (option.RevitName.Equals(
                         txt,
                         StringComparison.OrdinalIgnoreCase) ||
                     option.DisplayName.Equals(
                         txt,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    return option.RebarType;
                }
                if (item is RebarBarType rt && rt.Name.Equals(txt, StringComparison.OrdinalIgnoreCase)) return rt;
            }

            if (cbo.SelectedItem is RebarTypeOption selectedOption)
                return selectedOption.RebarType;
            if (cbo.SelectedItem is RebarBarType rb) return rb;

            foreach (var item in cbo.Items)
            {
                if (item is RebarTypeOption option &&
                    (option.RevitName.IndexOf(
                         txt,
                         StringComparison.OrdinalIgnoreCase) >= 0 ||
                     option.DisplayName.IndexOf(
                         txt,
                         StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return option.RebarType;
                }
                if (item is RebarBarType rt && rt.Name.Contains(txt)) return rt;
            }
            if (cbo.Items.Count == 0) return null;
            if (cbo.Items[0] is RebarTypeOption firstOption)
                return firstOption.RebarType;
            return cbo.Items[0] as RebarBarType;
        }

        private static string GetRebarDisplayName(string revitName)
        {
            if (string.IsNullOrWhiteSpace(revitName))
                return revitName ?? "";

            string trimmed = revitName.Trim();
            if (trimmed.EndsWith(
                    "M",
                    StringComparison.OrdinalIgnoreCase))
            {
                string numberPart =
                    trimmed.Substring(0, trimmed.Length - 1).Trim();
                if (double.TryParse(
                    numberPart,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
                {
                    return "D" + numberPart;
                }
            }

            return trimmed;
        }

        private void PopulateRebarCombobox(ComboBox cbo, List<RebarBarType> rebarTypes)
        {
            if (cbo == null) return;
            cbo.ItemsSource = rebarTypes;
        }

        private void SetupLayoutComboBox(System.Windows.Controls.ComboBox cbo, System.Windows.Controls.TextBox txtSp, System.Windows.Controls.TextBox txtQty)
        {
            if (cbo == null || txtSp == null || txtQty == null) return;
            cbo.SelectionChanged += (s, e) => ToggleState(cbo, txtSp, txtQty);
            ToggleState(cbo, txtSp, txtQty);
        }

        private void ToggleState(System.Windows.Controls.ComboBox cbo, System.Windows.Controls.TextBox txtSp, System.Windows.Controls.TextBox txtQty)
        {
            if (cbo.SelectedIndex == 0) { txtSp.IsEnabled = true; txtQty.IsEnabled = false; }
            else if (cbo.SelectedIndex == 1) { txtSp.IsEnabled = false; txtQty.IsEnabled = true; }
            else { txtSp.IsEnabled = true; txtQty.IsEnabled = true; }
        }

        private void AttachEvents()
        {
            SetupLayoutComboBox(cboLayoutLBot, txtSpaceLongBot, txtQtyLongBot);
            SetupLayoutComboBox(cboLayoutTBot, txtSpaceTransBot, txtQtyTransBot);
            SetupLayoutComboBox(cboLayoutLTop, txtSpaceLongTop, txtQtyLongTop);
            SetupLayoutComboBox(cboLayoutTTop, txtSpaceTransTop, txtQtyTransTop);
            SetupLayoutComboBox(cboLayoutVSideX, txtSpaceVertSideX, txtQtyVertSideX);
            SetupLayoutComboBox(cboLayoutVSideY, txtSpaceVertSideY, txtQtyVertSideY);
            SetupLayoutComboBox(cboLayoutDowel, txtSpaceDowel, txtQtyDowel);
            SetupLayoutComboBox(cboLayoutHorizSideX, txtSpaceHorizSideX, txtQtyHorizSideX);
            SetupLayoutComboBox(cboLayoutHorizSideY, txtSpaceHorizSideY, txtQtyHorizSideY);

            // Stem events for UI
            if (cboLayoutStemVertFront != null) cboLayoutStemVertFront.SelectionChanged += (s, e) => ToggleState(cboLayoutStemVertFront, txtSpaceStemVertFront, txtQtyStemVertFront);
            if (cboLayoutStemVertBack != null) cboLayoutStemVertBack.SelectionChanged += (s, e) => ToggleState(cboLayoutStemVertBack, txtSpaceStemVertBack, txtQtyStemVertBack);
            if (cboLayoutStemHoriz != null) cboLayoutStemHoriz.SelectionChanged += (s, e) => ToggleState(cboLayoutStemHoriz, txtSpaceStemHoriz, txtQtyStemHoriz);
            if (cboLayoutStemTieH != null) cboLayoutStemTieH.SelectionChanged += (s, e) => ToggleState(cboLayoutStemTieH, txtSpaceStemTieH, txtQtyStemTieH);
            if (cboLayoutStemTieV != null) cboLayoutStemTieV.SelectionChanged += (s, e) => ToggleState(cboLayoutStemTieV, txtSpaceStemTieV, txtQtyStemTieV);
            
            SetupLayoutComboBox(cboLayoutStemVertFront, txtSpaceStemVertFront, txtQtyStemVertFront);
            SetupLayoutComboBox(cboLayoutStemVertBack, txtSpaceStemVertBack, txtQtyStemVertBack);
            SetupLayoutComboBox(cboLayoutStemHoriz, txtSpaceStemHoriz, txtQtyStemHoriz);
            SetupLayoutComboBox(cboLayoutStemTieH, txtSpaceStemTieH, txtQtyStemTieH);
            SetupLayoutComboBox(cboLayoutStemTieV, txtSpaceStemTieV, txtQtyStemTieV);
            
            if (chkAutoSideHeight != null)
            {
                chkAutoSideHeight.Checked += chkAutoSideHeight_CheckedChanged;
                chkAutoSideHeight.Unchecked += chkAutoSideHeight_CheckedChanged;
            }

            if (txtQtyLongBot != null) txtQtyLongBot.TextChanged += (s, e) => { };
            if (txtQtyTransBot != null) txtQtyTransBot.TextChanged += (s, e) => { };
            if (txtQtyLongTop != null) txtQtyLongTop.TextChanged += (s, e) => { };
            if (txtQtyTransTop != null) txtQtyTransTop.TextChanged += (s, e) => { };
            if (txtQtyVertSideX != null) txtQtyVertSideX.TextChanged += (s, e) => { };
            if (txtQtyVertSideY != null) txtQtyVertSideY.TextChanged += (s, e) => { };
            if (txtQtyDowel != null) txtQtyDowel.TextChanged += (s, e) => { };
            if (txtQtyHorizSideX != null) txtQtyHorizSideX.TextChanged += (s, e) => { };
            if (txtQtyHorizSideY != null) txtQtyHorizSideY.TextChanged += (s, e) => { };
        }

        private void ChkSameTop_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (grpTop == null) return;
            bool isSame = chkSameTop.IsChecked == true;
            grpTop.IsEnabled = !isSame;
        }

        private void chkAutoSideHeight_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (chkAutoSideHeight != null && txtSideHeight != null && lblSideHeight != null)
            {
                bool isAuto = chkAutoSideHeight.IsChecked == true;
                lblSideHeight.Text = "Chiều cao thép hông:";

                // Nếu chọn tự động thì khóa ô chiều cao, nếu bỏ chọn thì cho phép nhập
                txtSideHeight.IsEnabled = !isAuto;
                lblSideHeight.IsEnabled = !isAuto;

                // Khi tick "Tự động tính", mờ 2 ô Z đi vì code sẽ lấy theo lưới thép
                if (txtCoverVertSideZBot != null) txtCoverVertSideZBot.IsEnabled = !isAuto;
                if (txtCoverVertSideZTop != null) txtCoverVertSideZTop.IsEnabled = !isAuto;
            }
        }

        private void chkAutoAntiBurstHeight_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (chkAutoAntiBurstHeight == null || txtAntiBurstHeight == null)
                return;

            bool isAuto = chkAutoAntiBurstHeight.IsChecked == true;
            txtAntiBurstHeight.IsEnabled = !isAuto;
            if (lblAntiBurstHeight != null)
                lblAntiBurstHeight.IsEnabled = !isAuto;
            if (isAuto)
                RefreshAutoAntiBurstHeight();
        }

        private void RefreshAutoAntiBurstHeight()
        {
            if (_geoInfo == null ||
                txtAntiBurstHeight == null ||
                chkAutoAntiBurstHeight?.IsChecked != true)
            {
                return;
            }

            double bottomLong = ReadDouble(txtCoverBotZLong, 0);
            double bottomTrans = ReadDouble(txtCoverBotZTrans, 0);
            double topLong = ReadDouble(txtCoverTopZLong, 0);
            double topTrans = ReadDouble(txtCoverTopZTrans, 0);
            double footingHeightMm = ToMillimeters(_geoInfo.FootingHeight);
            double calculatedHeight = footingHeightMm -
                                      Math.Min(bottomLong, bottomTrans) -
                                      Math.Min(topLong, topTrans);
            txtAntiBurstHeight.Text = Math.Max(0, calculatedHeight).ToString("0");
        }

        private void chkAutoDowelHeight_Click(object sender, RoutedEventArgs e)
        {
            if (lblDowelHeight != null && chkAutoDowelHeight != null)
            {
                if (chkAutoDowelHeight.IsChecked == true)
                    lblDowelHeight.Text = "Cách đỉnh thân mố (mm):";
                else
                    lblDowelHeight.Text = "Chiều cao đoạn chờ (mm):";
            }
        }

        public void UpdateStatus()
        {
            if (SelectedAbutment == null)
            {
                _geoInfo = null;
                txtStatus.Text = "Trạng thái: Chưa chọn mố cầu";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                DrawPreview2D();
            }
            else
            {
                try
                {
                    AbutmentGeometryReader reader = new AbutmentGeometryReader();
                    _geoInfo = reader.Read(_doc, SelectedAbutment);
                    RefreshGeometryStatus();
                }
                catch (Exception ex)
                {
                    SelectedAbutment = null;
                    _geoInfo = null;
                    txtStatus.Text = "Family mố không hợp lệ: " + ex.Message;
                    txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                    DrawPreview2D();
                }
            }
        }

        private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged is a routed event; ignore events raised by controls
            // contained inside a tab, such as ComboBox.
            if (!ReferenceEquals(e.OriginalSource, tabMain)) return;
            RefreshGeometryStatus();
            DrawPreview2D();
        }

        private void BtnTogglePreview_Click(object sender, RoutedEventArgs e)
        {
            if (btnTogglePreview == null || colPreview == null || borderPreview == null) return;
            if (btnTogglePreview.IsChecked == true)
            {
                this.MinWidth = 960;
                this.Width = this.Width + 300;
                colPreview.Width = new GridLength(300);
                borderPreview.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.MinWidth = 660;
                this.Width = this.Width - 300;
                colPreview.Width = new GridLength(0);
                borderPreview.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void RefreshGeometryStatus()
        {
            if (txtStatus == null || SelectedAbutment == null || _geoInfo == null)
                return;

            if (SelectedTabIndex == 1)
            {
                double stemLengthMm = ToMillimeters(_geoInfo.StemLength);
                double stemWidthMm = ToMillimeters(_geoInfo.StemWidth);
                double stemHeightMm = ToMillimeters(_geoInfo.StemHeight);

                txtStatus.Text =
                    $"Đã nhận dạng mố ID {SelectedAbutment.Id}: " +
                    $"thân {stemLengthMm:0} × {stemWidthMm:0} × {stemHeightMm:0} mm";
            }
            else
            {
                double footingLengthMm = ToMillimeters(_geoInfo.FootingLength);
                double footingWidthMm = ToMillimeters(_geoInfo.FootingWidth);
                double footingHeightMm = ToMillimeters(_geoInfo.FootingHeight);

                txtStatus.Text =
                    $"Đã nhận dạng mố ID {SelectedAbutment.Id}: " +
                    $"bệ {footingLengthMm:0} × {footingWidthMm:0} × {footingHeightMm:0} mm";
            }

            txtStatus.Foreground = System.Windows.Media.Brushes.Green;
            RefreshAutoAntiBurstHeight();
            DrawPreview2D();
        }

        private static double ToMillimeters(double internalValue)
        {
            // Revit's internal length unit is feet. Keeping this small
            // conversion independent also lets the standalone UI preview run
            // without starting the Revit application.
            return internalValue * 304.8;
        }

        private void Input_Changed(object sender, TextChangedEventArgs e)
        {
        }

        private async void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (OnPickAbutment != null)
                {
                    await OnPickAbutment(this);
                }
            }
            catch (Exception ex)
            {
                Show();
                MessageBox.Show("Khong the kich hoat lenh chon mo cau.\nChi tiet: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _isDrawingRebar;

        private async void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            if (_isDrawingRebar) return;

            if (SelectedAbutment == null)
            {
                MessageBox.Show("Vui lòng chọn bệ mố trước khi vẽ!", "Chưa chọn bệ mố", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _isDrawingRebar = true;
                if (BtnDrawRebar != null) BtnDrawRebar.IsEnabled = false;

                DrawBotRebar = chkDrawBotRebar.IsChecked ?? true;
                DrawTopRebar = chkDrawTopRebar.IsChecked ?? true;
                DrawVertSideRebar = chkDrawVertSideRebar.IsChecked ?? true;
                DrawSideX = DrawVertSideRebar && (chkDrawSideX?.IsChecked ?? true);
                DrawSideY = DrawVertSideRebar && (chkDrawSideY?.IsChecked ?? true);
                DrawDowelRebar = chkDrawDowelRebar.IsChecked ?? true;
                DrawHorizSideRebar = chkDrawHorizSideRebar.IsChecked ?? true;
                DrawHorizSideX = DrawHorizSideRebar && (chkDrawHorizSideX?.IsChecked ?? true);
                DrawHorizSideY = DrawHorizSideRebar && (chkDrawHorizSideY?.IsChecked ?? true);
                DrawAntiBurstRebar = chkDrawAntiBurstRebar?.IsChecked ?? true;

                DrawStemVertFront = chkDrawStemVertFront.IsChecked ?? true;
                DrawStemVertBack = chkDrawStemVertBack.IsChecked ?? true;
                DrawStemHoriz = chkDrawStemHoriz.IsChecked ?? true;
                DrawStemTie = chkDrawStemTie.IsChecked ?? true;

                bool sameTop = chkSameTop.IsChecked == true;

                if (DrawBotRebar && cboRebarBotLong != null && cboRebarBotTrans != null && txtCoverBotZLong != null && txtCoverBotZTrans != null && txtCoverBotLongX != null && txtSpaceLongBot != null && txtSpaceTransBot != null) {
                    RebarBotLong = GetRebarTypeFromCombo(cboRebarBotLong);
                    RebarBotTrans = GetRebarTypeFromCombo(cboRebarBotTrans);
                    CoverBotZLongMm = double.Parse(txtCoverBotZLong.Text);
                    CoverBotZTransMm = double.Parse(txtCoverBotZTrans.Text);
                    CoverBotLongXMm = double.Parse(txtCoverBotLongX.Text);
                    CoverBotLongYMm = double.Parse(txtCoverBotLongY.Text);
                    CoverBotTransXMm = double.Parse(txtCoverBotTransX.Text);
                    CoverBotTransYMm = double.Parse(txtCoverBotTransY.Text);
                    DrawBotLong = chkDrawBotLong.IsChecked == true;
                    DrawBotTrans = chkDrawBotTrans.IsChecked == true;
                    BotMeshAnchorLongMm = double.Parse(txtBotMeshAnchorLong.Text);
                    BotMeshAnchorTransMm = double.Parse(txtBotMeshAnchorTrans.Text);
                    
                    LayoutLBot = cboLayoutLBot.SelectedIndex;
                    SpaceLongBotMm = double.Parse(txtSpaceLongBot.Text);
                    QtyLongBotMm = double.Parse(txtQtyLongBot.Text);

                    LayoutTBot = cboLayoutTBot.SelectedIndex;
                    SpaceTransBotMm = double.Parse(txtSpaceTransBot.Text);
                    QtyTransBotMm = double.Parse(txtQtyTransBot.Text);
                }
                else if (!DrawBotRebar)
                {
                    DrawBotLong = false;
                    DrawBotTrans = false;
                }

                if (DrawTopRebar && chkSameTop?.IsChecked == true) {
                    RebarTopLong = RebarBotLong;
                    RebarTopTrans = RebarBotTrans;
                    CoverTopZLongMm = CoverBotZLongMm;
                    CoverTopZTransMm = CoverBotZTransMm;
                    CoverTopLongXMm = CoverBotLongXMm;
                    CoverTopLongYMm = CoverBotLongYMm;
                    CoverTopTransXMm = CoverBotTransXMm;
                    CoverTopTransYMm = CoverBotTransYMm;
                    DrawTopLong = DrawBotLong;
                    DrawTopTrans = DrawBotTrans;
                    TopMeshAnchorLongMm = BotMeshAnchorLongMm;
                    TopMeshAnchorTransMm = BotMeshAnchorTransMm;
                    
                    LayoutLTop = LayoutLBot;
                    SpaceLongTopMm = SpaceLongBotMm;
                    QtyLongTopMm = QtyLongBotMm;
                    
                    LayoutTTop = LayoutTBot;
                    SpaceTransTopMm = SpaceTransBotMm;
                    QtyTransTopMm = QtyTransBotMm;
                }
                else if (DrawTopRebar) {
                    RebarTopLong = GetRebarTypeFromCombo(cboRebarTopLong);
                    RebarTopTrans = GetRebarTypeFromCombo(cboRebarTopTrans);
                    CoverTopZLongMm = double.Parse(txtCoverTopZLong.Text);
                    CoverTopZTransMm = double.Parse(txtCoverTopZTrans.Text);
                    CoverTopLongXMm = double.Parse(txtCoverTopLongX.Text);
                    CoverTopLongYMm = double.Parse(txtCoverTopLongY.Text);
                    CoverTopTransXMm = double.Parse(txtCoverTopTransX.Text);
                    CoverTopTransYMm = double.Parse(txtCoverTopTransY.Text);
                    DrawTopLong = chkDrawTopLong.IsChecked == true;
                    DrawTopTrans = chkDrawTopTrans.IsChecked == true;
                    TopMeshAnchorLongMm = double.Parse(txtTopMeshAnchorLong.Text);
                    TopMeshAnchorTransMm = double.Parse(txtTopMeshAnchorTrans.Text);
                    
                    LayoutLTop = cboLayoutLTop.SelectedIndex;
                    SpaceLongTopMm = double.Parse(txtSpaceLongTop.Text);
                    QtyLongTopMm = double.Parse(txtQtyLongTop.Text);
                    
                    LayoutTTop = cboLayoutTTop.SelectedIndex;
                    SpaceTransTopMm = double.Parse(txtSpaceTransTop.Text);
                    QtyTransTopMm = double.Parse(txtQtyTransTop.Text);
                }
                else
                {
                    DrawTopLong = false;
                    DrawTopTrans = false;
                }

                if (DrawVertSideRebar)
                {
                    RebarSideX = GetRebarTypeFromCombo(cboRebarSideX);
                    if (txtCoverVertSide != null) CoverVertSideMm = double.Parse(txtCoverVertSide.Text);
                    if (txtCoverVertSideZBot != null) CoverVertSideZBotMm = double.Parse(txtCoverVertSideZBot.Text);
                    if (txtCoverVertSideZTop != null) CoverVertSideZTopMm = double.Parse(txtCoverVertSideZTop.Text);
                    LayoutVSideX = cboLayoutVSideX.SelectedIndex;
                    SpaceVertSideXMm = double.Parse(txtSpaceVertSideX.Text);
                    if (txtOffsetSideX != null) OffsetSideXMm = double.Parse(txtOffsetSideX.Text);
                    QtyVertSideXMm = double.Parse(txtQtyVertSideX.Text);
                    
                    RebarSideY = GetRebarTypeFromCombo(cboRebarSideY);
                    LayoutVSideY = cboLayoutVSideY.SelectedIndex;
                    SpaceVertSideYMm = double.Parse(txtSpaceVertSideY.Text);
                    if (txtOffsetSideY != null) OffsetSideYMm = double.Parse(txtOffsetSideY.Text);
                    QtyVertSideYMm = double.Parse(txtQtyVertSideY.Text);
                    
                    IsAutoSideHeight = chkAutoSideHeight.IsChecked == true;
                    SideHeightMm = double.Parse(txtSideHeight.Text);
                    VertSideAnchorMm = txtVertSideAnchor != null && !string.IsNullOrEmpty(txtVertSideAnchor.Text) ? double.Parse(txtVertSideAnchor.Text) : 640;
                }

                if (DrawAntiBurstRebar)
                {
                    RebarAntiBurstX = GetRebarTypeFromCombo(cboRebarAntiBurstX);
                    RebarAntiBurstY = GetRebarTypeFromCombo(cboRebarAntiBurstY);
                    DrawAntiBurstX = chkDrawAntiBurstX?.IsChecked ?? true;
                    DrawAntiBurstY = chkDrawAntiBurstY?.IsChecked ?? true;
                    CoverAntiBurstXMm = double.Parse(txtCoverAntiBurstX.Text);
                    CoverAntiBurstYMm = double.Parse(txtCoverAntiBurstY.Text);
                    CoverAntiBurstZMm = double.Parse(txtCoverAntiBurstZ.Text);
                    IsAutoAntiBurstHeight = chkAutoAntiBurstHeight?.IsChecked ?? true;
                    AntiBurstHeightMm = double.Parse(txtAntiBurstHeight.Text);
                    AntiBurstAnchorMm = double.Parse(txtAntiBurstAnchor.Text);
                    SpacingSequenceAntiBurstX = txtSpacingSequenceAntiBurstX.Text;
                    SpacingSequenceAntiBurstY = txtSpacingSequenceAntiBurstY.Text;
                }
                else
                {
                    DrawAntiBurstX = false;
                    DrawAntiBurstY = false;
                }

                IsAutoDowelHeight = chkAutoDowelHeight?.IsChecked ?? false;

                if (DrawDowelRebar)
                {
                    RebarDowel = GetRebarTypeFromCombo(cboRebarDowel);
                    LayoutDowel = cboLayoutDowel.SelectedIndex;
                    SpaceDowelMm = double.Parse(txtSpaceDowel.Text);
                    QtyDowelMm = double.Parse(txtQtyDowel.Text);
                    DowelHeightMm = double.Parse(txtDowelHeight.Text);
                    DowelLongOffsetMm = txtDowelLongOffset != null ? double.Parse(txtDowelLongOffset.Text) : 50;
                    DowelAnchorMm = txtDowelAnchor != null ? double.Parse(txtDowelAnchor.Text) : 200;
                }

                if (DrawHorizSideRebar && cboRebarHorizSideX != null && cboRebarHorizSideY != null && txtSpaceHorizSideX != null && txtSpaceHorizSideY != null && txtHorizAnchor != null && txtCoverHorizSideX != null && txtCoverBotHorizSide != null) {
                    RebarHorizSideX = GetRebarTypeFromCombo(cboRebarHorizSideX);
                    RebarHorizSideY = GetRebarTypeFromCombo(cboRebarHorizSideY);
                    CoverHorizSideXMm = double.Parse(txtCoverHorizSideX.Text);
                    if (txtCoverHorizSideY != null) CoverHorizSideYMm = double.Parse(txtCoverHorizSideY.Text);
                    CoverBotHorizSideMm = double.Parse(txtCoverBotHorizSide.Text);
                    LayoutHorizSideX = cboLayoutHorizSideX.SelectedIndex;
                    SpaceHorizSideXMm = double.Parse(txtSpaceHorizSideX.Text);
                    QtyHorizSideXMm = double.Parse(txtQtyHorizSideX.Text);
                    LayoutHorizSideY = cboLayoutHorizSideY.SelectedIndex;
                    SpaceHorizSideYMm = double.Parse(txtSpaceHorizSideY.Text);
                    QtyHorizSideYMm = double.Parse(txtQtyHorizSideY.Text);
                    HorizAnchorMm = double.Parse(txtHorizAnchor.Text);
                    HorizSideTopPos = cboHorizSideTopPos?.SelectedIndex ?? 0;
                }

                if (SelectedTabIndex == 1)
                {
                    UseDowelAsStemVert = chkUseDowelAsStemVert.IsChecked ?? true;
                    if (cboRebarStemVertFront != null) RebarStemVertFront = GetRebarTypeFromCombo(cboRebarStemVertFront);
                    if (txtCoverStemVertFront != null) CoverStemVertFrontMm = double.Parse(txtCoverStemVertFront.Text);
                    if (txtCoverStemVertFrontZ != null) CoverStemVertFrontZMm = double.Parse(txtCoverStemVertFrontZ.Text);
                    if (cboLayoutStemVertFront != null) LayoutStemVertFront = cboLayoutStemVertFront.SelectedIndex;
                    if (txtSpaceStemVertFront != null) SpaceStemVertFrontMm = double.Parse(txtSpaceStemVertFront.Text);
                    if (txtQtyStemVertFront != null) QtyStemVertFront = double.Parse(txtQtyStemVertFront.Text);
                    IsAutoStemVertFrontHeight = chkAutoStemVertFrontHeight?.IsChecked == true;
                    if (txtStemVertFrontHeight != null) StemVertFrontHeightMm = double.Parse(txtStemVertFrontHeight.Text);
                    if (txtStemVertFrontLongOffset != null) StemVertFrontLongOffsetMm = double.Parse(txtStemVertFrontLongOffset.Text);
                    if (txtStemVertFrontAnchor != null) StemVertFrontAnchorMm = double.Parse(txtStemVertFrontAnchor.Text);

                    if (cboRebarStemVertBack != null) RebarStemVertBack = GetRebarTypeFromCombo(cboRebarStemVertBack);
                    if (txtCoverStemVertBack != null) CoverStemVertBackMm = double.Parse(txtCoverStemVertBack.Text);
                    if (txtCoverStemVertBackZ != null) CoverStemVertBackZMm = double.Parse(txtCoverStemVertBackZ.Text);
                    if (cboLayoutStemVertBack != null) LayoutStemVertBack = cboLayoutStemVertBack.SelectedIndex;
                    if (txtSpaceStemVertBack != null) SpaceStemVertBackMm = double.Parse(txtSpaceStemVertBack.Text);
                    if (txtQtyStemVertBack != null) QtyStemVertBack = double.Parse(txtQtyStemVertBack.Text);
                    IsAutoStemVertBackHeight = chkAutoStemVertBackHeight?.IsChecked == true;
                    if (txtStemVertBackHeight != null) StemVertBackHeightMm = double.Parse(txtStemVertBackHeight.Text);
                    if (txtStemVertBackLongOffset != null) StemVertBackLongOffsetMm = double.Parse(txtStemVertBackLongOffset.Text);
                    if (txtStemVertBackAnchor != null) StemVertBackAnchorMm = double.Parse(txtStemVertBackAnchor.Text);

                    if (cboRebarStemHoriz != null) RebarStemHoriz = GetRebarTypeFromCombo(cboRebarStemHoriz);
                    if (cboHorizPosRelToVert != null) HorizPosRelToVert = cboHorizPosRelToVert.SelectedIndex;
                    if (txtCoverStemHorizX != null) CoverStemHorizXMm = double.Parse(txtCoverStemHorizX.Text);
                    if (txtCoverStemHorizY != null) CoverStemHorizYMm = double.Parse(txtCoverStemHorizY.Text);
                    if (txtCoverStemHorizZ != null) CoverStemHorizZMm = double.Parse(txtCoverStemHorizZ.Text);
                    if (cboLayoutStemHoriz != null) LayoutStemHoriz = cboLayoutStemHoriz.SelectedIndex;
                    if (txtSpaceStemHoriz != null) SpaceStemHorizMm = double.Parse(txtSpaceStemHoriz.Text);
                    if (txtQtyStemHoriz != null) QtyStemHoriz = double.Parse(txtQtyStemHoriz.Text);

                    if (cboRebarStemTie != null) RebarStemTie = GetRebarTypeFromCombo(cboRebarStemTie);
                    if (txtCoverStemTieY != null) CoverStemTieYMm = double.Parse(txtCoverStemTieY.Text);
                    if (cboLayoutStemTieV != null) LayoutStemTieV = cboLayoutStemTieV.SelectedIndex;
                    if (txtSpaceStemTieV != null) SpaceStemTieVMm = double.Parse(txtSpaceStemTieV.Text);
                    if (txtQtyStemTieV != null) QtyStemTieV = double.Parse(txtQtyStemTieV.Text);
                    if (cboLayoutStemTieH != null) LayoutStemTieH = cboLayoutStemTieH.SelectedIndex;
                    if (txtSpaceStemTieH != null) SpaceStemTieHMm = double.Parse(txtSpaceStemTieH.Text);
                    if (txtQtyStemTieH != null) QtyStemTieH = double.Parse(txtQtyStemTieH.Text);
                    StemTieShapeName = cboTieShape != null && cboTieShape.SelectedItem is RebarShape shape ? shape.Name : "M_01";
                    StemTieZMm = txtStemTieZ != null ? double.Parse(txtStemTieZ.Text) : 50;
                    StemTieDropMm = txtStemTieDrop != null ? double.Parse(txtStemTieDrop.Text) : 0;
                    if (cboTieHookDirection != null) TieHookDirection = cboTieHookDirection.SelectedIndex;
                }

                if (SelectedTabIndex == 0)
                {
                    if (!DrawBotRebar &&
                        !DrawTopRebar &&
                        !DrawVertSideRebar &&
                        !DrawDowelRebar &&
                        !DrawHorizSideRebar &&
                        !DrawAntiBurstRebar)
                    {
                        MessageBox.Show("Vui lÃ²ng chá»n Ã­t nháº¥t má»™t nhÃ³m thÃ©p Bá»‡ má»‘ cáº§n váº½.", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if ((DrawBotRebar && DrawBotLong && BotMeshAnchorLongMm <= 0) ||
                        (DrawBotRebar && DrawBotTrans && BotMeshAnchorTransMm <= 0) ||
                        (DrawTopRebar && DrawTopLong && TopMeshAnchorLongMm <= 0) ||
                        (DrawTopRebar && DrawTopTrans && TopMeshAnchorTransMm <= 0))
                    {
                        MessageBox.Show(
                            "L ngàm của lưới thép đỉnh/đáy phải lớn hơn 0 mm.",
                            "Lỗi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    if ((DrawBotRebar && DrawBotLong && RebarBotLong == null) ||
                        (DrawBotRebar && DrawBotTrans && RebarBotTrans == null) ||
                        (DrawTopRebar && DrawTopLong && RebarTopLong == null) ||
                        (DrawTopRebar && DrawTopTrans && RebarTopTrans == null) ||
                        (DrawVertSideRebar && DrawSideX && RebarSideX == null) ||
                        (DrawVertSideRebar && DrawSideY && RebarSideY == null) ||
                        (DrawDowelRebar && RebarDowel == null) ||
                        (DrawAntiBurstRebar && DrawAntiBurstX && RebarAntiBurstX == null) ||
                        (DrawAntiBurstRebar && DrawAntiBurstY && RebarAntiBurstY == null) ||
                        (DrawHorizSideRebar && DrawHorizSideX && RebarHorizSideX == null) ||
                        (DrawHorizSideRebar && DrawHorizSideY && RebarHorizSideY == null))
                    {
                        MessageBox.Show("Vui lòng chọn đầy đủ đường kính thép cho Bệ mố!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else if (SelectedTabIndex == 1)
                {
                    if (UseDowelAsStemVert && RebarDowel == null)
                    {
                        MessageBox.Show("Vui lòng chọn đầy đủ đường kính thép chờ ở Tab Bệ Mố (vì bạn chọn dùng chung)!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!UseDowelAsStemVert && ((cboRebarStemVertFront != null && RebarStemVertFront == null) || (cboRebarStemVertBack != null && RebarStemVertBack == null)))
                    {
                        MessageBox.Show("Vui lòng chọn đầy đủ đường kính thép Thân mố!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if ((cboRebarStemHoriz != null && RebarStemHoriz == null) ||
                        (cboRebarStemTie != null && RebarStemTie == null))
                    {
                        MessageBox.Show("Vui lòng chọn đầy đủ đường kính thép ngang và thép đai Thân mố!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                GetSettingsToDictionary();
                if (OnDrawRebar != null)
                {
                    await OnDrawRebar(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dữ liệu nhập không hợp lệ. Vui lòng nhập số.\nChi tiết: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDrawingRebar = false;
                if (BtnDrawRebar != null) BtnDrawRebar.IsEnabled = true;
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetSettingsToDictionary();
                OnSaveSettings?.Invoke(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dữ liệu nhập không hợp lệ. Vui lòng nhập số.\nChi tiết: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnPickTopRebar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (OnPickHorizSideTopRebar != null)
                {
                    await OnPickHorizSideTopRebar(this);
                }
            }
            catch (Exception ex)
            {
                Show();
                MessageBox.Show("Khong the kich hoat lenh chon thep.\nChi tiet: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenNamingUI_Click(object sender, RoutedEventArgs e)
        {
            RebarNamingWindow namingWin = new RebarNamingWindow(RebarNamesConfig);
            namingWin.Owner = this;
            if (namingWin.ShowDialog() == true)
            {
                RebarNamesConfig = namingWin.NamingConfig;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            UserAction = UIAction.Cancel;
            this.Close();
        }

        private void BtnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "ThongSoMoCau"; 
            dlg.DefaultExt = ".txt"; 
            dlg.Filter = "Text documents (.txt)|*.txt"; 

            if (dlg.ShowDialog() == true)
            {
                var dict = GetSettingsToDictionary();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(dlg.FileName))
                {
                    foreach (var entry in dict)
                    {
                        file.WriteLine($"{entry.Key}={entry.Value}");
                    }
                }
                MessageBox.Show("Đã lưu thông số thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportFootingExcel_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAbutment == null)
            {
                MessageBox.Show(
                    "Vui lòng chọn mố cầu trước khi xuất thống kê.",
                    "Chưa chọn mố",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            FootingRebarExportSelectionWindow selectionWindow =
                new FootingRebarExportSelectionWindow(GetRebarNamesByGroup())
                {
                    Owner = this
                };
            if (selectionWindow.ShowDialog() != true) return;

            Microsoft.Win32.SaveFileDialog dialog =
                new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "ThongKeThepBeMo",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx"
                };

            if (dialog.ShowDialog() != true) return;

            ExportFootingExcelPath = dialog.FileName;
            ExportFootingGroupCodes = selectionWindow.SelectedGroupCodes;
            OnExportFootingExcel?.Invoke(this);
        }

        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var dict = new Dictionary<string, string>();
                    string[] lines = System.IO.File.ReadAllLines(dlg.FileName);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx);
                            string val = line.Substring(idx + 1);
                            dict[key] = val;
                        }
                    }
                    ApplySettingsFromDictionary(dict);
                    MessageBox.Show("Đã tải thông số thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi đọc file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- QUICK NAVIGATION EVENT HANDLERS ---
        private void Nav_BeMoGeo_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpBeMoGeo?.BringIntoView(); }
        private void Nav_BotRebar_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpBotRebar?.BringIntoView(); }
        private void Nav_TopRebar_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpTop?.BringIntoView(); }
        private void Nav_VertSide_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpVertSide?.BringIntoView(); }
        private void Nav_AntiBurst_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpAntiBurst?.BringIntoView(); }
        private void Nav_Dowel_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpDowel?.BringIntoView(); }
        private void Nav_HorizSide_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 0; tabMain.UpdateLayout(); grpHorizSide?.BringIntoView(); }

        private void Nav_StemVertFront_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 1; tabMain.UpdateLayout(); grpStemVertFront?.BringIntoView(); }
        private void Nav_StemVertBack_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 1; tabMain.UpdateLayout(); grpStemVertBack?.BringIntoView(); }
        private void Nav_StemHoriz_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 1; tabMain.UpdateLayout(); grpStemHoriz?.BringIntoView(); }
        private void Nav_StemTie_Click(object sender, RoutedEventArgs e) { tabMain.SelectedIndex = 1; tabMain.UpdateLayout(); grpStemTie?.BringIntoView(); }

        public AbutmentRebarConfig GetAbutmentConfig()
        {
            return new AbutmentRebarConfig
            {
                Footing = new FootingMeshSettings
                {
                    DrawBotRebar = this.DrawBotRebar,
                    DrawTopRebar = this.DrawTopRebar,
                    DrawBotLong = this.DrawBotLong,
                    DrawBotTrans = this.DrawBotTrans,
                    DrawTopLong = this.DrawTopLong,
                    DrawTopTrans = this.DrawTopTrans,
                    BarBotLong = this.RebarBotLong,
                    BarBotTrans = this.RebarBotTrans,
                    CvBotZLongMm = this.CoverBotZLongMm,
                    CvBotZTransMm = this.CoverBotZTransMm,
                    CvBotLongXMm = this.CoverBotLongXMm,
                    CvBotLongYMm = this.CoverBotLongYMm,
                    CvBotTransXMm = this.CoverBotTransXMm,
                    CvBotTransYMm = this.CoverBotTransYMm,
                    BotMeshAnchorLongMm = this.BotMeshAnchorLongMm,
                    BotMeshAnchorTransMm = this.BotMeshAnchorTransMm,
                    LayoutLBot = this.LayoutLBot,
                    SpLBotMm = this.SpaceLongBotMm,
                    QtyLBot = this.QtyLongBotMm,
                    LayoutTBot = this.LayoutTBot,
                    SpTBotMm = this.SpaceTransBotMm,
                    QtyTBot = this.QtyTransBotMm,
                    BarTopLong = this.RebarTopLong,
                    BarTopTrans = this.RebarTopTrans,
                    CvTopZLongMm = this.CoverTopZLongMm,
                    CvTopZTransMm = this.CoverTopZTransMm,
                    CvTopLongXMm = this.CoverTopLongXMm,
                    CvTopLongYMm = this.CoverTopLongYMm,
                    CvTopTransXMm = this.CoverTopTransXMm,
                    CvTopTransYMm = this.CoverTopTransYMm,
                    TopMeshAnchorLongMm = this.TopMeshAnchorLongMm,
                    TopMeshAnchorTransMm = this.TopMeshAnchorTransMm,
                    LayoutLTop = this.LayoutLTop,
                    SpLTopMm = this.SpaceLongTopMm,
                    QtyLTop = this.QtyLongTopMm,
                    LayoutTTop = this.LayoutTTop,
                    SpTTopMm = this.SpaceTransTopMm,
                    QtyTTop = this.QtyTransTopMm
                },
                Side = new SideRebarSettings
                {
                    DrawVertSideRebar = this.DrawVertSideRebar,
                    DrawSideX = this.DrawSideX,
                    DrawSideY = this.DrawSideY,
                    DrawHorizSideRebar = this.DrawHorizSideRebar,
                    DrawHorizSideX = this.DrawHorizSideX,
                    DrawHorizSideY = this.DrawHorizSideY,
                    BarSideX = this.RebarSideX,
                    CvVertSideMm = this.CoverVertSideMm,
                    CvVertSideZBotMm = this.CoverVertSideZBotMm,
                    CvVertSideZTopMm = this.CoverVertSideZTopMm,
                    OffsetSideXMm = this.OffsetSideXMm,
                    LayoutVSideX = this.LayoutVSideX,
                    SpVSideXMm = this.SpaceVertSideXMm,
                    QtyVSideX = this.QtyVertSideXMm,
                    BarSideY = this.RebarSideY,
                    OffsetSideYMm = this.OffsetSideYMm,
                    LayoutVSideY = this.LayoutVSideY,
                    SpVSideYMm = this.SpaceVertSideYMm,
                    QtyVSideY = this.QtyVertSideYMm,
                    AutoSideHeight = this.IsAutoSideHeight,
                    SideHeightMm = this.SideHeightMm,
                    VertSideAnchorMm = this.VertSideAnchorMm,
                    BarHorizSideX = this.RebarHorizSideX,
                    BarHorizSideY = this.RebarHorizSideY,
                    CvHorizSideXMm = this.CoverHorizSideXMm,
                    CvHorizSideYMm = this.CoverHorizSideYMm,
                    CvBotHorizSideMm = this.CoverBotHorizSideMm,
                    LayoutHorizSideX = this.LayoutHorizSideX,
                    SpHorizSideXMm = this.SpaceHorizSideXMm,
                    QtyHorizSideX = this.QtyHorizSideXMm,
                    LayoutHorizSideY = this.LayoutHorizSideY,
                    SpHorizSideYMm = this.SpaceHorizSideYMm,
                    QtyHorizSideY = this.QtyHorizSideYMm,
                    HorizAnchorMm = this.HorizAnchorMm,
                    HorizSideTopPos = this.HorizSideTopPos
                },
                Dowel = new DowelRebarSettings
                {
                    DrawDowelRebar = this.DrawDowelRebar,
                    BarDowel = this.RebarDowel,
                    LayoutDowel = this.LayoutDowel,
                    SpDowelMm = this.SpaceDowelMm,
                    QtyDowel = this.QtyDowelMm,
                    AutoDowelHeight = this.IsAutoDowelHeight,
                    DowelHeightMm = this.DowelHeightMm,
                    DowelLongOffsetMm = this.DowelLongOffsetMm,
                    DowelAnchorMm = this.DowelAnchorMm
                },
                AntiBurst = new AntiBurstSettings
                {
                    DrawAntiBurstRebar = this.DrawAntiBurstRebar,
                    DrawAntiBurstX = this.DrawAntiBurstX,
                    DrawAntiBurstY = this.DrawAntiBurstY,
                    BarAntiBurstX = this.RebarAntiBurstX,
                    BarAntiBurstY = this.RebarAntiBurstY,
                    CvAntiBurstXMm = this.CoverAntiBurstXMm,
                    CvAntiBurstYMm = this.CoverAntiBurstYMm,
                    CvAntiBurstZMm = this.CoverAntiBurstZMm,
                    AutoAntiBurstHeight = this.IsAutoAntiBurstHeight,
                    AntiBurstHeightMm = this.AntiBurstHeightMm,
                    AntiBurstAnchorMm = this.AntiBurstAnchorMm,
                    SpacingSequenceAntiBurstX = this.SpacingSequenceAntiBurstX,
                    SpacingSequenceAntiBurstY = this.SpacingSequenceAntiBurstY
                }
            };
        }

        public StemRebarConfig GetStemConfig()
        {
            return new StemRebarConfig
            {
                UseDowelAsStemVert = this.UseDowelAsStemVert,
                DrawStemVertFront = this.DrawStemVertFront,
                DrawStemVertBack = this.DrawStemVertBack,
                DrawStemHoriz = this.DrawStemHoriz,
                DrawStemTie = this.DrawStemTie,
                BarVertFront = this.UseDowelAsStemVert ? this.RebarDowel : this.RebarStemVertFront,
                CvVertFrontMm = this.UseDowelAsStemVert ? this.CoverTopZLongMm : this.CoverStemVertFrontMm,
                CvVertFrontZMm = this.CoverStemVertFrontZMm,
                LayoutVertFront = this.LayoutStemVertFront,
                SpVertFrontMm = this.SpaceStemVertFrontMm,
                QtyVertFront = this.QtyStemVertFront,
                AutoVertFrontHeight = this.IsAutoStemVertFrontHeight,
                VertFrontHeightMm = this.StemVertFrontHeightMm,
                VertFrontLongOffsetMm = this.StemVertFrontLongOffsetMm,
                AnchorFrontMm = this.StemVertFrontAnchorMm,
                BarVertBack = this.RebarStemVertBack,
                CvVertBackMm = this.CoverStemVertBackMm,
                CvVertBackZMm = this.CoverStemVertBackZMm,
                LayoutVertBack = this.LayoutStemVertBack,
                SpVertBackMm = this.SpaceStemVertBackMm,
                QtyVertBack = this.QtyStemVertBack,
                AutoVertBackHeight = this.IsAutoStemVertBackHeight,
                VertBackHeightMm = this.StemVertBackHeightMm,
                VertBackLongOffsetMm = this.StemVertBackLongOffsetMm,
                AnchorBackMm = this.StemVertBackAnchorMm,
                BarHoriz = this.RebarStemHoriz,
                HorizPosRelToVert = this.HorizPosRelToVert,
                CvHorizXMm = this.CoverStemHorizXMm,
                CvHorizYMm = this.CoverStemHorizYMm,
                CvHorizZMm = this.CoverStemHorizZMm,
                LayoutHoriz = this.LayoutStemHoriz,
                SpHorizMm = this.SpaceStemHorizMm,
                QtyHoriz = this.QtyStemHoriz,
                BarTie = this.RebarStemTie,
                CvTieYMm = this.CoverStemTieYMm,
                LayoutTieV = this.LayoutStemTieV,
                SpTieVMm = this.SpaceStemTieVMm,
                QtyTieV = this.QtyStemTieV,
                LayoutTieH = this.LayoutStemTieH,
                SpTieHMm = this.SpaceStemTieHMm,
                QtyTieH = this.QtyStemTieH,
                TieShapeName = this.StemTieShapeName,
                TieZMm = this.StemTieZMm,
                TieHookDirection = this.TieHookDirection,
                TieDropMm = this.StemTieDropMm
            };
        }
    }
}



