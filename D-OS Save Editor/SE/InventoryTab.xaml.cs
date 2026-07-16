using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace D_OS_Save_Editor
{
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    public partial class InventoryTab
    {
        private Player _player;

        /// <summary>
        /// Tag used to mark list rows that represent queued (not-yet-saved) item additions.
        /// These rows are display-only and are not backed by an entry in Player.Items.
        /// </summary>
        private const string PendingAddTag = "PendingAdd";

        private Brush DefaultTextBoxBorderBrush { get; }
        private Brush[] _itemRarityColor =
            {Brushes.Black, Brushes.ForestGreen, Brushes.DodgerBlue, Brushes.BlueViolet, Brushes.DeepPink, Brushes.Gold, Brushes.DimGray};

        public Player Player
        {
            get => _player;

            set
            {
                _player = value;
                UpdateForm();
            }
        }

        public InventoryTab()
        {
            InitializeComponent();
            DefaultTextBoxBorderBrush = AmountTextBox.BorderBrush;

            RarityComboBox.ItemsSource = Enum.GetValues(typeof(Item.ItemRarityType)).Cast<Item.ItemRarityType>();
        }

        public void UpdateForm()
        {
            ItemsListBox.Items.Clear();
            foreach (var i in Player.Items)
             ItemsListBox.Items.Add(new ListBoxItem
                {
                    Content = ItemChineseNames.Map.ContainsKey(i.StatsName) ? ItemChineseNames.Map[i.StatsName] : i.StatsName,
                    Tag = i.ItemSort,
                    DataContext = i,
                    Foreground = _itemRarityColor[(int)i.ItemRarity]
                });

            // show queued (not-yet-saved) item additions so they're visible this session.
            // These rows are display-only; their index is past the end of Player.Items.
            foreach (var ic in Player.ItemChanges)
            {
                if (ic.Value.ChangeType != ChangeType.Add || ic.Value.ItemTemplate == null)
                    continue;
                ItemsListBox.Items.Add(new ListBoxItem
                {
                    Content = $"{ic.Value.ItemTemplate.Name}  (pending add — {ic.Value.ItemTemplate.ItemRarity})",
                    Tag = PendingAddTag,
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic
                });
            }

            // check filter
            foreach (var i in ShowWrapPanel.Children)
            {
                if (i is CheckBox)
                    CheckboxEventSetter_OnClick(i, new RoutedEventArgs());
            }

            // clear all text boxes
            foreach(var i in ValueWrapPanel.Children)
            {
                if (i is TextBox t)
                    t.Text = "";
            }
        }

        private void TextBoxEventSetter_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox s)) return;
            if (s.Uid == "SearchText") return;

            var text = s.Text;
            var valid = int.TryParse(text, out int _);
            s.BorderBrush = !valid ? Brushes.Red : DefaultTextBoxBorderBrush;
        }

        private void TextBoxEventSetter_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextBox s)) return;
            if (s.Uid == "SearchText") return;

            var text = s.Text.Insert(s.SelectionStart, e.Text);
            e.Handled = !int.TryParse(text, out int _);
        }

        private void ItemsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // clear list boxes
            BoostsListBox.Items.Clear();
            PermBoostsListBox.Items.Clear();

            var lb = sender as ListBox;
            if (lb.SelectedIndex < 0)
                return;
            // pending-add rows live past the end of Player.Items and aren't editable here
            if (lb.SelectedIndex >= Player.Items.Length)
            {
                DisableAllItemControls();
                return;
            }
            var item = Player.Items[lb.SelectedIndex];


            var allowedChanges = item.GetAllowedChangeType();
            #region enable disable controls

            if (allowedChanges.Contains(nameof(item.Vitality)))
            {
                VitalityTextBox.IsEnabled = true;
                MaxVitalityPatchCheckTextBox.IsEnabled = true;
            }
            else
            {
                VitalityTextBox.IsEnabled = false;
                MaxVitalityPatchCheckTextBox.IsEnabled = false;
            }

            RarityComboBox.IsEnabled = allowedChanges.Contains(nameof(item.ItemRarity));
            AmountTextBox.IsEnabled = allowedChanges.Contains(nameof(item.Amount));
            LockLevelTextBox.IsEnabled = allowedChanges.Contains(nameof(item.LockLevel));
            BoostsListBox.IsEnabled = allowedChanges.Contains(nameof(item.Generation));

            if (allowedChanges.Contains(nameof(item.Stats)))
            {
                DurabilityTextBox.IsEnabled = true;
                MaxDurabilityPatchCheckTextBox.IsEnabled = false;
                DurabilityCounterTextBox.IsEnabled = true;
                RepairDurabilityPenaltyTextBox.IsEnabled = true;
                LevelTextBox.IsEnabled = true;
                PermBoostsListBox.IsEnabled = true;
            }
            else
            {
                DurabilityTextBox.IsEnabled = false;
                MaxDurabilityPatchCheckTextBox.IsEnabled = false;
                DurabilityCounterTextBox.IsEnabled = false;
                RepairDurabilityPenaltyTextBox.IsEnabled = false;
                LevelTextBox.IsEnabled = false;
                PermBoostsListBox.IsEnabled = false;
            }
            #endregion

#if DEBUG && LOG_ITEMXML
            Console.WriteLine(item.Xml);
#endif
            // textbox contents
            AmountTextBox.Text = item.Amount;
            LockLevelTextBox.Text = item.LockLevel;
            VitalityTextBox.Text = item.Vitality;
            MaxVitalityPatchCheckTextBox.Text = item.MaxVitalityPatchCheck;
            // For equipment that has no Stats node yet, durability editing is still allowed
            // (a Stats node is synthesized on apply). Pre-fill sensible defaults so the
            // fields aren't blank — a blank durability would otherwise fail validation.
            var canEditStats = allowedChanges.Contains(nameof(item.Stats));
            DurabilityTextBox.Text = item.Stats?.Durability ?? (canEditStats ? "100" : "");
            DurabilityCounterTextBox.Text = item.Stats?.DurabilityCounter ?? (canEditStats ? "8" : "");
            MaxDurabilityPatchCheckTextBox.Text = item.MaxDurabilityPatchCheck;
            RepairDurabilityPenaltyTextBox.Text = item.Stats?.RepairDurabilityPenalty ?? (canEditStats ? "0" : "");
            LevelTextBox.Text = item.Stats?.Level ?? (canEditStats ? "1" : "");

            // combobox
            RarityComboBox.SelectedIndex = (int) item.ItemRarity;

            // generation
            if (item.Generation != null)
            {
                foreach (var m in item.Generation.Boosts)
                {
                    BoostsListBox.Items.Add(m);
                }
            }

            // stats
            if (item.Stats != null)
            {
                foreach (var m in item.Stats.PermanentBoost)
                {
                    PermBoostsListBox.Items.Add($"{m.Key} - {m.Value}");
                }
            }
        }
        
        /// <summary>
        /// Disable every per-item editor control. Used when a non-editable row
        /// (e.g. a queued pending-add) is selected.
        /// </summary>
        private void DisableAllItemControls()
        {
            VitalityTextBox.IsEnabled = false;
            MaxVitalityPatchCheckTextBox.IsEnabled = false;
            RarityComboBox.IsEnabled = false;
            AmountTextBox.IsEnabled = false;
            LockLevelTextBox.IsEnabled = false;
            BoostsListBox.IsEnabled = false;
            DurabilityTextBox.IsEnabled = false;
            MaxDurabilityPatchCheckTextBox.IsEnabled = false;
            DurabilityCounterTextBox.IsEnabled = false;
            RepairDurabilityPenaltyTextBox.IsEnabled = false;
            LevelTextBox.IsEnabled = false;
            PermBoostsListBox.IsEnabled = false;
        }

        private void CheckboxEventSetter_OnClick(object sender, RoutedEventArgs e)
        {
            var ckb = sender as CheckBox;
            if (!(ckb?.Tag is ItemSortType))
                return;

            if ((ItemSortType) ckb.Tag == ItemSortType.Other)
            {
                foreach (ListBoxItem i in ItemsListBox.Items)
                {
                    if (!(i.Tag is ItemSortType)) continue; // skip pending-add rows
                    if ((ItemSortType) i.Tag == ItemSortType.Item || (ItemSortType) i.Tag == ItemSortType.Unique ||
                        (ItemSortType) i.Tag == ItemSortType.Other)
                        i.Visibility = ckb.IsChecked == true && !IsFilteredOutByText(i) ? Visibility.Visible:Visibility.Collapsed;
                }
            }
            else
            {
                foreach (ListBoxItem i in ItemsListBox.Items)
                {
                    if (!(i.Tag is ItemSortType)) continue; // skip pending-add rows
                    if ((ItemSortType)i.Tag == (ItemSortType)ckb.Tag)
                        i.Visibility = ckb.IsChecked == true && !IsFilteredOutByText(i) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private bool IsFilteredOutByText(ListBoxItem item)
        {
            var searchTerms = SearchTextBox.Text.ToLower().Split(' ');
            if (searchTerms.Length == 0 || string.IsNullOrEmpty(searchTerms[0]))
                return false;

            var displayName = (item.Content as string)?.ToLower() ?? "";
            var statsName = (item.DataContext as Item)?.StatsName?.ToLower() ?? "";
            var combined = displayName + " " + statsName;

            foreach (var s in searchTerms)
            {
                if (!combined.Contains(s))
                    return true;
            }
            return false;
        }

        private void CheckAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var i in ShowWrapPanel.Children)
            {
                if (!(i is CheckBox box)) continue;
                box.IsChecked = true;
                CheckboxEventSetter_OnClick(i, new RoutedEventArgs());
            }
        }

        private void UncheckAllButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var i in ShowWrapPanel.Children)
            {
                if (!(i is CheckBox box)) continue;
                box.IsChecked = false;
                CheckboxEventSetter_OnClick(i, new RoutedEventArgs());
            }
        }

        private void ApplyChangesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedIndex < 0 || ItemsListBox.SelectedIndex >= Player.Items.Length)
                return;

            try
            {
                // apply changes to a copy of the item
                var item = Player.Items[ItemsListBox.SelectedIndex].DeepClone();
                var allowedChanges = item.GetAllowedChangeType();
                if (allowedChanges.Contains(nameof(item.Amount)))
                    item.Amount = AmountTextBox.Text;

                if (allowedChanges.Contains(nameof(item.LockLevel)))
                    item.LockLevel = LockLevelTextBox.Text;

                if (allowedChanges.Contains(nameof(item.Vitality)))
                {
                    item.Vitality = VitalityTextBox.Text;
                    item.MaxVitalityPatchCheck = MaxVitalityPatchCheckTextBox.Text;
                }
                
                if (allowedChanges.Contains(nameof(item.ItemRarity)))
                    item.ItemRarity = (Item.ItemRarityType) RarityComboBox.SelectedIndex;

                if (allowedChanges.Contains(nameof(item.Stats)))
                {
                    // Equipment may not have a Stats node yet; create one so its durability
                    // can be set. The save-time writer creates the matching XML node.
                    if (item.Stats == null)
                        item.Stats = new Item.StatsNode
                        {
                            Charges = "0",
                            PermanentBoost = new Dictionary<string, string>()
                        };
                    item.Stats.Durability = DurabilityTextBox.Text;
                    item.Stats.DurabilityCounter = DurabilityCounterTextBox.Text;
                    item.Stats.RepairDurabilityPenalty = RepairDurabilityPenaltyTextBox.Text;
                    item.Stats.Level = LevelTextBox.Text;
                }

                if (allowedChanges.Contains(nameof(item.Generation)))
                {
                    if (item.Generation == null)
                    {
                        // A modifier lives in the Generation node, and its Level is taken from
                        // Stats.Level. Some equipment has no Stats node yet; synthesize one
                        // (verified in-game schema, full durability) so the item can take a
                        // modifier and becomes durability-editable after the file is reopened.
                        if (item.Stats == null)
                        {
                            item.Stats = new Item.StatsNode
                            {
                                Durability = "100",
                                DurabilityCounter = "8",
                                RepairDurabilityPenalty = "0",
                                Level = "1",
                                Charges = "0",
                                PermanentBoost = new Dictionary<string, string>()
                            };
                        }
                        item.Generation = new Item.GenerationNode(item.StatsName, "0");
                    }

                    item.Generation.Boosts = new List<string>();
                    foreach (string s in BoostsListBox.Items)
                    {
                        item.Generation.Boosts.Add(s);
                    }
                }

                // add changes
                if (Player.ItemChanges.ContainsKey(item.Slot))
                {
                    Player.ItemChanges[item.Slot] = new ItemChange(item, Player.ItemChanges[item.Slot].ChangeType);
                }
                else
                {
                    Player.ItemChanges.Add(item.Slot,
                        new ItemChange(item, ChangeType.Modify));
                }

                // apply changes to the original item
                Player.Items[ItemsListBox.SelectedIndex] = item;

                // change colour
                ((ListBoxItem) ItemsListBox.Items[ItemsListBox.SelectedIndex]).Foreground =
                    _itemRarityColor[(int) item.ItemRarity];

                var tooltip = new ToolTip { Content = "Changes have been applied!" };
                ((Button) sender).ToolTip = tooltip;
                tooltip.Opened += async delegate (object o, RoutedEventArgs args)
                {
                    var s = o as ToolTip;
                    await Task.Delay(1000);
                    s.IsOpen = false;
                    await Task.Delay(1000);
                    ((Button)sender).ClearValue(ToolTipProperty);
                };
                tooltip.IsOpen = true;
            }
            catch (XmlValidationException ex)
            {
                MessageBox.Show($"Invalid value entered: {ex.Name}: {ex.Value}. No change has been applied.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Internal error. No change has been applied.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BoostsContextMenu_Click(object sender, RoutedEventArgs e)
        {
            switch (((MenuItem)sender).Header)
            {
                case "添加":
                    if (ItemsListBox.SelectedIndex < 0 || ItemsListBox.SelectedIndex >= Player.Items.Length)
                    {
                        MessageBox.Show("Select an existing item in the list first, then add a modifier. " +
                            "(Pending added items can't be modified until you save and reopen the file.)",
                            "No item selected", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
                    // try pre-determine equipment type
                    var predictedKeyword = "";
                    var boostsString= BoostsListBox.Items.Cast<string>().Aggregate("", (current, s) => current + s);
                    boostsString += (Player.Items[ItemsListBox.SelectedIndex].StatsName ?? "").ToLower();

                    if (boostsString!="")
                    foreach (var s in DataTable.GenerationBoostsFilterNames)
                    {
                        if (!boostsString.Contains(s)) continue;
                        switch (s)
                        {
                            case "arm":
                                predictedKeyword += "armor ";
                                break;
                            case "wpn":
                                predictedKeyword += "weapon ";
                                break;
                            default:
                                predictedKeyword += s + " ";
                                break;
                        }
                    }

                    var dlg=new AddBoostDialog(predictedKeyword);
                    dlg.ShowDialog();
                    if (dlg.DialogResult == true)
                        BoostsListBox.Items.Add(dlg.BoostText);
                    break;
                case "复制文本":
                    Clipboard.SetText((string)BoostsListBox.SelectedValue);
                    break;
                case "删除":
                    BoostsListBox.Items.RemoveAt(BoostsListBox.SelectedIndex);
                    break;
            }
        }

        private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateForm();
        }

    }
}
