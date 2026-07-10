using System;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;

namespace Vetheprevit.MoCau
{
    public partial class TiltRebarWindow : Window
    {
        public Func<TiltRebarWindow, Task> OnPickRebarA { get; set; }
        public Func<TiltRebarWindow, Task> OnPickRebarB { get; set; }
        public Func<TiltRebarWindow, Task> OnRunTilt { get; set; }

        public ElementId RebarAId { get; private set; }
        public ElementId RebarBId { get; private set; }
        public bool IsAAboveB => RadAAbove.IsChecked == true;

        public TiltRebarWindow()
        {
            InitializeComponent();
            UpdateRunState();
        }

        public void SetRebarA(ElementId id, string label)
        {
            RebarAId = id;
            TxtRebarA.Text = label;
            TxtRebarA.Foreground = System.Windows.Media.Brushes.DarkGreen;
            SetStatus("Đã chọn thanh A.");
            UpdateRunState();
        }

        public void SetRebarB(ElementId id, string label)
        {
            RebarBId = id;
            TxtRebarB.Text = label;
            TxtRebarB.Foreground = System.Windows.Media.Brushes.DarkGreen;
            SetStatus("Đã chọn thanh B.");
            UpdateRunState();
        }

        public void SetStatus(string message)
        {
            TxtStatus.Text = message;
        }

        private async void BtnPickA_Click(object sender, RoutedEventArgs e)
        {
            if (OnPickRebarA != null)
                await OnPickRebarA(this);
        }

        private async void BtnPickB_Click(object sender, RoutedEventArgs e)
        {
            if (OnPickRebarB != null)
                await OnPickRebarB(this);
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (OnRunTilt != null)
                await OnRunTilt(this);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateRunState()
        {
            BtnRun.IsEnabled = RebarAId != null && RebarBId != null;
        }
    }
}
