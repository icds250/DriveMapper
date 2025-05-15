using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace Install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: Install.exe <TargetDirectory> <ScheduledTaskArguments> <ExeName> <install|uninstall> <createTasks:true|false>");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"-log -config config.json\" DriveMapper.exe install true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" DriveMapper.exe uninstall false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" DriveMapper.exe install false");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <TargetDirectory>           Folder where files will be copied");
                Console.WriteLine("  <ScheduledTaskArguments>    Arguments passed to the exe in the scheduled tasks");
                Console.WriteLine("  <ExeName>                  Executable name (e.g., DriveMapper.exe)");
                Console.WriteLine("  <install|uninstall>         Install copies files and optionally creates scheduled tasks");
                Console.WriteLine("  <createTasks:true|false>    Whether to create or remove scheduled tasks during install/uninstall");
                return;
            }

            string targetDir = args[0];
            string taskArgs = args[1];
            string exeName = args[2];
            string mode = args[3].ToLower();
            bool createTasks = bool.TryParse(args[4], out bool ct) && ct;

            string taskLogonName = exeName + "Logon";
            string taskNetworkName = exeName + "NetworkChange";

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

                    if (createTasks)
                    {
                        string exePath = Path.Combine(targetDir, exeName);
                        CreateScheduledTask(taskLogonName, exePath, taskArgs, TaskTriggerType.Logon);
                        CreateScheduledTask(taskNetworkName, exePath, taskArgs, TaskTriggerType.NetworkChange);
                    }

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    if (createTasks)
                    {
                        using (TaskService ts = new TaskService())
                        {
                            ts.RootFolder.DeleteTask(taskLogonName, false);
                            ts.RootFolder.DeleteTask(taskNetworkName, false);
                        }
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

                switch (triggerType)
                {
                    case TaskTriggerType.Logon:
                        td.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromSeconds(10) });
                        break;

                    case TaskTriggerType.NetworkChange:
                        td.Triggers.Add(new EventTrigger
                        {
                            Subscription = @"<QueryList><Query Id='0' Path='Microsoft-Windows-NetworkProfile/Operational'>
                                <Select Path='Microsoft-Windows-NetworkProfile/Operational'>
                                    *[System[Provider[@Name='Microsoft-Windows-NetworkProfile'] and (EventID=10000 or EventID=10001)]]
                                </Select>
                            </Query></QueryList>"
                        });
                        break;

                    default:
                        throw new ArgumentException("Unsupported trigger type");
                }

                td.Actions.Add(new ExecAction(exePath, arguments, null));

                ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, "SYSTEM", null, TaskLogonType.ServiceAccount);
            }
        }

        enum TaskTriggerType
        {
            Logon,
            NetworkChange
        }
    }
}
