using System.Windows.Controls;

namespace D_OS_Save_Editor
{
    /// <summary>
    /// Logique d'interaction pour GameInfoTab.xaml
    /// </summary>
    public partial class GameInfoTab
    {
        private readonly string[] _difficultyName = { "探索模式", "经典模式", "策略模式", "荣誉模式" };
        private readonly string[] _gameSavetypeName = { "手动存档", "快速存档", "自动存档", "荣誉模式存档" };

        private Meta _meta;

        public Meta Meta
        {
            get => _meta; set
            {
                _meta = value;
                UpdateForm();
            }
        }

        public GameInfoTab()
        {
            InitializeComponent();
        }

        public void UpdateForm()
        {
            DifficultyTextBlock.Text = _difficultyName[Meta.Difficulty];
            LevelTextBlock.Text = Meta.Level;
            SaveTimeTextBlock.Text = Meta.SavegameTimeString;
            SeedTextBlock.Text = Meta.Seed;
            SaveGameTypeTextBlock.Text = _gameSavetypeName[Meta.SavegameType];
            if (GameVersionListBox.Items.Count == 0)
                foreach (var i in Meta.GameVersions)
                {

                    GameVersionListBox.Items.Add(new ListBoxItem { Content = i });
                }

            if (ModsListBox.Items.Count == 0)
                foreach (var i in Meta.ModNames)
                {

                    ModsListBox.Items.Add(new ListBoxItem { Content = i });
                }
        }
    }


}
