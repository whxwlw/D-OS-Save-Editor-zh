//#define AVOIDUNPACK
//#define LOG_ITEMXML
//#define LOAD_FROM_JSON

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using D_OS_Save_Editor.Annotations;
using LSLib.LS;
using LSLib.LS.Enums;
using static System.IO.Path;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace D_OS_Save_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly string _defaultProfileDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}{DirectorySeparatorChar}Larian Studios{DirectorySeparatorChar}Divinity Original Sin Enhanced Edition{DirectorySeparatorChar}PlayerProfiles";
        private enum BackupStatus { None, Current, Old, NoChecksum, NoImage }

        public static string Version { get; } = "v1.6.0";
        private BackgroundWorker _getMetaBackgroundWorker;
        private string _updateLink;
        public MainWindowData MainWindowData;

        public MainWindow()
        {
            InitializeComponent();

            MainWindowData = new MainWindowData();
            DataContext = MainWindowData;

#if LOAD_FROM_JSON
            var se = new SaveEditor(@"E:\Documents\Visual Studio 2017\Projects\D-OS SE\D-OS Save Editor\test\SaveGame_180404_120803.json");
            //var se = new SaveEditor(@"E:\Documents\Visual Studio 2017\Projects\D-OS SE\D-OS Save Editor\test\SaveGame180403_011306.json");
            se.Show();
            this.Visibility = Visibility.Hidden;
#endif
            // set default savegame directory
            var dir = GetMostRecentProfile();
            if (dir != null)
                DirectoryTextBox.Text = dir;

            // update
            UpdatePanel.Visibility = Visibility.Collapsed;
            CheckUpdate();
        }

        #region private methods
        /// <summary>
        /// Checks for update on github
        /// </summary>
        private async Task CheckUpdate()
        {
            Debug.WriteLine("start");
            const string urlAddress = "https://github.com/tmxkn1/D-OS-Save-Editor/blob/master/UpdateCheck";
            _updateLink = null;
            UpdatePanel.Visibility = Visibility.Collapsed;

            var request = WebRequest.Create(urlAddress);

            using (var response = await request.GetResponseAsync())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream))
                    {
                        var data = await reader.ReadToEndAsync();
                        
                        // handshake fail
                        if (!data.Contains("HandShake={ABCQWEZXCrtyfghvbnUIOJKLNM}"))
                            return;
                        // app is the latest
                        if (data.Contains($"LatestVersion={{{Version}}}"))
                            return;

                        // app is outdated
                        var reg = new Regex(@"Link=linkStart\{(.*)\}linkEnd");
                        var matches = reg.Matches(data);
                        if (matches.Count <= 0) return;
                        if (matches[0].Groups.Count <= 1) return;
                        // link found
                        _updateLink = matches[0].Groups[1].Value;

                        // message
                        reg = new Regex(@"Msg=msgStart\{(.*)\}msgEnd");
                        matches = reg.Matches(data);
                        var msg = "有新版本可用！";

                        if (matches.Count > 0)
                            if (matches[0].Groups.Count <= 2)
                                msg = matches[0].Groups[1].Value;

                        UpdateTextBox.Text = msg;
                        UpdatePanel.Visibility = Visibility.Visible;
                    }
                }
            }
            Debug.WriteLine("done");
        }

        /// <summary>
        /// get the user profile that is access most recently
        /// </summary>
        /// <returns>full path to "Savegames_patch"</returns>
        private string GetMostRecentProfile()
        {
            if (!Directory.Exists(_defaultProfileDir))
                return null;

            var dirs = Directory.GetDirectories(_defaultProfileDir);
            var idx = 0;
            var saveTime = DateTime.MinValue;
            for (var i = 0; i < dirs.Length; i++)
            {
                var saveDir = dirs[i] + DirectorySeparatorChar + "Savegames_patch";
                if (!Directory.Exists(saveDir)) continue;
                var saveDirs = Directory.GetDirectories(saveDir);
                foreach (var d in saveDirs)
                {
                    var lastWriteTime = Directory.GetLastWriteTime(d);
                    if (lastWriteTime <= saveTime) continue;
                    saveTime = lastWriteTime;
                    idx = i;
                }
            }

            return dirs[idx] + DirectorySeparatorChar + "Savegames_patch";
        }

        /// <summary>
        /// load all savegames to list box
        /// </summary>
        /// <param name="dir">full path to Savegames_path</param>
        private void LoadSavegamesPath(string dir)
        {
            // clear ui
            GameEditionTextBlock.Text = "";
            SavegameListBox.Items.Clear();

            if (!Directory.Exists(dir)) return;

            // get game version
            var gameVer = GetGameVersion(dir);
            if (gameVer == null)
            {
                MessageBox.Show(this, "无法识别的游戏版本。请检查您输入的存档路径是否正确。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            GameEditionTextBlock.Text = Regex.Replace(gameVer.ToString(), "([DOS2]|E{2})", " $1").Trim();
            GameEditionTextBlock.Tag = gameVer;

            var dirs = Directory.GetDirectories(dir);
            // find save time and save names
            var fnames = new List<string>();
            var dates = new List<DateTime>();
            foreach (var d in dirs)
            {
                // check if the folder contains a save by looking for the lsv file of the same fname
                var fname = d.Split(DirectorySeparatorChar).Last();
                if (!File.Exists(d + DirectorySeparatorChar + fname + ".lsv"))
                    continue;

                fnames.Add(fname);
                dates.Add(Directory.GetLastWriteTime(d));
            }

            // order by time
            var idx = Enumerable.Range(0, dates.Count).ToArray();
            Array.Sort(idx, (a, b) => dates[b].CompareTo(dates[a]));

            // add to list box
            for (var i = 0; i < dates.Count; i++)
            {
                SavegameListBox.Items.Add(new TextBlock
                {
                    Text = String.Join(" ", fnames[idx[i]].Split('_')),
                    Uid = fnames[idx[i]],
                    Tag = dates[idx[i]].ToString("dd/MM/yyyy|HH:mm:ss")
                });
            }
        }

        /// <summary>
        /// checks for game version
        /// </summary>
        /// <param name="path">savegame directory</param>
        /// <returns>game version</returns>
        private static Game? GetGameVersion(string path)
        {
            if (path.Contains("Divinity Original Sin Enhanced Edition")) return Game.DivinityOriginalSinEE;
            if (path.Contains("Divinity Original Sin 2")) return Game.DivinityOriginalSin2;
            if (path.Contains("Divinity Original Sin")) return Game.DivinityOriginalSin;

            return null;
        }

        private async Task LoadSavegame()
        {
            LoadButton.IsEnabled = false;

            if (SavegameListBox.SelectedItem == null) return;

            var unpackDir = GetTempPath() + "DOSSE" + DirectorySeparatorChar + "unpackaged";
            var saveGameName = ((TextBlock)SavegameListBox.SelectedItem).Uid;

            // check backup
            switch (IsBackedUp(saveGameName))
            {
                case BackupStatus.None:
                    var dlgResult = MessageBox.Show(this, "该存档尚未备份。是否要首先创建备份？",
                        "未找到备份。", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.Yes)
                        BackupSavegame(saveGameName);
                    break;
                case BackupStatus.Current:
                    break;
                case BackupStatus.Old:
                    dlgResult = MessageBox.Show(this,
                        "备份文件似乎已经过时，因为它未通过校验和验证。是否要创建新的备份？",
                        "发现旧备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.Yes)
                        BackupSavegame(saveGameName);
                    break;
                case BackupStatus.NoChecksum:
                    dlgResult = MessageBox.Show(this,
                        "备份文件可能已经过时，因为它没有校验和文件。是否要创建新的备份？",
                        "缺少校验和文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.No) return;
                    else break;
                case BackupStatus.NoImage:
                    dlgResult = MessageBox.Show(this,
                        "备份文件可能已经过时，因为它没有图像文件。是否要创建新的备份？",
                        "缺少图像文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.No) return;
                    else break;
            }

            var savegame = new Savegame(
            DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar + saveGameName + ".lsv",
            unpackDir,
            (Game)GameEditionTextBlock.Tag);

            // unpack
            var progressIndicator = new ProgressIndicator($"正在加载 {saveGameName}", false) { Owner = Application.Current.MainWindow };
            var progress = new Progress<string>();
            progress.ProgressChanged += (o, s) =>
            {
                progressIndicator.ProgressText = s;
            };

            if (_getMetaBackgroundWorker.IsBusy)
                progressIndicator.ProgressText = "正在等待元信息...";

            progressIndicator.Show();

            while (_getMetaBackgroundWorker.IsBusy)
                await Task.Delay(5);

            // TODO DNSA (Do Not Show Again) message box 
            // version check
            if (MainWindowData.Meta.IsOutdatedVersion)
            {
                var dlgResult = MessageBox.Show(this,
                    $"您的游戏版本似乎与此存档编辑器设计的版本（{DataTable.SupportedGameVersion}）不同。因此，对存档所做的修改可能会导致存档损坏。\n\n请确保在继续之前创建备份。",
                    "游戏版本不兼容", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (dlgResult == MessageBoxResult.Cancel)
                {
                    progressIndicator.Close();
                    return;
                }

            }
            // mod check
            if (MainWindowData.Meta.IsModWarning)
            {
                var dlgResult = MessageBox.Show(this,
                    "您似乎使用了模组。因此，对存档所做的修改可能会导致存档损坏。\n\n请确保在继续之前创建备份。",
                    "发现模组", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (dlgResult == MessageBoxResult.Cancel)
                {
                    progressIndicator.Close();
                    return;
                }
            }

            var unpackTask = await UnpackSaveAsync(savegame, progress);
            progressIndicator.Close();
            if (!unpackTask) return;

            SaveEditor se;
            try
            {
                se = new SaveEditor(savegame) { Owner = Application.Current.MainWindow };
            }
            catch
            {
                return;
            }
            se.ShowDialog();
        }

        /// <summary>
        /// Unpacks lsv files
        /// </summary>
        /// <param name="savegame">Savegame object</param>
        /// <param name="progress">Progress text to be updated in Progress Indicator UI</param>
        /// <returns>true for successful, otherwise fail</returns>
        private async Task<bool> UnpackSaveAsync(Savegame savegame, IProgress<string> progress)
        {
            try
            {
                Cursor = Cursors.Wait;
#if (!DEBUG || !AVOIDUNPACK)
                await savegame.UnpackSavegameAsync(progress);
#endif
                await savegame.ParseLsxAsync(progress);
            }
            catch (NotAPackageException)
            {
                MessageBox.Show(this, $"指定的包文件（{savegame.SavegameFullFile}）不是存档文件。",
                    "失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                var er = new ErrorReporting($"内部错误！\n\n{ex}", null);
                er.ShowDialog();
                return false;
            }
            finally
            {
                Cursor = Cursors.Arrow;
                LoadButton.IsEnabled = true;
            }
            return true;
        }

        /// <summary>
        /// compute checksum from savegame snapshot (the png file in the savegame folder)
        /// </summary>
        /// <param name="imagefile">snapshot path + name</param>
        /// <returns>checksum</returns>
        private string CalculateChecksumFromPng(string imagefile)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(imagefile))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// check if a back up exists
        /// </summary>
        /// <param name="saveGameName">savegame name, excluding path</param>
        /// <returns>backup stats</returns>
        private BackupStatus IsBackedUp(string saveGameName)
        {
            var backupfile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                             saveGameName + ".bak";
            var imagefile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                            saveGameName + ".png";
            var checksumfile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                               saveGameName + ".cks";

            // backup file not exist
            if (!File.Exists(backupfile)) return BackupStatus.None;

            // checksum not exist
            if (!File.Exists(checksumfile))
            {
                return BackupStatus.NoChecksum;
            }

            // image not exist
            if (!File.Exists(imagefile))
            {
                return BackupStatus.NoImage;
            }

            // compare checksum
            string cks;
            using (var sr = new StreamReader(checksumfile))
            {
                cks = sr.ReadToEnd().Trim();
            }

            return cks != CalculateChecksumFromPng(imagefile) ? BackupStatus.Old : BackupStatus.Current;
        }

        /// <summary>
        /// create a backup of the savegame
        /// </summary>
        /// <param name="saveGameName">savegame name excluding path</param>
        private void BackupSavegame(string saveGameName)
        {
            var savegamefile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                               saveGameName + ".lsv";
            var backupfile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                             saveGameName + ".bak";
            var imagefile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                            saveGameName + ".png";
            var checksumfile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                               saveGameName + ".cks";

            File.Copy(savegamefile, backupfile, true);

            // generate checksum 
            var isNoImage = false;
            if (!File.Exists(imagefile))
            {
                isNoImage = true;
                Properties.Resources.NoImage.Save(imagefile);
            }
            using (var sw = new StreamWriter(checksumfile))
            {
                sw.WriteLine(CalculateChecksumFromPng(imagefile));
            }

            MessageBox.Show(this, isNoImage? "备份成功！\n\n但未找到存档截图，已使用空白图像计算校验和。" : "备份成功！", "成功");
        }
        #endregion private methods

        #region ui events
        private void DirectoryTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            LoadSavegamesPath(DirectoryTextBox.Text);
        }

        private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
        {
            var initialDir = Directory.Exists(DirectoryTextBox.Text)
                ? DirectoryTextBox.Text
                : $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}{DirectorySeparatorChar}Larian Studios";
            if (!Directory.Exists(initialDir))
            {
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            var dialog = new CommonOpenFileDialog
            {
                InitialDirectory = initialDir,
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                DirectoryTextBox.Text = dialog.FileName;
            }
        }

        private async void SavegameListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || ((ListBox) sender).Items.Count < 1)
            {
                LoadButton.IsEnabled = false;
                BackupButton.IsEnabled = false;
                RestoreButton.IsEnabled = false;
                SavegameImage.Source = null;
                return;
            }
            
            LoadButton.IsEnabled = true;
            BackupButton.IsEnabled = true;
            RestoreButton.IsEnabled = true;
            var saveGameName = ((TextBlock) e.AddedItems[e.AddedItems.Count-1]).Uid;
            var saveGameNameWithoutExt = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                               saveGameName;
            var imageDir = saveGameNameWithoutExt + ".png";
            if (File.Exists(imageDir))
            {
                SavegameImage.Source = new BitmapImage(new Uri(imageDir));
            }

            var savegameTemp = new Savegame(saveGameNameWithoutExt + ".lsv",
                GetTempPath() + "DOSSE" + DirectorySeparatorChar + "temp", 
                (Game)GameEditionTextBlock.Tag);
            if (_getMetaBackgroundWorker == null)
            {
                _getMetaBackgroundWorker = new BackgroundWorker {WorkerSupportsCancellation = true};
                _getMetaBackgroundWorker.RunWorkerCompleted += (o, args) =>
                {
                    if (args.Cancelled || args.Error != null || args.Result == null) return;
                    MainWindowData.Meta = (Meta) args.Result;
                    _getMetaBackgroundWorker.Dispose();
                };
            }

            if (_getMetaBackgroundWorker.IsBusy)
                _getMetaBackgroundWorker.CancelAsync();
            while (_getMetaBackgroundWorker.IsBusy)
                await Task.Delay(5);

            _getMetaBackgroundWorker.DoWork += savegameTemp.GetMetaBackgroundWorker;
            MainWindowData.Meta = new Meta();
            _getMetaBackgroundWorker.RunWorkerAsync();
        }

        private async void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadSavegame();
        }

        private void BackupButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (SavegameListBox.SelectedItem == null) return;

            var saveGameName = ((TextBlock)SavegameListBox.SelectedItem).Uid;
            BackupSavegame(saveGameName);
        }

        private void RestoreButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (SavegameListBox.SelectedItem == null) return;

            var saveGameName = ((TextBlock)SavegameListBox.SelectedItem).Uid;

            var savegamefile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                               saveGameName + ".lsv";
            var backupfile = DirectoryTextBox.Text + DirectorySeparatorChar + saveGameName + DirectorySeparatorChar +
                             saveGameName + ".bak";

            switch (IsBackedUp(saveGameName))
            {
                case BackupStatus.None:
                    MessageBox.Show("未找到备份。");
                    return;
                case BackupStatus.Current:
                    break;
                case BackupStatus.Old:
                    var dlgResult = MessageBox.Show(this,
                        "备份文件未通过校验和验证。是否仍要恢复存档？该备份可能是旧存档。",
                        "校验和失败", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.No) return;
                    else break;
                case BackupStatus.NoChecksum:
                    dlgResult = MessageBox.Show(this,
                        "未找到校验和文件。是否仍要恢复存档？该备份可能是旧存档。",
                        "缺少校验和文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.No) return;
                    else break;
                case BackupStatus.NoImage:
                    dlgResult = MessageBox.Show(this,
                        "未找到图像文件。是否仍要恢复存档？该备份可能是旧存档。",
                        "缺少图像文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dlgResult == MessageBoxResult.No) return;
                    else break;
            }

            File.Copy(backupfile, savegamefile, true);
            MessageBox.Show(this, "恢复成功！", "成功");
        }
        #endregion ui events

        private void AboutButton_OnClick(object sender, RoutedEventArgs e)
        {
            var about = new About { Owner = Application.Current.MainWindow};
            about.ShowDialog();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            LoadSavegamesPath(DirectoryTextBox.Text);
            var tooltip = new ToolTip { Content = "已刷新！" };
            RefreshButton.ToolTip = tooltip;
            tooltip.Opened += async delegate (object o, RoutedEventArgs args)
            {
                var s = o as ToolTip;
                await Task.Delay(1000);
                s.IsOpen = false;
                await Task.Delay(1000);
                s.Content = "刷新存档列表";
            };
            tooltip.IsOpen = true;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RoutedEventArgs e)
        {
            if (((Hyperlink) sender).Tag as string == "update")
            {

            }
            else if (((Hyperlink) sender).Tag as string == "site")
            {
                Process.Start(_updateLink);
            }
        }

        private void DismissButton_OnClick(object sender, RoutedEventArgs e)
        {
            UpdatePanel.Visibility = Visibility.Collapsed;
        }

        private void BugReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(
                "https://docs.google.com/forms/d/e/1FAIpQLSeUeKYdV8InQslbvCvA1rmffJ5t1ieond4W6hpUHkHTH7I7dg/viewform");
        }
    }

    public class MainWindowData: INotifyPropertyChanged
    {
        private Meta _meta;

        public Meta Meta
        {
            get => _meta;
            set
            {
                if (Equals(value, _meta)) return;
                _meta = value;
                OnPropertyChanged();
            }
        }

        public MainWindowData()
        {
            Meta = new Meta();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
