using FzLib.Extension;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using ShellLink;
using ShellLink.Structures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace LinkFileRepair
{
    public class LnkFile : INotifyPropertyChanged
    {
        private string lnkPath;

        private string newPath;

        private ObservableCollection<string> newPaths;

        private string oldPath;

        private Shortcut shortCut;

        private string status;

        public LnkFile(string path)
        {
            Debug.Assert(path.EndsWith(".lnk"));
            LnkPath = path;
            ShortCut = Shortcut.ReadFromFile(path);
            OldPath = shortCut.LinkTargetIDList.Path;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Brush Foreground => NewPaths != null && NewPaths.Count > 1 ? Brushes.Red : App.Current.MainWindow.Foreground;

        public string LnkPath
        {
            get => lnkPath;
            set => this.SetValueAndNotify(ref lnkPath, value, nameof(LnkPath), nameof(Name1));
        }

        public string Name1 => Path.GetFileName(LnkPath);

        public string Name2 => Path.GetFileName(OldPath);

        public bool Need { get; set; } = true;

        public string NewPath
        {
            get => newPath;
            set => this.SetValueAndNotify(ref newPath, value.Trim('"'), nameof(NewPath));
        }

        public ObservableCollection<string> NewPaths
        {
            get => newPaths;
            set => this.SetValueAndNotify(ref newPaths, value, nameof(NewPaths), nameof(Foreground));
        }

        public string OldPath
        {
            get => oldPath;
            set => this.SetValueAndNotify(ref oldPath, value, nameof(OldPath), nameof(Name2));
        }

        public Shortcut ShortCut
        {
            get => shortCut;
            set => this.SetValueAndNotify(ref shortCut, value, nameof(ShortCut));
        }

        public string Status
        {
            get => status;
            set => this.SetValueAndNotify(ref status, value, nameof(Status));
        }
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = ViewModel;
            InitializeComponent();
        }

        public MainWindowViewModel ViewModel { get; } = new MainWindowViewModel();

        /// <summary>
        /// 比较2个字符串的相似度（使用余弦相似度）
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        /// <returns>0-1之间的数</returns>
        public static double SimilarityCos(string str1, string str2)
        {
            str1 = str1.Trim();
            str2 = str2.Trim();
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
                return 0;

            List<string> lstr1 = SimpParticiple(str1);
            List<string> lstr2 = SimpParticiple(str2);
            //求并集
            var strUnion = lstr1.Union(lstr2);
            //求向量
            List<int> int1 = new List<int>();
            List<int> int2 = new List<int>();
            foreach (var item in strUnion)
            {
                int1.Add(lstr1.Count(o => o == item));
                int2.Add(lstr2.Count(o => o == item));
            }

            double s = 0;
            double den1 = 0;
            double den2 = 0;
            for (int i = 0; i < int1.Count(); i++)
            {
                //求分子
                s += int1[i] * int2[i];
                //求分母（1）
                den1 += Math.Pow(int1[i], 2);
                //求分母（2）
                den2 += Math.Pow(int2[i], 2);
            }

            return s / (Math.Sqrt(den1) * Math.Sqrt(den2));

            List<string> SimpParticiple(string str)
            {
                List<string> vs = new List<string>();
                foreach (var item in str)
                {
                    vs.Add(item.ToString());
                }
                return vs;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                ViewModel.LnkDir = dialog.FileName;
            }
        }

        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
            };
            if (dialog.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                ((sender as FrameworkElement).Tag as LnkFile).NewPath = dialog.FileName;
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
                IsFolderPicker = true,
            };
            if (dialog.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                ((sender as FrameworkElement).Tag as LnkFile).NewPath = dialog.FileName;
            }
        }

        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                ViewModel.SourceDir = dialog.FileName;
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            List<LnkFile> lnks = null;
            btnSearch.IsEnabled = false;
            await Task.Run(() =>
            {
                lnks = FzLib.IO.FileSystem.EnumerateAccessibleFiles(ViewModel.LnkDir, "*.lnk")
                         .Where(p => !p.Contains("$RECYCLE.BIN"))
                         .Select(p =>
                         {
                             try
                             {
                                 return new LnkFile(p);
                             }
                             catch
                             {
                                 return null;
                             }
                         })
                         .Where(p => p != null).ToList();
                ViewModel.Files = new ObservableCollection<LnkFile>(lnks);

                //var sourceFileName2LnkFilesDic = lnks.GroupBy(p => Path.GetFileName(p.OldPath)).ToDictionary(p => p.Key);
                Parallel.ForEach(lnks, lnk =>
                {
                    if (lnk.ShortCut.FileAttributes.HasFlag(ShellLink.Flags.FileAttributesFlags.FILE_ATTRIBUTE_DIRECTORY))
                    {
                        lnk.NewPaths = new ObservableCollection<string>(FzLib.IO.FileSystem.EnumerateAccessibleDirectories(ViewModel.SourceDir, Path.GetFileName(lnk.OldPath)));
                    }
                    else
                    {
                        lnk.NewPaths = new ObservableCollection<string>(FzLib.IO.FileSystem.EnumerateAccessibleFiles(ViewModel.SourceDir, Path.GetFileName(lnk.OldPath)));
                    }
                    if (lnk.NewPaths.Count > 0)
                    {
                        if (lnk.NewPaths.Count > 1)
                        {
                            if (lnk.NewPaths.Contains(lnk.OldPath))
                            {
                                lnk.NewPath = lnk.OldPath;
                                lnk.Status = "多匹配但无需修改";
                                lnk.Need = false;
                            }
                            else
                            {
                                lnk.NewPaths = new ObservableCollection<string>(lnk.NewPaths.OrderByDescending(p => SimilarityCos(lnk.OldPath, p)));
                                lnk.Status = "找到多个匹配";
                                lnk.NewPath = lnk.NewPaths[0];
                            }
                        }
                        else
                        {
                            lnk.NewPath = lnk.NewPaths[0];
                            if (lnk.NewPath == lnk.OldPath)
                            {
                                lnk.Status = "无需修改";
                                lnk.Need = false;
                            }
                            else
                            {
                                lnk.Status = "待修复";
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(lnk.OldPath) || Directory.Exists(lnk.OldPath))
                        {
                            lnk.Status = "无匹配，但文件存在";
                        }
                        else
                        {
                            lnk.Status = "无匹配";
                        }
                    }
                });
            });
            if (ViewModel.AutoHide)
            {
                foreach (var file in ViewModel.Files.Where(p => !p.Need).ToList())
                {
                    ViewModel.Files.Remove(file);
                }
            }

            btnSearch.IsEnabled = true;
            if (lnks.Count > 0)
            {
                btnRepair.IsEnabled = true;
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                DataGrid dg = sender as DataGrid;
                if (dg.SelectedCells.Count == 1)
                {
                    var a = dg.SelectedCells[0].Item as LnkFile;
                    switch (dg.SelectedCells[0].Column.DisplayIndex)
                    {
                        case 2:
                            Clipboard.SetText(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? Path.GetFileName(a.LnkPath) : a.LnkPath);
                            e.Handled = true;
                            break;

                        case 3:
                            Clipboard.SetText(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? Path.GetFileName(a.OldPath) : a.OldPath);
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        private void DataGrid_Selected(object sender, RoutedEventArgs e)
        {
            if ((sender as DataGrid).SelectedCells.First().Column.Header.Equals("新的源路径"))
            {
                (sender as DataGrid).BeginEdit(e);
            }
        }

        private void OpenExplorerAndSelectFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            string argument = $"/select, \"{path}\"";

            Process.Start("explorer.exe", argument);
        }

        private void RepairButton_Click(object sender, RoutedEventArgs e)
        {
            btnRepair.IsEnabled = false;
            foreach (var file in ViewModel.Files.Where(p => p.Need && !string.IsNullOrWhiteSpace(p.NewPath)))
            {
                var shortCut = new Shortcut()
                {
                    AccessTime = file.ShortCut.AccessTime,
                    CreationTime = file.ShortCut.CreationTime,
                    WriteTime = file.ShortCut.WriteTime,
                    FileAttributes = file.ShortCut.FileAttributes,
                    StringData = new StringData()
                    {
                        RelativePath = Path.GetRelativePath(file.LnkPath, file.NewPath),
                        WorkingDir = Path.GetDirectoryName(file.NewPath)
                    },
                    LinkTargetIDList = new LinkTargetIDList()
                    {
                        Path = file.NewPath,
                    },
                };

                shortCut.WriteToFile(file.LnkPath);
                file.Need = false;
                file.Status = "已修复";
            }
        }

        private void TextBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (dg.SelectedCells.Count == 1)
                {
                    var a = dg.SelectedCells[0].Item as LnkFile;
                    switch (dg.SelectedCells[0].Column.DisplayIndex)
                    {
                        case 2:
                            OpenExplorerAndSelectFile(a.LnkPath);
                            e.Handled = true;
                            break;

                        case 3:
                            OpenExplorerAndSelectFile(a.OldPath);
                            e.Handled = true;
                            break;
                    }
                }
            }
        }
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private bool autoHide = true;

        private ObservableCollection<LnkFile> files;

        private bool isWorking;

        private string lnkDir = @"";

        private string sourceDir = "";

        private string status;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool AutoHide
        {
            get => autoHide;
            set => this.SetValueAndNotify(ref autoHide, value, nameof(AutoHide));
        }

        public ObservableCollection<LnkFile> Files
        {
            get => files;
            set => this.SetValueAndNotify(ref files, value, nameof(Files));
        }

        public bool IsWorking
        {
            get => isWorking;
            set => this.SetValueAndNotify(ref isWorking, value, nameof(IsWorking));
        }

        public string LnkDir
        {
            get => lnkDir;
            set => this.SetValueAndNotify(ref lnkDir, value, nameof(LnkDir));
        }

        public string SourceDir
        {
            get => sourceDir;
            set => this.SetValueAndNotify(ref sourceDir, value, nameof(SourceDir));
        }

        public string Status
        {
            get => status;
            set => this.SetValueAndNotify(ref status, value, nameof(Status));
        }
    }
}