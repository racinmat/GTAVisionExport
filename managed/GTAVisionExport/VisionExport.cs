﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using YamlDotNet;
using YamlDotNet.Serialization;
using BitMiracle.LibTiff.Classic;
using System.Drawing;
using System.Drawing.Imaging;
using Amazon;
using Amazon.Runtime;
using YamlDotNet.RepresentationModel;
using Amazon.S3;
using Amazon.S3.IO;
using Amazon.S3.Model;
using System.IO.Pipes;
using System.Net;
using VAutodrive;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using GTAVisionUtils;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using GTA.Native;
using Color = System.Windows.Media.Color;
using System.Configuration;
using System.Threading;
using IniParser;

namespace GTAVisionExport {
    
    class VisionExport : Script
    {
#if DEBUG
        const string session_name = "NEW_DATA_CAPTURE_NATURAL_V4_3";
#else
        const string session_name = "NEW_DATA_CAPTURE_NATURAL_V4_3";
#endif
        //private readonly string dataPath =
        //    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Data");
        private readonly string dataPath = @"D:\projekty\GTA-V-extractors\output\";
        private readonly Weather[] wantedWeather = new Weather[] {Weather.Clear, Weather.Clouds, Weather.Overcast, Weather.Raining, Weather.Christmas};
        private Player player;
        private string outputPath;
        private GTARun run;
        private bool enabled = false;
        private Socket server;
        private Socket connection;
        private UTF8Encoding encoding = new UTF8Encoding(false);
        private KeyHandling kh = new KeyHandling();
//        private ZipArchive archive;
//        private Stream outStream;
        private Task postgresTask;
        private Task runTask;
        private int curSessionId = -1;
        private speedAndTime lowSpeedTime = new speedAndTime();
        private bool IsGamePaused = false;
        private StereoCamera cams;
        public VisionExport()
        {
            System.IO.File.WriteAllText(@"D:\projekty\GTA-V-extractors\output\log.txt", "VisionExport constructor called.\n");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            PostgresExport.InitSQLTypes();
            player = Game.Player;
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Loopback, 5555));
            server.Listen(5);
            //server = new UdpClient(5555);
            var parser = new FileIniDataParser();
            var location = AppDomain.CurrentDomain.BaseDirectory;
            var data = parser.ReadFile(Path.Combine(location, "GTAVision.ini"));
            //outputPath = @"D:\Datasets\GTA\";
            //outputPath = Path.Combine(outputPath, "testData.yaml");
            //outStream = File.CreateText(outputPath);
            this.Tick += new EventHandler(this.OnTick);
            this.KeyDown += OnKeyDown;
            
            Interval = 1000;
            if (enabled)
            {
                postgresTask?.Wait();
                postgresTask = StartSession();
                runTask?.Wait();
                runTask = StartRun();
            }
        }

        private void handlePipeInput()
        {
            System.IO.File.AppendAllText(@"D:\projekty\GTA-V-extractors\output\log.txt", "VisionExport handlePipeInput called.\n");
            UI.Notify("handlePipeInput called");
            UI.Notify("server connected:" + server.Connected.ToString());
            UI.Notify(connection == null ? "connection is null" : "connection:" + connection.ToString());
            if (connection == null) return;
            
            byte[] inBuffer = new byte[1024];
            string str = "";
            int num = 0;
            try
            {
                num = connection.Receive(inBuffer);
                str = encoding.GetString(inBuffer, 0, num);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.WouldBlock)
                {
                    return;
                }
                throw;
            }
            if (num == 0)
            {
                connection.Shutdown(SocketShutdown.Both);
                connection.Close();
                connection = null;
                return;
            }
            UI.Notify("str: " + str.ToString());
            switch (str)
            {
                case "START_SESSION":
                    postgresTask?.Wait();
                    postgresTask = StartSession();
                    runTask?.Wait();
                    runTask = StartRun();
                    break;
                case "STOP_SESSION":
                    StopRun();
                    StopSession();
                    break;
                case "TOGGLE_AUTODRIVE":
                    ToggleNavigation();
                    break;
                case "ENTER_VEHICLE":
                    UI.Notify("Trying to enter vehicle");
                    EnterVehicle();
                    break;
                case "AUTOSTART":
                    Autostart();
                    break;
                case "RELOADGAME":
                    ReloadGame();
                    break;
                case "RELOAD":
                    FieldInfo f = this.GetType().GetField("_scriptdomain", BindingFlags.NonPublic | BindingFlags.Instance);
                    object domain = f.GetValue(this);
                    MethodInfo m = domain.GetType()
                        .GetMethod("DoKeyboardMessage", BindingFlags.Instance | BindingFlags.Public);
                    m.Invoke(domain, new object[] {Keys.Insert, true, false, false, false});
                    break;
//                    uncomment when resolving, how the hell should I get image by socket correctly
//                case "GET_SCREEN":
//                    var last = ImageUtils.getLastCapturedFrame();
//                    Int64 size = last.Length;
//                    UI.Notify("last size: " + size.ToString());
//                    size = IPAddress.HostToNetworkOrder(size);
//                    connection.Send(BitConverter.GetBytes(size));
//                    connection.Send(last);
//                    break;

            }
        }

//        private void UploadFile()
//        {
//            System.IO.File.AppendAllText(@"D:\projekty\GTA-V-extractors\output\log.txt", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + ": VisionExport UploadFile called.\n");
//            UI.Notify("UploadFile called");
//
////            archive.Dispose();
//            var oldOutput = outputPath;
//            if (oldOutput != null)
//            {
//                new Thread(() =>
//                {
//                    File.Move(oldOutput, Path.Combine(dataPath, run.guid + ".zip"));
//                }).Start();
//            }
//            
//            outputPath = Path.GetTempFileName();
//            outStream = File.Open(outputPath, FileMode.Truncate);
////            archive = new ZipArchive(outStream, ZipArchiveMode.Update);
//            //File.Delete(oldOutput);
//            
//            /*
//            archive.Dispose();
//            var req = new PutObjectRequest {
//                BucketName = "gtadata",
//                Key = "images/" + run.guid + ".zip",
//                FilePath = outputPath
//            };
//            var resp = client.PutObjectAsync(req);
//            outputPath = Path.GetTempFileName();
//            S3Stream = File.Open(outputPath, FileMode.Truncate);
//            archive = new ZipArchive(S3Stream, ZipArchiveMode.Update);
//            
//            await resp;
//            File.Delete(req.FilePath);
//            */
//        }
        
        public void OnTick(object o, EventArgs e)
        {
            

            if (server.Poll(10, SelectMode.SelectRead) && connection == null)
            {
                connection = server.Accept();
                UI.Notify("CONNECTED");
                connection.Blocking = false;
            }
            handlePipeInput();
            if (!enabled) return;
            
            //Array values = Enum.GetValues(typeof(Weather));


            switch (checkStatus()) {
                case GameStatus.NeedReload:
                    //TODO: need to get a new session and run?
                    StopRun();
                    runTask?.Wait();
                    runTask = StartRun();
                    //StopSession();
                    //Autostart();
                    UI.Notify("need reload game");
                    Script.Wait(100);
                    ReloadGame();
                    break;
                case GameStatus.NeedStart:
                    //TODO do the autostart manually or automatically?
                    //Autostart();
                    // use reloading temporarily
                    StopRun();
                    
                    ReloadGame();
                    Script.Wait(100);
                    runTask?.Wait();
                    runTask = StartRun();
                    //Autostart();
                    break;
                case GameStatus.NoActionNeeded:
                    break;
            }
            UI.Notify("runTask.IsCompleted: " + runTask.IsCompleted.ToString());
            UI.Notify("postgresTask.IsCompleted: " + postgresTask.IsCompleted.ToString());
            if (!runTask.IsCompleted) return;
            if (!postgresTask.IsCompleted) return;

            UI.Notify("going to save tiff");

//            List<byte[]> colors = new List<byte[]>();
            Game.Pause(true);
            Script.Wait(100);
            var dateTimeFormat = @"yyyy-MM-dd--HH-mm-ss";
            GTAData dat = GTAData.DumpData(DateTime.UtcNow.ToString(dateTimeFormat), wantedWeather.ToList());
            if (dat == null) return;
//            var thisframe = VisionNative.GetCurrentTime();
//            var depth = VisionNative.GetDepthBuffer();
//            var stencil = VisionNative.GetStencilBuffer();
//            colors.Add(VisionNative.GetColorBuffer());
            /*
            foreach (var wea in wantedWeather) {
                World.TransitionToWeather(wea, 0.0f);
                Script.Wait(1);
                colors.Add(VisionNative.GetColorBuffer());
            }*/
//            Game.Pause(false);
            
            /*
            if (World.Weather != Weather.Snowing)
            {
                World.TransitionToWeather(Weather.Snowing, 1);
                
            }*/
//            var colorframe = VisionNative.GetLastColorTime();
//            var depthframe = VisionNative.GetLastConstantTime();
//            var constantframe = VisionNative.GetLastConstantTime();
            //UI.Notify("DIFF: " + (colorframe - depthframe) + " FRAMETIME: " + (1 / Game.FPS) * 1000);
//            UI.Notify("colors length: " + colors[0].Length.ToString());
//            if (depth == null || stencil == null)
//            {
//                UI.Notify("No DEPTH");
//                return;
//            }

            /*
             * this code checks to see if there's drift
             * it's kinda pointless because we end up "straddling" a present call,
             * so the capture time difference can be ~1/4th of a frame but still the
             * depth/stencil and color buffers are one frame offset from each other
            if (Math.Abs(thisframe - colorframe) < 60 && Math.Abs(colorframe - depthframe) < 60 &&
                Math.Abs(colorframe - constantframe) < 60)
            {
                



                
                PostgresExport.SaveSnapshot(dat, run.guid);
            }
            */
            UI.Notify("going to save images and save to postgres");
//            ImageUtils.WaitForProcessing();
            saveSnapshotToFile(dat.ImageName, wantedWeather);
//            ImageUtils.StartUploadTask(archive, Game.GameTime.ToString(), Game.ScreenResolution.Width,
//                Game.ScreenResolution.Height, colors, depth, stencil);
            
            UI.Notify("going to save snapshot to db");
            UI.Notify("current weather: " + dat.CurrentWeather.ToString());
            PostgresExport.SaveSnapshot(dat, run.guid);
//            outStream.Flush();
//            if ((Int64)outStream.Length > (Int64)2048 * (Int64)1024 * (Int64)1024) {
//                ImageUtils.WaitForProcessing();
//                StopRun();
//                runTask?.Wait();
//                runTask = StartRun();
//            }
        }

        /* -1 = need restart, 0 = normal, 1 = need to enter vehicle */
        public GameStatus checkStatus()
        {
            Ped player = Game.Player.Character;
            if (player.IsDead) return GameStatus.NeedReload;
            if (player.IsInVehicle())
            {
                Vehicle vehicle = player.CurrentVehicle;
                //UI.Notify("T:" + Game.GameTime.ToString() + " S: " + vehicle.Speed.ToString());
                if (vehicle.Speed < 1.0f) //speed is in mph
                {
                    if (lowSpeedTime.checkTrafficJam(Game.GameTime, vehicle.Speed))
                    {
                        return GameStatus.NeedReload;
                    }
                }
                else
                {
                    lowSpeedTime.clearTime();
                }
                return GameStatus.NoActionNeeded;
            }
            else
            {
                return GameStatus.NeedReload;
            }
        }

        public Bitmap CaptureScreen()
        {
            UI.Notify("CaptureScreen called");
            var cap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            var gfx = Graphics.FromImage(cap);
            //var dat = GTAData.DumpData(Game.GameTime + ".jpg");
            gfx.CopyFromScreen(0, 0, 0, 0, cap.Size);
            /*
            foreach (var ped in dat.ClosestPeds) {
                var w = ped.ScreenBBMax.X - ped.ScreenBBMin.X;
                var h = ped.ScreenBBMax.Y - ped.ScreenBBMin.Y;
                var x = ped.ScreenBBMin.X;
                var y = ped.ScreenBBMin.Y;
                w *= cap.Size.Width;
                h *= cap.Size.Height;
                x *= cap.Size.Width;
                y *= cap.Size.Height;
                gfx.DrawRectangle(new Pen(Color.Lime), x, y, w, h);
            } */
            return cap;
            //cap.Save(GetFileName(".png"), ImageFormat.Png);

        }

        public void Autostart()
        {
            EnterVehicle();
            Script.Wait(200);
            ToggleNavigation();
            Script.Wait(200);
            postgresTask?.Wait();
            postgresTask = StartSession();
        }

        public async Task StartSession(string name = session_name)
        {
            if (name == null) name = Guid.NewGuid().ToString();
            if (curSessionId != -1) StopSession();
            int id = await PostgresExport.StartSession(name);
            curSessionId = id;
        }

        public void StopSession()
        {
            if (curSessionId == -1) return;
            PostgresExport.StopSession(curSessionId);
            curSessionId = -1;
        }
        public async Task StartRun()
        {
            await postgresTask;
            if(run != null) PostgresExport.StopRun(run);
            var runid = await PostgresExport.StartRun(curSessionId);

            //var s3Info = new S3FileInfo(client, "gtadata", run.archiveKey);
            //S3Stream = s3Info.Create();
            
//            outputPath = Path.GetTempFileName();
//            outStream = File.Open(outputPath, FileMode.Truncate);
//            archive = new ZipArchive(outStream, ZipArchiveMode.Create);
            
            //archive = new ZipArchive(, ZipArchiveMode.Create);
            
            //archive = ZipFile.Open(Path.Combine(dataPath, run.guid + ".zip"), ZipArchiveMode.Create);
            

            run = runid;
            enabled = true;
        }

        public void StopRun()
        {
            runTask?.Wait();
            ImageUtils.WaitForProcessing();
//            if (outStream.CanWrite)
//            {
//                outStream.Flush();
//            }
            enabled = false;
            PostgresExport.StopRun(run);
//            UploadFile();
            run = null;
            
            Game.Player.LastVehicle.Alpha = int.MaxValue;
        }

        public void EnterVehicle()
        {
            /*
            var vehicle = World.GetClosestVehicle(player.Character.Position, 30f);
            player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
            */
            Model mod = new Model(GTA.Native.VehicleHash.Asea);
            if (mod == null) {UI.Notify("mod is null");}
            if (player == null) {UI.Notify("player is null");}
            if (player.Character == null) {UI.Notify("player.Character is null");}
            UI.Notify("player position: " + player.Character.Position.ToString());
            var vehicle = GTA.World.CreateVehicle(mod, player.Character.Position);
            if (vehicle == null)
            {
                UI.Notify("vehicle is null. Something is fucked up");
            }
            else
            {
                player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);                
            }
            //vehicle.Alpha = 0; //transparent
            //player.Character.Alpha = 0;
        }

        public void ToggleNavigation()
        {
            //YOLO
            MethodInfo inf = kh.GetType().GetMethod("AtToggleAutopilot", BindingFlags.NonPublic | BindingFlags.Instance);
            inf.Invoke(kh, new object[] {new KeyEventArgs(Keys.J)});
        }

        public void ReloadGame()
        {
            /*
            Process p = Process.GetProcessesByName("Grand Theft Auto V").FirstOrDefault();
            if (p != null)
            {
                IntPtr h = p.MainWindowHandle;
                SetForegroundWindow(h);
                SendKeys.SendWait("{ESC}");
                //Script.Wait(200);
            }
            */
            // or use CLEAR_AREA_OF_VEHICLES
            Ped player = Game.Player.Character;
            //UI.Notify("x = " + player.Position.X + "y = " + player.Position.Y + "z = " + player.Position.Z);
            // no need to release the autodrive here
            // delete all surrounding vehicles & the driver's car
            Function.Call(GTA.Native.Hash.CLEAR_AREA_OF_VEHICLES, player.Position.X, player.Position.Y, player.Position.Z, 1000f, false, false, false, false);
            player.LastVehicle.Delete();
            // teleport to the spawning position, defined in GameUtils.cs, subject to changes
            player.Position = GTAConst.StartPos;
            Function.Call(GTA.Native.Hash.CLEAR_AREA_OF_VEHICLES, player.Position.X, player.Position.Y, player.Position.Z, 100f, false, false, false, false);
            // start a new run
            EnterVehicle();
            //Script.Wait(2000);
            ToggleNavigation();

            lowSpeedTime.clearTime();

        }

        public void TraverseWeather()
        {
            for (int i = 1; i < 14; i++)
            {
                //World.Weather = (Weather)i;
                World.TransitionToWeather((Weather)i, 0.0f);
                //Script.Wait(1000);
            }
        }

        public void OnKeyDown(object o, KeyEventArgs k)
        {
            System.IO.File.AppendAllText(@"D:\projekty\GTA-V-extractors\output\log.txt", "VisionExport OnKeyDown called.\n");
            if (k.KeyCode == Keys.PageUp)
            {
                postgresTask?.Wait();
                postgresTask = StartSession();
                runTask?.Wait();
                runTask = StartRun();
                UI.Notify("GTA Vision Enabled");
            }
            if (k.KeyCode == Keys.PageDown)
            {
                StopRun();
                StopSession();
                UI.Notify("GTA Vision Disabled");
            }
            if (k.KeyCode == Keys.H) // temp modification
            {
                EnterVehicle();
                UI.Notify("Trying to enter vehicle");
                ToggleNavigation();
            }
            if (k.KeyCode == Keys.Y) // temp modification
            {
                ReloadGame();
            }
            if (k.KeyCode == Keys.U) // temp modification
            {
                var settings = ScriptSettings.Load("GTAVisionExport.xml");
                var loc = AppDomain.CurrentDomain.BaseDirectory;

                //UI.Notify(ConfigurationManager.AppSettings["database_connection"]);
                var str = settings.GetValue("", "ConnectionString");
                UI.Notify("BaseDirectory: " + loc);
                UI.Notify("ConnectionString: " + str);

            }
            if (k.KeyCode == Keys.G) // temp modification
            {
                /*
                IsGamePaused = true;
                Game.Pause(true);
                Script.Wait(500);
                TraverseWeather();
                Script.Wait(500);
                IsGamePaused = false;
                Game.Pause(false);
                */
                var data = GTAData.DumpData(Game.GameTime + ".tiff", new List<Weather>(wantedWeather));

                string path = @"D:\projekty\GTA-V-extractors\output\trymatrix.txt";
                // This text is added only once to the file.
                if (!File.Exists(path))
                {
                    // Create a file to write to.
                    using (StreamWriter file = File.CreateText(path))
                    {
                        
                        
                        file.WriteLine("cam direction file");
                        file.WriteLine("direction:");
                        file.WriteLine(GameplayCamera.Direction.X.ToString() + ' ' + GameplayCamera.Direction.Y.ToString() + ' ' + GameplayCamera.Direction.Z.ToString());
                        file.WriteLine("Dot Product:");
                        file.WriteLine(Vector3.Dot(GameplayCamera.Direction, GameplayCamera.Rotation));
                        file.WriteLine("position:");
                        file.WriteLine(GameplayCamera.Position.X.ToString() + ' ' + GameplayCamera.Position.Y.ToString() + ' ' + GameplayCamera.Position.Z.ToString());
                        file.WriteLine("rotation:");
                        file.WriteLine(GameplayCamera.Rotation.X.ToString() + ' ' + GameplayCamera.Rotation.Y.ToString() + ' ' + GameplayCamera.Rotation.Z.ToString());
                        file.WriteLine("relative heading:");
                        file.WriteLine(GameplayCamera.RelativeHeading.ToString());
                        file.WriteLine("relative pitch:");
                        file.WriteLine(GameplayCamera.RelativePitch.ToString());
                        file.WriteLine("fov:");
                        file.WriteLine(GameplayCamera.FieldOfView.ToString());
                    }
                }
            }

            if (k.KeyCode == Keys.T) // temp modification
            {
                World.Weather = Weather.Raining;
                /* set it between 0 = stop, 1 = heavy rain. set it too high will lead to sloppy ground */
                Function.Call(GTA.Native.Hash._SET_RAIN_FX_INTENSITY, 0.5f);
                var test = Function.Call<float>(GTA.Native.Hash.GET_RAIN_LEVEL);
                UI.Notify("" + test);
                World.CurrentDayTime = new TimeSpan(12, 0, 0);
                //Script.Wait(5000);
            }

            if (k.KeyCode == Keys.N)
            {
                UI.Notify("N pressed, going to print stats to file or what?");
                
                //var color = VisionNatGetColorBuffer();
                
                dumpTest();

                //var color = VisionNative.GetColorBuffer();
                for (int i = 0; i < 100; i++)
                {
                    saveSnapshotToFile(i.ToString(), wantedWeather);

                    Script.Wait(200);
                }
        }
            if (k.KeyCode == Keys.I)
            {
                var info = new GTAVisionUtils.InstanceData();
                UI.Notify(info.type);
                UI.Notify(info.publichostname);
            }
        }

        private void saveSnapshotToFile(String name, Weather[] weathers)
        {
            List<byte[]> colors = new List<byte[]>();
            Game.Pause(true);
            var depth = VisionNative.GetDepthBuffer();
            var stencil = VisionNative.GetStencilBuffer();
            foreach (var wea in weathers)
            {
                World.TransitionToWeather(wea, 0.0f);
                Script.Wait(1);
                colors.Add(VisionNative.GetColorBuffer());
            }

            Game.Pause(false);
            var res = Game.ScreenResolution;
            var fileName = Path.Combine(dataPath, "info-" + name);
            ImageUtils.WriteToTiff(fileName, res.Width, res.Height, colors, depth, stencil);
            UI.Notify("file saved to: " + fileName);
//            UI.Notify("FieldOfView: " + GameplayCamera.FieldOfView.ToString());
            //UI.Notify((connection != null && connection.Connected).ToString());


//            var data = GTAData.DumpData(Game.GameTime + ".dat", new List<Weather>(wantedWeather));

//            string path = @"D:\projekty\GTA-V-extractors\output\info.txt";
//            // This text is added only once to the file.
//            if (!File.Exists(path))
//            {
//                // Create a file to write to.
//                using (StreamWriter file = File.CreateText(path))
//                {
//                    file.WriteLine("cam direction & Ped pos file");
//                }
//            }
//
//            using (StreamWriter file = File.AppendText(path))
//            {
//                file.WriteLine("==============info" + i.ToString() + ".tiff 's metadata=======================");
//                file.WriteLine("cam pos");
//                file.WriteLine(GameplayCamera.Position.X.ToString());
//                file.WriteLine(GameplayCamera.Position.Y.ToString());
//                file.WriteLine(GameplayCamera.Position.Z.ToString());
//                file.WriteLine("cam direction");
//                file.WriteLine(GameplayCamera.Direction.X.ToString());
//                file.WriteLine(GameplayCamera.Direction.Y.ToString());
//                file.WriteLine(GameplayCamera.Direction.Z.ToString());
//                file.WriteLine("projection matrix");
//                file.WriteLine(data.ProjectionMatrix.Values.ToString());
//                file.WriteLine("view matrix");
//                file.WriteLine(data.ViewMatrix.Values.ToString());
//                file.WriteLine("character");
//                file.WriteLine(data.Pos.X.ToString());
//                file.WriteLine(data.Pos.Y.ToString());
//                file.WriteLine(data.Pos.Z.ToString());
//                foreach (var detection in data.Detections)
//                {
//                    file.WriteLine(detection.Type.ToString());
//                    file.WriteLine(detection.Pos.X.ToString());
//                    file.WriteLine(detection.Pos.Y.ToString());
//                    file.WriteLine(detection.Pos.Z.ToString());
//                }
//            }
        }

        private void dumpTest()
        {
            List<byte[]> colors = new List<byte[]>();
            Game.Pause(true);
            Script.Wait(1);
            var depth = VisionNative.GetDepthBuffer();
            var stencil = VisionNative.GetStencilBuffer();
            foreach (var wea in wantedWeather)
            {
                World.TransitionToWeather(wea, 0.0f);
                Script.Wait(1);
                colors.Add(VisionNative.GetColorBuffer());
            }
            Game.Pause(false);
            if (depth != null)
            {
                var res = Game.ScreenResolution;
                ImageUtils.WriteToTiff(Path.Combine(dataPath, "test"), res.Width, res.Height, colors, depth, stencil);
                UI.Notify(GameplayCamera.FieldOfView.ToString());
            }
            else
            {
                UI.Notify("No Depth Data quite yet");
            }
            UI.Notify((connection != null && connection.Connected).ToString());
        }
    }
}
