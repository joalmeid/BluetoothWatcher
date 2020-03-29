using System;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;

namespace Jalmeida.Bluetooth
{
    public partial class SysTrayApp : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private static string kvmCaption;
        private static string kvmClassGuid;
        private static string kvmDeviceId;
        private static string desktopCaption;
        private static string desktopClassGuid;
        private static string desktopDeviceId;

        public SysTrayApp()
        {
            InitializeComponent();

            // Configuration
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile("config.json", optional: true)
                .Build();

            kvmCaption = configuration["kvmDevice:Caption"];
            kvmClassGuid = configuration["kvmDevice:ClassGuid"];
            kvmDeviceId = configuration["kvmDevice:PNPDeviceID"];
            desktopCaption = configuration["desktopDevice:Caption"];
            desktopClassGuid = configuration["desktopDevice:ClassGuid"];
            desktopDeviceId = configuration["desktopDevice:PNPDeviceID"];

            // Create a simple tray menu with only one item.
            this.trayMenu = new ContextMenuStrip();
            this.trayMenu.Items.Add("Set &Home", null, SetHomeEnvironment);
            this.trayMenu.Items.Add("Set &Mobile", null, SetMobileEnvironment);
            this.trayMenu.Items.Add(new ToolStripSeparator());
            this.trayMenu.Items.Add("Exit", null, OnExit);

            // Create a tray icon. In this example we use a
            // standard system icon for simplicity, but you
            // can of course use your own custom icon too.
            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "BlueTooth";
            this.trayIcon.Icon = new System.Drawing.Icon("./assets/bl6.ico");

            // Add menu to tray icon and show it.
            this.trayIcon.ContextMenuStrip = trayMenu;
            this.trayIcon.Visible = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            this.Visible = false; // Hide form window.
            this.ShowInTaskbar = false; // Remove from taskbar.

            base.OnLoad(e);
        }

        private void SysTrayApp_Load(object sender, EventArgs e)
        {
            DetectDeviceChanges(3, kvmCaption);
            DoNotification(this.trayIcon,"Bluetooth Watcher", $"Detecting Device changes...");
        }

        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void DoNotification(NotifyIcon trayIcon, string title, string message)
        {
            trayIcon.Icon = new System.Drawing.Icon("./assets/bl6.ico");
            trayIcon.Visible = true;
            trayIcon.BalloonTipText = message;
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(2000);
        }

        private void SetHomeEnvironment(object sender, EventArgs e)
        {
            SetWorkEnvironment(WorkEnvironment.Home);
        }

        private void SetMobileEnvironment(object sender, EventArgs e)
        {
            SetWorkEnvironment(WorkEnvironment.Mobile);
        }

        private void SetWorkEnvironment(WorkEnvironment targetEnv)
        {
            if(targetEnv == WorkEnvironment.Home) 
            {
                // Environment Home
                EnableDisableDevice(kvmClassGuid, kvmDeviceId, false);
                EnableDisableDevice(desktopClassGuid, desktopDeviceId, false);
                EnableDisableDevice(kvmClassGuid, kvmDeviceId, true);
                DoNotification(this.trayIcon, "Bluetooth Watcher", $"Enabled {kvmCaption}.");

            }
            else
            {
                // Environment Mobile
                EnableDisableDevice(desktopClassGuid, desktopDeviceId, true);
                DoNotification(this.trayIcon, "Bluetooth Watcher", $"Enabled {desktopCaption}.");
            }
        }


        private void EnableDisableDevice(string deviceId, string instancePath, bool enable)
        {
            Guid deviceGuid;
            if (Guid.TryParse(deviceId, out deviceGuid))
            {
                try
                {
                    DeviceHelper.SetDeviceEnabled(deviceGuid, instancePath, enable);
                }
                catch (ArgumentException ex)
                {
                    DoNotification(this.trayIcon, "Error", ex.Message);
                }
            }
            else Console.WriteLine($"EnableDisableDevice: DeviceId not valid: {deviceId}");
        }

        public void DeviceChangedEvent(object sender, EventArrivedEventArgs e)
        {
            using (ManagementBaseObject blBase = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value)
            {
                string curDeviceCaption = blBase?.Properties["Caption"]?.Value.ToString();
                string curDeviceClassGuid = blBase?.Properties["ClassGuid"]?.Value.ToString();
                string curDeviceManufacturer = blBase?.Properties["Manufacturer"]?.Value.ToString();
                string curDevicePNPClass = blBase?.Properties["PNPClass"]?.Value.ToString();
                string curDevicePNPDeviceID = blBase?.Properties["PNPDeviceID"]?.Value.ToString();
                string curDeviceService = blBase?.Properties["Service"]?.Value.ToString();

                switch (e.NewEvent.ClassPath.ClassName)
                {
                    case "__InstanceDeletionEvent":
                        Console.WriteLine("Device Instance Delete");
                        PrintEvent(blBase);
                        SetWorkEnvironment(WorkEnvironment.Mobile);
                        //EnableDisableDevice(curDeviceClassGuid, curDevicePNPDeviceID, false);
                        //EnableDisableDevice(desktopClassGuid, desktopDeviceId, true);
                        //DoNotification(this.trayIcon, "Bluetooth Watcher", $"Disabled {curDeviceCaption}.");
                        //DoNotification(this.trayIcon, "Bluetooth Watcher", $"Enabled{desktopCaption}.");
                        break;
                    case "__InstanceCreationEvent":
                        Console.WriteLine("Device Instance Creation");
                        PrintEvent(blBase);
                        SetWorkEnvironment(WorkEnvironment.Home);
                        //EnableDisableDevice(curDeviceClassGuid, curDevicePNPDeviceID, true);
                        //DoNotification(this.trayIcon, "Bluetooth Watcher", $"Enabled {curDeviceCaption}.");
                        break;
                    case "__InstanceModificationEvent":
                        Console.WriteLine($"Device Instance Modification: {curDeviceCaption}");
                        break;
                    default:
                        Console.WriteLine(e.NewEvent.ClassPath.ClassName);
                        PrintEvent(blBase);
                        DoNotification(this.trayIcon, "Bluetooth Watcher", $"Event {e.NewEvent.ClassPath.ClassName}.");
                        break;
                }
            }
        }

        private static void PrintEvent(ManagementBaseObject evtObj)
        {
            foreach (var property in evtObj.Properties)
            {
                Console.WriteLine($"\t{property.Name}:{property.Value}");
            }
        }

        public void DetectDeviceChanges(int WmiInterval, string deviceCaption)
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery();
                query.EventClassName = "__InstanceOperationEvent";
                query.WithinInterval = new TimeSpan(0, 0, WmiInterval);
                query.Condition = $"TargetInstance Isa 'Win32_PnPEntity' And TargetInstance.PNPClass like 'Bluetooth' And TargetInstance.Caption like '{deviceCaption}'";

                ManagementScope scope = new ManagementScope("root\\CIMV2");
                using (ManagementEventWatcher blWatcher = new ManagementEventWatcher(scope, query))
                {
                    blWatcher.Options.Timeout = ManagementOptions.InfiniteTimeout;
                    blWatcher.EventArrived += new EventArrivedEventHandler(DeviceChangedEvent);
                    blWatcher.Stopped += new StoppedEventHandler(HandleStoppedSubscription);

                    blWatcher.Start();
                }

            }
            catch (ManagementException ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }

        public static void HandleStoppedSubscription(object sender, StoppedEventArgs e)
        {
            Console.WriteLine("Event Subscription Stopeed!");
        }
    }

    internal enum WorkEnvironment
    {
        Mobile = 1,
        Home = 2
    }

}
