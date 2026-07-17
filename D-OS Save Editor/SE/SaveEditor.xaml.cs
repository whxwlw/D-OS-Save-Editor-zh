using LSLib.Granny.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace D_OS_Save_Editor
{
    /// <summary>
    /// Interaction logic for SaveEditor.xaml
    /// </summary>
    public partial class SaveEditor
    {
        private Savegame Savegame { get; set; }
        private Player[] EditingPlayers { get; set; }

        private List<ItemTemplate> GameItems { get; set; }

        public SaveEditor(string jsonFile)
        {
            InitializeComponent();
            GameItems = LoadNewItems();
            Savegame = Savegame.GetSavegameFromJson(jsonFile);
            // make a copy of players
            try
            {
                EditingPlayers = Savegame.Players.Select(a => a?.DeepClone()).ToArray();
            }
            catch (Exception ex)
            {
                var er = new ErrorReporting($"Fail to clone players.\n\n{ex}", null);
                er.ShowDialog();
                throw;
            }

            foreach (var p in Savegame.Players)
            {
                PlayerSelectionComboBox.Items.Add(p.Name);
            }
            
            PlayerSelectionComboBox.SelectedIndex = 0;
        }

        private List<ItemTemplate> LoadNewItems()
        {
            List<ItemTemplate> result = new List<ItemTemplate>();
            string path = Path.Combine(AppContext.BaseDirectory, "ItemTemplates");
            
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.lsx"))
                {
                    XElement items = XElement.Load(file);
                    IEnumerable<XElement> node = items.XPathSelectElement("//node[@id='root']//children").Elements();

                    foreach (var itemToAdd in node)
                    {

                        if (
                            (GetAttr(itemToAdd, "CanBePickedUp") == "True")  
                            & !(GetAttr(itemToAdd, "Stats") == null)
                            & !(GetAttr(itemToAdd, "MapKey") == null)
                            ) 
                        {
                            string name = GetAttr(itemToAdd, "Name");
                            string stats = GetAttr(itemToAdd, "Stats");
                            string description = GetAttr(itemToAdd, "Description");
                            string templateKey = GetAttr(itemToAdd, "MapKey");
                            string maxStack = GetAttr(itemToAdd, "maxStackAmount");

                            ItemTemplate item = new ItemTemplate(name, description, templateKey, maxStack, stats);

                            if (DataTable.GoldNames.Contains(item.Name.ToLower()))
                                item.ItemSort = ItemSortType.Gold;
                            else
                            {
                                var nameParts = item.Name.ToLower().Split('_');

                                if (nameParts[0] == "wpn" &&
                                    DataTable.ArrowTypeNames.Contains(nameParts[1]))
                                    item.ItemSort = ItemSortType.Arrow;
                                else
                                {
                                    if (nameParts.Length >= 2 && nameParts[0] == "book" && nameParts[1] == "skill")
                                        nameParts[0] = "skillbook";
                                    switch (nameParts[0])
                                    {
                                        case "item":
                                            item.ItemSort = ItemSortType.Item;
                                            break;
                                        case "potion":
                                            item.ItemSort = ItemSortType.Potion;
                                            break;
                                        case "arm":
                                            item.ItemSort = ItemSortType.Armor;
                                            break;
                                        case "wpn":
                                            item.ItemSort = ItemSortType.Weapon;
                                            break;
                                        case "skillbook":
                                            item.ItemSort = ItemSortType.Skillbook;
                                            break;
                                        case "book_skill":
                                            item.ItemSort = ItemSortType.Skillbook;
                                            break;
                                        case "scroll":
                                            item.ItemSort = ItemSortType.Scroll;
                                            break;
                                        case "grn":
                                            item.ItemSort = ItemSortType.Granade;
                                            break;
                                        case "food":
                                            item.ItemSort = ItemSortType.Food;
                                            break;
                                        case "fur":
                                            item.ItemSort = ItemSortType.Furniture;
                                            break;
                                        case "loot":
                                            item.ItemSort = ItemSortType.Loot;
                                            break;
                                        case "quest":
                                            item.ItemSort = ItemSortType.Quest;
                                            break;
                                        case "tool":
                                            item.ItemSort = ItemSortType.Tool;
                                            break;
                                        case "unique":
                                            item.ItemSort = ItemSortType.Unique;
                                            break;
                                        case "book":
                                            item.ItemSort = ItemSortType.Book;
                                            break;
                                        case "con":
                                            if (nameParts.Length >= 2 && nameParts[1] == "potion")
                                                item.ItemSort = ItemSortType.Potion;
                                            else
                                                item.ItemSort = ItemSortType.Food;
                                            break;
                                        default:
                                            item.ItemSort = ItemSortType.Other;
                                            break;
                                    }
                                }
                            }
                            result.Add(item); 
                        }

                    }

                }
            }

            return result;
        }
        string GetAttr(XElement el, string id) =>
          el.Descendants("attribute")
            .FirstOrDefault(a => (string)a.Attribute("id") == id)?
            .Attribute("value")?.Value;

        public SaveEditor(Savegame savegame)
        {
            InitializeComponent();
            Savegame = savegame;
            GameItems = LoadNewItems();
            Title = $"D-OS Save Editor: {savegame.SavegameName.Substring(0,savegame.SavegameName.Length-4)}";

            // make a copy of players
            try
            {
                EditingPlayers = Savegame.Players.Select(a => a?.DeepClone()).ToArray();
            }
            catch (Exception ex)
            {
                var er = new ErrorReporting($"Fail to clone players.\n\n{ex}", null);
                er.ShowDialog();
                throw;
            }

            foreach (var p in Savegame.Players)
            {
                PlayerSelectionComboBox.Items.Add(p.Name);
            }

            PlayerSelectionComboBox.SelectedIndex = 0;
        }

        private void ShowContent(int id)
        {
            StatsTab.Player = EditingPlayers[id];
            AbilitiesTab.Player = EditingPlayers[id];
            InventoryTab.Player = EditingPlayers[id];
            TraitsTab.Player = EditingPlayers[id];
            TalentTab.Player = EditingPlayers[id];
            AddItemTab.NewItems = GameItems;
            AddItemTab.Player = EditingPlayers[id];
            

            if (EditingPlayers[id].Name == "Henchman")
            {
                //TraitsTab.IsEnabled = false;
            }
        }

        private void PlayerSelectionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowContent(PlayerSelectionComboBox.SelectedIndex);
        }

        private void MainTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only react to the TabControl's own tab changes, not inner selectors bubbling up.
            if (!ReferenceEquals(e.OriginalSource, MainTabControl))
                return;

            // Refresh the inventory list when it becomes active so queued item
            // additions made on the Add Items tab show up immediately.
            if (InventoryTabItem.IsSelected)
                InventoryTab?.UpdateForm();
        }

        private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            try
            {
                Cursor = Cursors.Wait;
                StatsTab.SaveEdits();
                AbilitiesTab.SaveEdits();
                TraitsTab.SaveEdits();
                TalentTab.SaveEdits();

                // progress indicator
                var progressIndicator = new ProgressIndicator("Saving", false) { Owner = Application.Current.MainWindow};
                var progress = new Progress<string>();
                progress.ProgressChanged += (o, s) =>
                {
                    progressIndicator.ProgressText = s;
                };
                progressIndicator.Show();

                // apply changes
                Savegame.Players = EditingPlayers;
                await Savegame.WriteEditsToLsxAsync(progress);
                // pack up files
                await Savegame.PackSavegameAsync(progress);
                
                progressIndicator.ProgressText = "Successful.";
                progressIndicator.CanCancel = true;
                progressIndicator.CancelButtonText = "Close";

                DialogResult = true;
            }
            catch (Exception ex)
            {
                SaveButton.IsEnabled = true;
                var er = new ErrorReporting($"Failed to save changes.\n\n{ex}", null);
                er.ShowDialog();
            }
            finally
            {
                Cursor = Cursors.Arrow;
                SaveButton.IsEnabled = true;
            }
        }

        private void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            EditingPlayers = Savegame.Players.Select(a => a.DeepClone()).ToArray();
            StatsTab.UpdateForm();
            AbilitiesTab.UpdateForm();
            InventoryTab.UpdateForm();
            AddItemTab.UpdateForm();
        }

        private void SaveEditor_OnClosed(object sender, EventArgs e)
        {
            Savegame = null;
            EditingPlayers = null;
        }

        private void DebugButton_OnClick(object sender, RoutedEventArgs e)
        {
            switch (((Button)sender).Tag)
            {
                case "AllPlayer":
                    Savegame.DumpSavegame();
                    break;
                case "AllInv":
                    Savegame.DumpAllInventory();
                    break;
                case "AllMod":
                    Savegame.DumpAllModifiers();
                    break;
                case "AllPerBoost":
                    Savegame.DumpAllPermanentBoosts();
                    break;
                case "AllSkills":
                    Savegame.DumpAllSkills();
                    break;
                case "AllTalents":
                    Savegame.DumpAllTalents();
                    break;
            }

            MessageBox.Show("A dump file has been created. Thank you!");
        }

        
        private void SavePlayer_OnClick(object sender, RoutedEventArgs e)
        {
            SavePlayer.IsEnabled = false;
            try
            {
                StatsTab.SaveEdits();
                AbilitiesTab.SaveEdits();
                TraitsTab.SaveEdits();
                TalentTab.SaveEdits();

                MessageBox.Show(this, "Changes have been applied to the selected character.", "Successful");
            }
            catch (Exception ex)
            {
                SavePlayer.IsEnabled = true;
                var er = new ErrorReporting($"Failed to save changes.\n\n{ex}", null);
                er.ShowDialog();
            }
            finally
            {
                SavePlayer.IsEnabled = true;
            }
        }

        private void DismissButton_OnClick(object sender, RoutedEventArgs e)
        {
            SubmitPanel.Visibility = Visibility.Collapsed;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RoutedEventArgs e)
        {
        }

        private void SaveEditor_OnClosing(object sender, CancelEventArgs e)
        {
            //if ()
            //{
            //    var result = MessageBox.Show(this, "You have unsaved changes. Do you want to close the window now?",
            //        "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            //}
        }

        private void BugReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(
                "https://docs.google.com/forms/d/e/1FAIpQLSeUeKYdV8InQslbvCvA1rmffJ5t1ieond4W6hpUHkHTH7I7dg/viewform");
        }
    }
}
