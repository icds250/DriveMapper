using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace Install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 6)
            {
                Console.WriteLine("Usage: Install.exe <TargetDirectory> <ExeName> <ScheduledTaskArguments> <install|uninstall> <createLogonTask:true|false> <createNetworkTask:true|false>");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" install true true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"-log -config config.json\" install true true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" uninstall false false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" DriveMapper.exe \"\" install true false");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <TargetDirectory>           Folder where files will be copied");
                Console.WriteLine("  <ExeName>                  Executable name (e.g. DriveMapper.exe)");
                Console.WriteLine("  <ScheduledTaskArguments>    Arguments passed to the executable in the scheduled tasks");
                Console.WriteLine("  <install|uninstall>         Install copies files and optionally creates scheduled tasks, uninstall removes them");
                Console.WriteLine("  <createLogonTask:true|false>    Whether to create or remove the logon triggered scheduled task");
                Console.WriteLine("  <createNetworkTask:true|false>  Whether to create or remove the network event triggered scheduled task");
                return;
            }

            string targetDir = args[0];
            string exeName = args[1];
            string taskArgs = args[2];
            string mode = args[3].ToLower();
            bool createLogonTask = bool.TryParse(args[4], out bool clt) && clt;
            bool createNetworkTask = bool.TryParse(args[5], out bool cnt) && cnt;

            string taskLogonName = Path.GetFileNameWithoutExtension(exeName) + "_Logon";
            string taskNetworkName = Path.GetFileNameWithoutExtension(exeName) + "_NetworkChange";

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

                    using (TaskService ts = new TaskService())
                    {
                        if (createLogonTask)
                            CreateScheduledTask(ts, taskLogonName, exePath, taskArgs, TaskTriggerType.Logon);

                        if (createNetworkTask)
                            CreateScheduledTask(ts, taskNetworkName, exePath, taskArgs, TaskTriggerType.NetworkChange);
                    }

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    using (TaskService ts = new TaskService())
                    {
                        if (createLogonTask)
                            ts.RootFolder.DeleteTask(taskLogonName, false);

                        if (createNetworkTask)
                            ts.RootFolder.DeleteTask(taskNetworkName, false);
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
                Console.WriteLine("Operation failed:");
                Console.WriteLine(ex.ToString());
            }
        }

        static void CreateScheduledTask(TaskService ts, string taskName, string exePath, string arguments, TaskTriggerType triggerType)
        {
            TaskDefinition td = ts.NewTask();
            td.RegistrationInfo.Description = $"Run {Path.GetFileName(exePath)} on {triggerType}";

            Trigger trigger = triggerType switch
            {
                TaskTriggerType.Logon => new LogonTrigger { Delay = TimeSpan.FromSeconds(10) },
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

            ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, "SYSTEM", null, TaskLogonType.ServiceAccount);
        }

        enum TaskTriggerType
        {
            Logon,
            NetworkChange
        }
    }
}
