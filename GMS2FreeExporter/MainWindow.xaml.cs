using System;
using System.Collections.Generic;
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
using System.IO;
using System.Diagnostics;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO.Compression;

namespace GMS2FreeExporter
{
    class CopyDir
    {
        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool running = false;
        string runnerPath = "";

        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            lblRunnerStatus.Foreground = Brushes.Red;
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            var flag = false;
            foreach (var p in Process.GetProcessesByName("runner"))
            {
                var rv = FileVersionInfo.GetVersionInfo(p.MainModule.FileName);
                if (rv.ProductName == "GameMaker Studio 2                                                ")
                {
                    flag = true;
                    runnerPath = p.MainModule.FileName;

                    var gms2temp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameMakerStudio2", "GMS2TEMP");
                    var dirs = Directory.GetDirectories(gms2temp);
                    cboGameData.ItemsSource = dirs;

                    if (!running)
                    {
                        var last = dirs
                         .OrderByDescending(d => Directory.GetCreationTime(d))
                         .First();
                        cboGameData.SelectedItem = last;
                    }

                    running = true;
                }
            }

            if (!flag)
                running = false;

            if (running)
            {
                lblRunnerStatus.Content = "Running";
                lblRunnerStatus.Foreground = Brushes.Green;
                btnExport.IsEnabled = true;
                btnExportZip.IsEnabled = true;
                cboGameData.IsEnabled = true;
            }
            else
            {
                lblRunnerStatus.Content = "Not Running";
                lblRunnerStatus.Foreground = Brushes.Red;
                btnExport.IsEnabled = false;
                btnExportZip.IsEnabled = false;
                cboGameData.IsEnabled = false;
            }
        }

       
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var tdir = "";
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                tdir = dialog.FileName;
            }
            else
            {
                return;
            }

            // copy data.win & included file
            var dir = (string)cboGameData.SelectedItem;
            var gameName = "GAME";
            foreach (var f in Directory.GetFiles(dir))
            {
                if (Path.GetExtension(f) == ".win")
                {
                    gameName = Path.GetFileNameWithoutExtension(f);
                    File.Copy(f, Path.Combine(tdir, "data.win"), true);
                }
                else if (Path.GetExtension(f) != ".yydebug")
                {
                    File.Copy(f, Path.Combine(tdir, Path.GetFileName(f)), true);
                }
            }

            foreach (var d in Directory.GetDirectories(dir))
            {
                CopyDir.Copy(d, Path.Combine(tdir, Path.GetFileName(d)));
            }

            // remove data.win time limit
            RemoveTimeLimit(Path.Combine(tdir, "data.win"));

            // copy runner
            File.Copy(runnerPath, Path.Combine(tdir, $"{gameName}.exe"), true);

            MessageBox.Show($"The export was successful! \nGame folder: {tdir}");
        }

        static void RemoveTimeLimit(string filename)
        {
            var f = File.OpenRead(filename);
            var reader = new BinaryReader(f);
            f.Seek(0x4C, SeekOrigin.Begin);
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            f.Seek(0x24, SeekOrigin.Begin);
            var gameid = reader.ReadInt32();
            f.Seek(0x6C, SeekOrigin.Begin);
            var time = reader.ReadInt64();
            f.Seek(0x54, SeekOrigin.Begin);
            var info = reader.ReadInt32();
            f.Seek(0x90, SeekOrigin.Begin);
            var roomcount = reader.ReadInt32();
            for (int i = 0; i < roomcount; i++)
            {
                reader.ReadInt32();
            }
            var pos = f.Position;
            reader.Close();

            f = File.OpenWrite(filename);
            var writer = new BinaryWriter(f);

            f.Seek(pos, SeekOrigin.Begin);
            WriteNum(writer, time, gameid, width, height, info, roomcount);
            writer.Close();
        }
        static void WriteNum(BinaryWriter writer, long time, int gameid, int width, int height, int info, int roomcount)
        {
            Random random = new Random((int)(time & 4294967295L));
            long randomNum = (long)random.Next() << 32 | (long)random.Next();
            long specialNum = time;
            specialNum += -1000L;
            ulong initializeNum = (ulong)specialNum;
            initializeNum =
                ((initializeNum << 56 & 18374686479671623680UL) |
                (initializeNum >> 8 & 71776119061217280UL) |
                (initializeNum << 32 & 280375465082880UL) |
                (initializeNum >> 16 & 1095216660480UL) |
                (initializeNum << 8 & 4278190080UL) |
                (initializeNum >> 24 & 16711680UL) |
                (initializeNum >> 16 & 65280UL) |
                (initializeNum >> 32 & 255UL));
            specialNum = (long)initializeNum;
            specialNum ^= randomNum;
            specialNum = ~specialNum;
            specialNum ^= ((long)gameid << 32 | (long)gameid);
            specialNum ^= ((long)(width + info) << 48 | (long)(height + info) << 32 | (long)(height + info) << 16 | (long)(width + info));
            specialNum ^= 17L;
            int specialIndex = Math.Abs((int)(time & 65535L) / 7 + (gameid - width) + roomcount);
            specialIndex %= 4;

            writer.Write(randomNum);
            for (int i = 0; i < 4; i++)
            {
                if (i == specialIndex)
                {
                    writer.Write(specialNum);
                }
                else
                {
                    writer.Write(random.Next());
                    writer.Write(random.Next());
                }
            }
        }

        private void btnExportZip_Click(object sender, RoutedEventArgs e)
        {
            var zdir = "";
            var dialog = new CommonSaveFileDialog();
            dialog.Filters.Add(new CommonFileDialogFilter("Zip File", "*.zip"));
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                zdir = dialog.FileName;
                if (Path.GetExtension(zdir) != ".zip")
                {
                    zdir += ".zip";
                }
            }
            else
            {
                return;
            }

            var tdir = Path.Combine(Path.GetTempPath(), "GMS2FreeExporter");
            Directory.CreateDirectory(tdir);

            // copy data.win & included file
            var dir = (string)cboGameData.SelectedItem;
            var gameName = "GAME";
            foreach (var f in Directory.GetFiles(dir))
            {
                if (Path.GetExtension(f) == ".win")
                {
                    gameName = Path.GetFileNameWithoutExtension(f);
                    File.Copy(f, Path.Combine(tdir, "data.win"), true);
                }
                else if (Path.GetExtension(f) != ".yydebug")
                {
                    File.Copy(f, Path.Combine(tdir, Path.GetFileName(f)), true);
                }
            }

            foreach (var d in Directory.GetDirectories(dir))
            {
                CopyDir.Copy(d, Path.Combine(tdir, Path.GetFileName(d)));
            }

            // remove data.win time limit
            RemoveTimeLimit(Path.Combine(tdir, "data.win"));

            // copy runner
            File.Copy(runnerPath, Path.Combine(tdir, $"{gameName}.exe"), true);

            // make zip
            if (File.Exists(zdir))
                File.Delete(zdir);
            ZipFile.CreateFromDirectory(tdir, zdir);

            // remove temp files
            Directory.Delete(tdir, true);

            MessageBox.Show($"The export was successful! \nGame zip: {zdir}");
        }
    }
}
