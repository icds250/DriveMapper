using System;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace Install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 7)
            {
                Console.WriteLine("Usage: Install.exe <TargetDirectory> <ExeName> <ScheduledTaskArguments> <install|uninstall> <createLogonTask:true|false> <createBootTask:true|false> <createNetworkChangeTask:true|false>");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" install true true true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"-log -config config.json\" install true true true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" uninstall false false false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" install true false false");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <TargetDirectory>            Folder where files will be copied");
                Console.WriteLine("  <ExeName>                   Executable name to run in scheduled tasks");
                Console.WriteLine("  <ScheduledTaskArguments>     Arguments passed to the executable in the scheduled tasks");
                Console.WriteLine("  <install|uninstall>          Install copies files and optionally creates scheduled tasks");
                Console.WriteLine("  <createLogonTask:true|false>        Create or remove Logon trigger task");
                Console.WriteLine("  <createBootTask:true|false>          Create or remove Boot trigger task");
                Console.WriteLine("  <createNetworkChangeTask:true|false> Create or remove Network Change event trigger task");
                return;
            }

            string targetDir = args[0];
            string exeName = args[1];
            string taskArgs = args[2];
            string mode = args[3].ToLower();
            bool createLogonTask = bool.TryParse(args[4], out bool logon) && logon;
            bool createBootTask = bool.TryParse(args[5], out bool boot) && boot;
            bool createNetworkChangeTask = bool.TryParse(args[6], out bool netchange) && netchange;

            string taskLogonName = exeName + "_Logon";
            string taskBootName = exeName + "_Boot";
            string taskNetworkName = exeName + "_NetworkChange";

            try
            {
                if (mode == "install")
                {
                    Directory.CreateDirectory(targetDir);

                    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (string file in Directory.GetFiles(currentDir))
                    {
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(targetDir, fileName);
                        File.Copy(file, destPath, true);
                    }

                    string exePath = Path.Combine(targetDir, exeName);

                    if (createLogonTask)
                    {
                        CreateScheduledTask(taskLogonName, exePath, taskArgs, TaskTriggerType.Logon);
                    }
                    if (createBootTask)
                    {
                        CreateScheduledTask(taskBootName, exePath, taskArgs, TaskTriggerType.Boot);
                    }
                    if (createNetworkChangeTask)
                    {
                        CreateScheduledTask(taskNetworkName, exePath, taskArgs, TaskTriggerType.NetworkChange);
                    }

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    using (TaskService ts = new TaskService())
                    {
                        if (createLogonTask) ts.RootFolder.DeleteTask(taskLogonName, false);
                        if (createBootTask) ts.RootFolder.DeleteTask(taskBootName, false);
                        if (createNetworkChangeTask) ts.RootFolder.DeleteTask(taskNetworkName, false);
                    }

                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, true);
                    }

                    Console.WriteLine("Uninstallation complete.");
                }
                else
                {
                    Console.WriteLine("Unknown mode. Use 'install' or 'uninstall'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Operation failed: {ex.Message}");
            }
        }

        static void CreateScheduledTask(string taskName, string exePath, string arguments, TaskTriggerType triggerType)
        {
            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = $"Run {Path.GetFileName(exePath)} on {triggerType}";

                Trigger trigger = triggerType switch
                {
                    TaskTriggerType.Logon => new LogonTrigger { Delay = TimeSpan.FromSeconds(10) },
                    TaskTriggerType.Boot => new BootTrigger { Delay = TimeSpan.FromSeconds(10) },
                    TaskTriggerType.NetworkChange => new EventTrigger
                    {
                        Subscription = @"<QueryList><Query Id='0' Path='Microsoft-Windows-NetworkProfile/Operational'>
                                         <Select Path='Microsoft-Windows-NetworkProfile/Operational'>
                                         *[System[Provider[@Name='Microsoft-Windows-NetworkProfile'] and (EventID=10000 or EventID=10001)]]
                                         </Select></Query></QueryList>"
                    },
                    _ => throw new ArgumentException("Unsupported trigger type")
                };
                td.Triggers.Add(trigger);

                td.Actions.Add(new ExecAction(exePath, arguments, null));

                string currentUser = WindowsIdentity.GetCurrent().Name;
                ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, currentUser, null, TaskLogonType.InteractiveToken);
            }
        }

        enum TaskTriggerType
        {
            Logon,
            Boot,
            NetworkChange
        }
    }
}
