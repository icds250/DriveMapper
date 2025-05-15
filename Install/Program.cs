using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace Install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 7)
            {
                Console.WriteLine("Usage: Install.exe <TargetDirectory> <ScheduledTaskArguments> <exeName> <install|uninstall> <createLogonTask:true|false> <createNetworkTask:true|false> <createBootTask:true|false>");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"-log -config config.json\" DriveMapper.exe install true true false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" DriveMapper.exe uninstall false false false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" DriveMapper.exe install true false false");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <TargetDirectory>           Folder where files will be copied");
                Console.WriteLine("  <ScheduledTaskArguments>    Arguments passed to exe in the scheduled tasks");
                Console.WriteLine("  <exeName>                  Executable name to run");
                Console.WriteLine("  <install|uninstall>         Install copies files and optionally creates scheduled tasks");
                Console.WriteLine("  <createLogonTask:true|false>    Create or delete Logon trigger task");
                Console.WriteLine("  <createNetworkTask:true|false>  Create or delete Network Profile Event trigger task");
                Console.WriteLine("  <createBootTask:true|false>     Create or delete Boot trigger task");
                return;
            }

            string targetDir = args[0];
            string taskArgs = args[1];
            string exeName = args[2];
            string mode = args[3].ToLower();
            bool createLogonTask = bool.TryParse(args[4], out bool clt) && clt;
            bool createNetworkTask = bool.TryParse(args[5], out bool cnt) && cnt;
            bool createBootTask = bool.TryParse(args[6], out bool cbt) && cbt;

            string logonTaskName = exeName;
            string networkTaskName = exeName + "_NetworkChange";
            string bootTaskName = exeName + "_Boot";

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
                        CreateScheduledTask(logonTaskName, exePath, taskArgs, TaskTriggerType.Logon);

                    if (createNetworkTask)
                        CreateScheduledTask(networkTaskName, exePath, taskArgs, TaskTriggerType.NetworkProfile);

                    if (createBootTask)
                        CreateScheduledTask(bootTaskName, exePath, taskArgs, TaskTriggerType.Boot);

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    using (TaskService ts = new TaskService())
                    {
                        if (createLogonTask)
                            ts.RootFolder.DeleteTask(logonTaskName, false);

                        if (createNetworkTask)
                            ts.RootFolder.DeleteTask(networkTaskName, false);

                        if (createBootTask)
                            ts.RootFolder.DeleteTask(bootTaskName, false);
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
                    TaskTriggerType.NetworkProfile => new EventTrigger
                    {
                        Subscription = @"<QueryList><Query Id='0' Path='Microsoft-Windows-NetworkProfile/Operational'>
                            <Select Path='Microsoft-Windows-NetworkProfile/Operational'>
                            *[System[Provider[@Name='Microsoft-Windows-NetworkProfile'] and (EventID=10000 or EventID=10001)]]
                            </Select></Query></QueryList>"
                    },
                    TaskTriggerType.Boot => new BootTrigger { Delay = TimeSpan.FromSeconds(10) },
                    _ => throw new ArgumentException("Unsupported trigger type")
                };

                td.Triggers.Add(trigger);

                td.Actions.Add(new ExecAction(exePath, arguments, null));

                // Configure the principal directly instead of assigning a new instance
                //td.Principal.UserId = "SYSTEM";
                //td.Principal.LogonType = TaskLogonType.ServiceAccount;
                //td.Principal.GroupId = "S-1-5-32-545"; // Users group SID

                ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, "Authenticated Users", null, TaskLogonType.Group);
            }
        }

        enum TaskTriggerType
        {
            Logon,
            NetworkProfile,
            Boot
        }
    }
}
