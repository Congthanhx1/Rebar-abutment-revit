using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Vetheprevit.MoCau
{
    public class RebarNameItem
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public partial class RebarNamingWindow : Window
    {
        public Dictionary<string, string> NamingConfig { get; private set; }
        public ObservableCollection<RebarNameItem> RebarItems { get; set; }
        
        public RebarNamingWindow(Dictionary<string, string> existingConfig)
        {
            InitializeComponent();
            NamingConfig = existingConfig != null ? new Dictionary<string, string>(existingConfig) : new Dictionary<string, string>();
            RebarItems = new ObservableCollection<RebarNameItem>();
            DataContext = this;
            PopulateFields();
        }

        private void PopulateFields()
        {
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Đáy phương dọc", Key = "VT_BotLong", Value = GetVal("VT_BotLong", "B1") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Đáy phương ngang", Key = "VT_BotTrans", Value = GetVal("VT_BotTrans", "B2") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Đỉnh phương dọc", Key = "VT_TopLong", Value = GetVal("VT_TopLong", "B3") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Đỉnh phương ngang", Key = "VT_TopTrans", Value = GetVal("VT_TopTrans", "B4") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Hông đứng phương X", Key = "VT_VertSideX", Value = GetVal("VT_VertSideX", "B5") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Hông đứng phương Y", Key = "VT_VertSideY", Value = GetVal("VT_VertSideY", "B6") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Thép dọc hông", Key = "VT_HorizSide", Value = GetVal("VT_HorizSide", "B7") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Thép chờ", Key = "VT_Dowel", Value = GetVal("VT_Dowel", "B8") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Chống nở phương X", Key = "VT_AntiBurstX", Value = GetVal("VT_AntiBurstX", "B9") });
            RebarItems.Add(new RebarNameItem { Category = "BỆ MỐ", Description = "Chống nở phương Y", Key = "VT_AntiBurstY", Value = GetVal("VT_AntiBurstY", "B10") });

            RebarItems.Add(new RebarNameItem { Category = "THÂN MỐ", Description = "Thép đứng mặt trước", Key = "VT_StemVertFront", Value = GetVal("VT_StemVertFront", "T1") });
            RebarItems.Add(new RebarNameItem { Category = "THÂN MỐ", Description = "Thép đứng mặt sau", Key = "VT_StemVertBack", Value = GetVal("VT_StemVertBack", "T2") });
            RebarItems.Add(new RebarNameItem { Category = "THÂN MỐ", Description = "Thép ngang", Key = "VT_StemHoriz", Value = GetVal("VT_StemHoriz", "T3") });
            RebarItems.Add(new RebarNameItem { Category = "THÂN MỐ", Description = "Thép giằng / đai", Key = "VT_StemTie", Value = GetVal("VT_StemTie", "T4") });
        }

        private string GetVal(string key, string def)
        {
            if (NamingConfig.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;
            return def;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RebarItems)
            {
                NamingConfig[item.Key] = item.Value;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
