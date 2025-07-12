using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public nint hwnd;
        private AppWindow appWindow;
        public MainWindow()
        {
            InitializeComponent();
            SystemBackdrop = new MicaBackdrop()
            { Kind = MicaKind.BaseAlt };
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            hwnd = WindowNative.GetWindowHandle(this);
            WindowId id = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(id);
            appWindow.MoveAndResize(new RectInt32(_X: 560, _Y: 280, _Width: 652, _Height: 200));

            //
            if (File.Exists("URLs.txt"))
            {
                string[] URLs = File.ReadAllLines("URLs.txt");
                foreach (string url in URLs)
                {
                    URL.Items.Add(url);
                }
                if(URLs.Length > 0) URL.SelectedIndex = 0;
            }
            byte[] bytes = new byte[8];
            fs.Position = 0;
            fs.Read(bytes, 0, bytes.Length);
            totaldownload = BitConverter.ToInt64(bytes);
            Total.Text = CountSize(totaldownload);
            //
            Timer timer = new Timer(1000); // 设置时间间隔为1秒
            timer.Elapsed += new ElapsedEventHandler(Method1);
            timer.AutoReset = true; // 设置为循环执行
            timer.Enabled = true; // 启动定时器
        }
        public int thread = 1;
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if(URL.SelectedIndex >= 0)
            {
                S.IsEnabled = false;
                iscancel = false;
                while (!iscancel)
                {
                    try
                    {
                        tasks.Clear();
                        for (int i = 0; i < thread; i++)
                        {
                            tasks.Add(Download(URL.SelectedItem.ToString()!));
                        }
                        await Task.WhenAll(tasks);

                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(500);
                        if (ex.Message == "$Canceled")
                        {
                            S.IsEnabled = true;
                            return;
                        }
                        else
                        {
                            threadsnow = 0;
                        }
                    }
                }
                Save();
            }
        }
        public long totaldownload = 0;
        public bool iscancel = true;
        public byte[] buffer = new byte[0x7FFFFF];
        public List<Task> tasks = new List<Task>();
        public int threadsnow = 0;
        public MemoryStream destinationStream = new MemoryStream();
        FileStream fs = new("Total.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        public static string CountSize(long Size)
        {
            string m_strSize = "";
            long FactSize = Size;
            if (FactSize < 1073741824)
                m_strSize = (FactSize / 1024.00 / 1024.00).ToString("F2") + " MB";
            else if (FactSize >= 1073741824)
                m_strSize = (FactSize / 1024.00 / 1024.00 / 1024.00).ToString("F2") + " GB";
            return m_strSize;
        }
        public async void Save()
        {
            fs.Position = 0;
            await fs.WriteAsync(BitConverter.GetBytes(totaldownload));
            await fs.FlushAsync();
        }
        public async Task Download(string fileUrl)
        {
            threadsnow++;
            while (!iscancel)
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36 Edg/134.0.0.0");
                    HttpResponseMessage response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            destinationStream.Seek(0, SeekOrigin.Begin);
                            await destinationStream.WriteAsync(buffer, 0, bytesRead);
                            totaldownload += bytesRead;
                            Total.Text = CountSize(totaldownload);
                            if (iscancel)
                            {
                                threadsnow--;
                                Save();
                                throw new Exception("$Canceled");
                            }
                        }
                    }
                }
                break;
            }
            threadsnow--;
            Save();
            return;
        }
        void Method1(object source, ElapsedEventArgs e) => DispatcherQueue.TryEnqueue(async() =>
        {
            try
            {
                KeyValuePair<DateTime, long> t1 = new(DateTime.Now, totaldownload);
                await Task.Delay(1000);
                KeyValuePair<DateTime, long> t2 = new(DateTime.Now, totaldownload);
                long s = t2.Value - t1.Value;
                TimeSpan t = t2.Key - t1.Key;
                long v = s / t.Seconds;
                Speed.Text = CountSize(v) + "/s";
                Thread.Text = threadsnow.ToString() + " T";
                Detail.Text = $"MemoryBuffer {CountSize(destinationStream.Length)} / URL {URL.Items.Count} / Active {!iscancel}";
            }
            catch
            {

            }
        }
        );
        private void T_Click(object sender, RoutedEventArgs e)
        {
            iscancel = true;
            S.IsEnabled = true;
            Save();
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Check for input device
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(G).Properties;
                if (properties.IsLeftButtonPressed && thread < 65536)
                {
                    thread++;
                    Thread.Text = $"Set -> {thread} T";
                }
                else if (properties.IsRightButtonPressed && thread > 1)
                {
                    thread--;
                    Thread.Text = $"Set -> {thread} T";
                }
                else if (properties.IsMiddleButtonPressed)
                {
                    thread = 1;
                    Thread.Text = $"Set -> {thread} T";
                }
            }
        }

        private void SC(object sender, WindowSizeChangedEventArgs args)
        {
            try
            {
                appWindow.Resize(new(652, 200));
            }
            catch
            {
                return;
            }
        }
    }
}
