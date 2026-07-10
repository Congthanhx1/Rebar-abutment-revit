using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vetheprevit.MoCau
{
    public class FootingRebarExportSelectionWindow : Window
    {
        private readonly Dictionary<string, CheckBox> _checkBoxes =
            new Dictionary<string, CheckBox>();

        public ISet<string> SelectedGroupCodes { get; private set; } =
            new HashSet<string>();

        public FootingRebarExportSelectionWindow(
            IDictionary<string, string> namesByGroup)
        {
            Title = "Chọn nhóm thép xuất Excel";
            Width = 430;
            Height = 590;
            MinWidth = 380;
            MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");

            Grid root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock title = new TextBlock
            {
                Text = "Chọn các nhóm thép bệ mố cần thống kê",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(title);

            StackPanel actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Button selectAll = CreateButton("Chọn tất cả");
            selectAll.Click += (sender, args) => SetAll(true);
            Button clearAll = CreateButton("Bỏ tất cả");
            clearAll.Click += (sender, args) => SetAll(false);
            actions.Children.Add(selectAll);
            actions.Children.Add(clearAll);
            Grid.SetRow(actions, 1);
            root.Children.Add(actions);

            StackPanel list = new StackPanel { Margin = new Thickness(4) };
            foreach (ExportGroupDefinition group in GetGroups())
            {
                string displayName = namesByGroup != null &&
                                     namesByGroup.TryGetValue(group.Code, out string name) &&
                                     !string.IsNullOrWhiteSpace(name)
                    ? name.Trim()
                    : group.Code;

                CheckBox checkBox = new CheckBox
                {
                    Content = $"{displayName} — {group.Description}",
                    IsChecked = true,
                    Margin = new Thickness(2, 6, 2, 6),
                    FontSize = 13
                };
                _checkBoxes[group.Code] = checkBox;
                list.Children.Add(checkBox);
            }

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = list,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
            Grid.SetRow(scrollViewer, 2);
            root.Children.Add(scrollViewer);

            StackPanel footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button cancel = CreateButton("Hủy");
            cancel.Click += (sender, args) =>
            {
                DialogResult = false;
                Close();
            };
            Button export = CreateButton("Tiếp tục xuất");
            export.Background = new SolidColorBrush(Color.FromRgb(29, 78, 216));
            export.Foreground = Brushes.White;
            export.Click += Export_Click;
            footer.Children.Add(cancel);
            footer.Children.Add(export);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            SelectedGroupCodes = new HashSet<string>(
                _checkBoxes
                    .Where(entry => entry.Value.IsChecked == true)
                    .Select(entry => entry.Key));

            if (SelectedGroupCodes.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất một nhóm thép.",
                    "Chưa chọn nhóm thép",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void SetAll(bool isChecked)
        {
            foreach (CheckBox checkBox in _checkBoxes.Values)
                checkBox.IsChecked = isChecked;
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 90
            };
        }

        private static IEnumerable<ExportGroupDefinition> GetGroups()
        {
            yield return new ExportGroupDefinition("VT_BotLong", "Thép đáy phương dọc");
            yield return new ExportGroupDefinition("VT_BotTrans", "Thép đáy phương ngang");
            yield return new ExportGroupDefinition("VT_TopLong", "Thép đỉnh phương dọc");
            yield return new ExportGroupDefinition("VT_TopTrans", "Thép đỉnh phương ngang");
            yield return new ExportGroupDefinition("VT_VertSideX", "Thép hông đứng phương X");
            yield return new ExportGroupDefinition("VT_VertSideY", "Thép hông đứng phương Y");
            yield return new ExportGroupDefinition("VT_AntiBurstX", "Chống nở hông phương X");
            yield return new ExportGroupDefinition("VT_AntiBurstY", "Chống nở hông phương Y");
            yield return new ExportGroupDefinition("VT_Dowel", "Thép chờ thân mố");
            yield return new ExportGroupDefinition("VT_HorizSide", "Thép dọc hông");
        }

        private class ExportGroupDefinition
        {
            public string Code { get; }
            public string Description { get; }

            public ExportGroupDefinition(string code, string description)
            {
                Code = code;
                Description = description;
            }
        }
    }
}
