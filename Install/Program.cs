using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace DriveMapperInstaller
{
    class Installer
    {
        public static void Main(string[] args)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DriveMapper.exe");

            if (!File.Exists(exePath))
            {
                Console.WriteLine("DriveMapper.exe not found.");
                return;
            }

            CreateLogonTask("DriveMapperLogon", exePath, 10);
            CreateNetworkChangeTask("DriveMapperNetworkChange", exePath, 10);

            Console.WriteLine("Scheduled tasks created successfully.");
        }

        private static void CreateLogonTask(string taskName, string exePath, int delaySeconds)
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(taskName, false);

                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "DriveMapper - Runs at user logon";

                td.Triggers.Add(new LogonTrigger
                {
                    Delay = TimeSpan.FromSeconds(delaySeconds)
                });

                td.Actions.Add(new ExecAction(exePath));
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Principal.LogonType = TaskLogonType.InteractiveToken;
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;

                ts.RootFolder.RegisterTaskDefinition(taskName, td);
            }
        }

        private static void CreateNetworkChangeTask(string taskName, string exePath, int delaySeconds)
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(taskName, false);

                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "DriveMapper - Runs on network change";

                // Event IDs for network connect/disconnect in NetworkProfile/Operational
                td.Triggers.Add(new EventTrigger
                {
                    Subscription = @"<QueryList><Query Id='0' Path='Microsoft-Windows-NetworkProfile/Operational'>
                    <Select Path='Microsoft-Windows-NetworkProfile/Operational'>
                    *[System[Provider[@Name='Microsoft-Windows-NetworkProfile'] and (EventID=10000 or EventID=10001)]]
                    </Select></Query></QueryList>"
                });

                td.Actions.Add(new ExecAction(exePath));
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Principal.LogonType = TaskLogonType.InteractiveToken;
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;

                ts.RootFolder.RegisterTaskDefinition(taskName, td);
            }
        }
    }
}
