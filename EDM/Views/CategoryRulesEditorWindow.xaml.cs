using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public class CategoryRuleViewModel
    {
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DefaultSubFolder { get; set; } = string.Empty;
        public string ExtensionsDisplay { get; set; } = string.Empty;
    }

    public partial class CategoryRulesEditorWindow : Window
    {
        public ObservableCollection<CategoryRuleViewModel> Categories { get; set; } = new();

        public CategoryRulesEditorWindow()
        {
            InitializeComponent();
            CategoryGrid.ItemsSource = Categories;
            LoadCategories();
        }

        private void LoadCategories()
        {
            Categories.Clear();
            var cats = DownloadCategoryRouter.Instance.GetCategories();
            foreach (var c in cats)
            {
                Categories.Add(new CategoryRuleViewModel
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    DefaultSubFolder = c.DefaultSubFolder,
                    ExtensionsDisplay = string.Join(", ", c.Extensions)
                });
            }
        }

        private void OnAddCategory(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string folder = FolderBox.Text.Trim();
            string exts = ExtsBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(exts))
            {
                System.Windows.MessageBox.Show("Please fill out Category Name, Folder, and Extensions.", "EDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string id = name.ToLowerInvariant().Replace(" ", "_");
            var extList = exts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => x.StartsWith(".") ? x : "." + x);

            DownloadCategoryRouter.Instance.AddCustomCategory(id, name, folder, extList);

            Categories.Add(new CategoryRuleViewModel
            {
                CategoryId = id,
                Name = name,
                DefaultSubFolder = folder,
                ExtensionsDisplay = string.Join(", ", extList)
            });

            NameBox.Clear();
            FolderBox.Clear();
            ExtsBox.Clear();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
