
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SP_Exam_work
{
    public partial class MainWindow : Window
    {
        private static readonly object key = new object();
        private static int allRead;
        private static long allSize;
        private CancellationTokenSource cancel;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ButtonFrom_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();
            if (dialog.ShowDialog() == true)
            {   
                pathFrom.Text = dialog.FolderName;
            }
        }

        private void ButtonTo_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();
            if (dialog.ShowDialog() == true)
            {
                pathTo.Text = dialog.FolderName;
            }
        }

        private async void ButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            if (pathFrom.Text.Length == 0 || pathTo.Text.Length == 0) return;

            try
            {
                ButtonCopy.IsEnabled = false;
                ButtonCancel.IsEnabled = true;

                cancel = new CancellationTokenSource();
                allSize = folderSize(new DirectoryInfo(pathFrom.Text));
                List<string> filesToCopy = Directory.GetFiles(pathFrom.Text).ToList();
                int threadCount = 3;
                List<List<string>> sublists = filesToCopy
                    .Select((file, index) => new { file, index })
                    .GroupBy(x => x.index % threadCount)
                    .Select(group => group.Select(x => x.file).ToList())
                    .ToList();
                List<ProgressBar> progressBars = [p1, p2, p3];
                List<TextBlock> textBlocks = [t1, t2, t3];
                List<TextBlock> progressTexts = [t11, t21, t31];
                List<Task> tasks = new();

                for (int i = 0; i < threadCount; i++)
                {
                    int threadIndex = i;
                    tasks.Add(CopyFiles(sublists[threadIndex], pathTo.Text, progressBars[threadIndex], textBlocks[threadIndex], progressTexts[threadIndex], cancel.Token));
                }
                tasks.Add(MoveProgress(p4, cancel.Token));

                await Task.WhenAll(tasks);
                MessageBox.Show("Everithing was copied!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Copy operation was canceled.", "Canceled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonCopy.IsEnabled = true;
                ButtonCancel.IsEnabled = false;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            cancel?.Cancel();
        }

        private static async Task CopyFiles(List<string> from, string to, ProgressBar p, TextBlock t1, TextBlock t2, CancellationToken token)
        {
            await p.Dispatcher.InvokeAsync(() => p.Maximum = from.Count());
            long a = 0;
            foreach (string path in from)
            {
                a += new System.IO.FileInfo(path).Length;
            }
            foreach (var file in from)
            {
                string fileName = System.IO.Path.GetFileName(file);
                await t1.Dispatcher.InvokeAsync(() => t1.Text = fileName);
                string destinationPath = System.IO.Path.Combine(to, fileName);
                using (Stream @in = File.OpenRead(file))
                using (Stream @out = File.Create(destinationPath))
                {
                    int block = 10 * 1024 * 1024;
                    byte[] buffer = new byte[block];
                    int read;

                    while ((read = await @in.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        if (token.IsCancellationRequested) return;
                        await @out.WriteAsync(buffer, 0, read);
                        lock (key)
                        {
                            allRead += read;
                        }
                        await t2.Dispatcher.InvokeAsync(() => t2.Text = $"{a}/{read}");
                    }
                    await p.Dispatcher.InvokeAsync(() => p.Value += 1);
                }
            }
        }

        private static long folderSize(DirectoryInfo folder)
        {
            long totalSizeOfDir = 0;

            FileInfo[] allFiles = folder.GetFiles();

            foreach (FileInfo file in allFiles)
            {
                totalSizeOfDir += file.Length;
            }

            DirectoryInfo[] subFolders = folder.GetDirectories();
            foreach (DirectoryInfo dir in subFolders)
            {
                totalSizeOfDir += folderSize(dir);
            }

            return totalSizeOfDir;
        }
        private static async Task MoveProgress(ProgressBar p, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await p.Dispatcher.InvokeAsync(() =>
                {
                    p.Value = allSize > 0 ? (double)allRead / allSize  : 0;
                });

                if (allRead >= allSize)
                {
                    break;
                }
                await Task.Delay(100);

            }
        }
    }
}