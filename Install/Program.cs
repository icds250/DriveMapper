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
                Console.WriteLine("Usage: Install.exe <TargetDirectory> <ScheduledTaskArguments> <install|uninstall> <CreateLogonTask:true|false> <CreateNetworkTask:true|false>");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"-log -config config.json\" install true true");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" uninstall false false");
                Console.WriteLine("  Install.exe \"C:\\Program Files\\DriveMapper\" \"\" install true false");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <TargetDirectory>             Folder where files will be copied");
                Console.WriteLine("  <ScheduledTaskArguments>      Arguments passed to the EXE in the scheduled tasks");
                Console.WriteLine("  <install|uninstall>           Install copies files and optionally creates scheduled tasks");
                Console.WriteLine("  <CreateLogonTask:true|false>  Whether to create/remove the logon task");
                Console.WriteLine("  <CreateNetworkTask:true|false>Whether to create/remove the network trigger task");
                return;
            }

            string targetDir = args[0];
            string taskArgs = args[1];
            string mode = args[2].ToLower();
            bool createLogon = bool.TryParse(args[3], out bool cl) && cl;
            bool createNetwork = bool.TryParse(args[4], out bool cn) && cn;

            string exeName = "DriveMapper.exe";
            string taskLogonName = exeName.Replace(".exe", "") + "Logon";
            string taskNetworkName = exeName.Replace(".exe", "") + "NetworkChange";

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

                    if (createLogon)
                    {
                        CreateScheduledTask(taskLogonName, exePath, taskArgs, TaskTriggerType.Logon);
                    }

                    if (createNetwork)
                    {
                        CreateScheduledTask(taskNetworkName, exePath, taskArgs, TaskTriggerType.NetworkChange);
                    }

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    using (TaskService ts = new TaskService())
                    {
                        if (createLogon)
                            ts.RootFolder.DeleteTask(taskLogonName, false);
                        if (createNetwork)
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
        }

        enum TaskTriggerType
        {
            Logon,
            NetworkChange
        }
    }
}
