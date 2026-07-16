using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace D_OS_Save_Editor
{
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    public partial class AddItemTab
    {
        private Player _player;
        private List<ItemTemplate> _NewItems;
        private List<ItemTemplate> _AddedItems;
        private ICollectionView _itemsView;

        /// <summary>
        /// Rarity options for the per-item picker (bound from XAML via x:Static).
        /// </summary>
        public static readonly Array RarityValues = Enum.GetValues(typeof(Item.ItemRarityType));

        public Player Player
        {
            get => _player;

            set
            {
                _player = value;
                UpdateForm();
            }
        }

        public List<ItemTemplate> NewItems
        {
            get => _NewItems;

            set
            {
                _NewItems = value;
                UpdateForm();
            }
        }

        public List<ItemTemplate> AddedItems
        {
            get => _AddedItems;

            set
            {
                _AddedItems = value;
                UpdateForm();
            }
        }

        public AddItemTab()
        {
            InitializeComponent();
            
            _AddedItems = new List<ItemTemplate>();

        }

        public void UpdateForm()
        {
            ItemsListBox.ItemsSource = _NewItems;

            SelectedItemsListbox.Items.Clear();

            if (_AddedItems != null)
            {
                foreach (var i in _AddedItems)
                    SelectedItemsListbox.Items.Add(i);
            }

            this._itemsView = CollectionViewSource.GetDefaultView(ItemsListBox.ItemsSource);

            _itemsView.Filter = UnifiedFilter;
            _itemsView.SortDescriptions.Clear();
            _itemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));


            _itemsView.Refresh();

            // check filter
            foreach (var i in ItemSortCheck.Children)
            {
                if (i is CheckBox) CheckboxEventSetter_OnClick(i, new RoutedEventArgs());
            }

        }

        private bool UnifiedFilter(object obj)
        {
            var item = obj as ItemTemplate;
            if (item == null)
                return false;

            if (IsFilteredOutByText(item.Name + " " + item.DisplayName))
                return false;

            if (!PassesCheckboxFilter(item))
                return false;

            return true;
        }

        private bool PassesCheckboxFilter(ItemTemplate item) 
        {
            foreach (CheckBox cb in ItemSortCheck.Children)
            {
                if ((ItemSortType)cb.Tag == ItemSortType.Other & cb.IsChecked == true)
                {
                    if (item.ItemSort == ItemSortType.Item ||
                        item.ItemSort == ItemSortType.Unique ||
                        item.ItemSort == ItemSortType.Other) return true;
                }

                if (cb.IsChecked == true & item.ItemSort == (ItemSortType)cb.Tag)
                {
                    return true;
                }

            }

            return false;

        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e) {

            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as ItemTemplate;
            if (item == null) return;

            item.Amount--;
            return;
        }

        private void IncrementButton_Click(object sender, RoutedEventArgs e)
        {

            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as ItemTemplate;
            if (item == null) return;

            item.Amount++;
            return;
        }

        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {

            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as ItemTemplate;
            if (item == null) return; 
            
            if(int.TryParse(item.MaxStack, out int i)) item.Amount = i;
            return;
        }

        private void ZeroButton_Click(object sender, RoutedEventArgs e)
        {

            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as ItemTemplate;
            if (item == null) return;

            _AddedItems.Remove(item);
            UpdateForm();
        }

        //private void TextBoxEventSetter_OnLostFocus(object sender, RoutedEventArgs e)
        //{
        //    if (!(sender is TextBox s)) return;
        //    if (s.Uid == "SearchText") return;

        //    var text = s.Text;
        //    var valid = int.TryParse(text, out int _);
        //    s.BorderBrush = !valid ? Brushes.Red : DefaultTextBoxBorderBrush;
        //}

        //private void TextBoxEventSetter_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        //{
        //    if (!(sender is TextBox s)) return;
        //    if (s.Uid == "SearchText") return;

        //    var text = s.Text.Insert(s.SelectionStart, e.Text);
        //    e.Handled = !int.TryParse(text, out int _);
        //}

        private void ItemsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            var lb = sender as ListBox;
            if (lb?.SelectedItem == null) return;

            var item = lb.SelectedItem as ItemTemplate;
            if (item == null) return;

            #if DEBUG
            Console.WriteLine("Item name: "+item.Name.ToString());
            Console.WriteLine("Item Mapkey: "+item.TemplateKey.ToString());
            if (item.Description != null) Console.WriteLine("Item Description: " + item.Description.ToString());
            else Console.WriteLine("Description Null");
            if (item.MaxStack != null) Console.WriteLine("Max Stack: " + item.MaxStack.ToString());
            else Console.WriteLine("MaxStack Null");
            #endif

            var itemToAdd = item.DeepClone();
            if (itemToAdd != null) 
            { 
                itemToAdd.Amount = 1;
                _AddedItems.Add(itemToAdd); 
            }
            UpdateForm();

        }

        private void ListBoxItem_Click(object sender, MouseButtonEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as ItemTemplate;
            if (item == null) return;

            #if DEBUG
            Console.WriteLine("Item name: " + item.Name.ToString());
            Console.WriteLine("Item Mapkey: " + item.TemplateKey.ToString());
            if (item.Description != null) Console.WriteLine("Item Description: " + item.Description.ToString());
            else Console.WriteLine("Description Null");
            if (item.MaxStack != null) Console.WriteLine("Max Stack: " + item.MaxStack.ToString());
            else Console.WriteLine("MaxStack Null");
            #endif

            var itemToAdd = item.DeepClone();
            if (itemToAdd != null)
            {
                itemToAdd.Amount = 1;
                _AddedItems.Add(itemToAdd);
            }
            UpdateForm();

        }

        private void CheckboxEventSetter_OnClick(object sender, RoutedEventArgs e)
        {
            _itemsView?.Refresh();
        }

        private bool IsFilteredOutByText(string itemName)
        {
            var isFilteredOut = false;
            itemName = itemName.ToLower();
            var searchTerms = SearchTextBox.Text.ToLower().Split(' ');
            foreach (var s in searchTerms)
            {
                if (itemName.Contains(s)) continue;

                isFilteredOut = true;
                break;
            }

            return isFilteredOut;
        }

        private void CheckAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var i in ItemSortCheck.Children)
            {
                if (!(i is CheckBox box)) continue;
                box.IsChecked = true;
            }

            _itemsView?.Refresh();
        }

        private void UncheckAllButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var i in ItemSortCheck.Children)
            {
                if (!(i is CheckBox box)) continue;
                box.IsChecked = false;
            }
            _itemsView?.Refresh();
        }

        private void ApplyChangesButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_AddedItems.Count < 1) return;
                foreach (var i in _AddedItems) 
                {
                    string slot = getEmptySlot(_player);
                    ItemTemplate temp = new ItemTemplate(i.Name,i.Description,i.TemplateKey,i.MaxStack,i.Stats,i.Amount);
                    temp.ItemRarity = i.ItemRarity;
                    ItemChange change = new ItemChange(temp,ChangeType.Add);
                    _player.ItemChanges.Add(slot,change);
                
                }

                _AddedItems.Clear();
                UpdateForm();

                var tooltip = new ToolTip { Content = "修改已应用！" };
                ((Button)sender).ToolTip = tooltip;
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
                MessageBox.Show($"输入的值无效：{ex.Name}：{ex.Value}。未应用任何修改。\n\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"内部错误。未应用任何修改。\n\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _itemsView?.Refresh();
        }

        public static string getEmptySlot(Player player)
        {

            int emptySlot = 20;
            while (emptySlot < 16300)
            {
                if (!player.SlotsOccupation[emptySlot])
                {
                    player.SlotsOccupation[emptySlot] = true;
                    return emptySlot.ToString();
                }
                else emptySlot++;
            }
            throw new Exception("Can't find empty slot");
        }
        // Get Chinese name for an item
        private string GetChineseName(ItemTemplate t)
        {
            if (t == null) return "";
            try
            {
                if (!string.IsNullOrEmpty(t.Stats) && ItemChineseNames.Map.ContainsKey(t.Stats))
                    return ItemChineseNames.Map[t.Stats];
            }
            catch { }
            return t.Name;
        }
    }
}
