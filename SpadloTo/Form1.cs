using NAudio.Wave;
using SpadloTo.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpadloTo
{
    public partial class Form1 : Form
    {
        // ReSharper disable once InconsistentNaming
        private const string LOG_FILE_NAME = "crashlog.txt";
        private NotifyIcon _trayIcon;
        private string _executePath;
        private string _sharedDirPath;
        private readonly List<string> _logFiles = new List<string>();
        private List<Process> _processes;
        private List<string> _watchedProcessNames;
        private IWaveProvider _waveProvider;

        public Form1()
        {
            InitializeComponent();
            this.Icon = Resources.Jolanda;
            InitTray();
            InitPaths();
            InitMp3Player();
            StartGuarding();
        }

        private void StartGuarding()
        {
            _processes = new List<Process>();
            _watchedProcessNames = ConfigurationManager.AppSettings["watchedProcesses"].Split(',').ToList();

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    var watchedProcesses = _watchedProcessNames.SelectMany(Process.GetProcessesByName);

                    watchedProcesses.Where(x => _processes.FirstOrDefault(y => y.Id == x.Id) == null).ToList().ForEach(
                        x =>
                        {
                            try
                            {
                                _processes.Add(x);
                                x.EnableRaisingEvents = true;
                                x.Exited += (o, e) =>
                                {
                                    _processes.Remove(x);
                                    if (!(o is Process process))
                                        return;
                                    if (process.ExitCode == 0)
                                        return;

                                    Debug.WriteLine("Abnormal exit! - " + process.ExitCode);

                                    using (var player = new WaveOut())
                                    {
                                        player.Init(_waveProvider);
                                        player.Play();
                                        while (player.PlaybackState != PlaybackState.Stopped)
                                        {
                                            Thread.Sleep(10);
                                        }
                                    }

                                    InitMp3Player();

                                    LogCrash(process.ProcessName);
                                };
                            }
                            // ReSharper disable once EmptyGeneralCatchClause
                            catch
                            {
                            }
                        });
                    Thread.Sleep(200);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void InitPaths()
        {
            _executePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _sharedDirPath = ConfigurationManager.AppSettings["sharedDirPath"];


            var localFile = _executePath + "\\" + LOG_FILE_NAME;
            var networkFile = _sharedDirPath + "\\" + LOG_FILE_NAME;

            _logFiles.Add(localFile);
            _logFiles.Add(networkFile);
        }

        private void InitTray()
        {
            _trayIcon = new NotifyIcon();
            var item = new MenuItem("Zavřít", (o, e) => { this.Close(); });
            var context = new ContextMenu(new[] {item});
            _trayIcon.ContextMenu = context;
            _trayIcon.Visible = true;
            _trayIcon.Icon = this.Icon;
            _trayIcon.Text = "Jolanda Crash Detector";
        }

        private void InitMp3Player()
        {
            var audioFileName = ConfigurationManager.AppSettings["mp3File"];

            if (string.IsNullOrEmpty(_executePath))
                return;


            var path = Path.Combine(_executePath, audioFileName);
            if (!File.Exists(path))
            {
                using (var fileStream = File.OpenWrite(path))
                {
                    using (var writer = new BinaryWriter(fileStream))
                    {
                        writer.Write(Resources.Spadlo_to);
                    }
                }
            }

            _waveProvider = new Mp3FileReader(path);
        }

        private void LogCrash(string process)
        {
            if (string.IsNullOrEmpty(process))
            {
                return;
            }

            var time = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            var user = $"{Environment.UserName}";

            var sb = new StringBuilder();
            sb.AppendLine($"{time}\t{user}\t\t{process.ToUpper()}\tCRASH!");

            foreach (var logFile in _logFiles)
            {
                try
                {
                    using (var logWriter = File.AppendText(logFile))
                    {
                        logWriter.Write(sb);
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.ShowInTaskbar = false;
            this.Opacity = 0;
            this.Visible = false;
        }
    }
}