using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;

namespace Install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2 || (args[1].ToLower() != "install" && args[1].ToLower() != "uninstall"))
            {
                Console.WriteLine("Usage: Install.exe <config.json> <install|uninstall>");
                return;
            }

            string configPath = args[0];
            string mode = args[1].ToLower();

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found: {configPath}");
                return;
            }

            Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

            string logonTaskName = config.ExeName;
            string networkTaskName = config.ExeName + "_NetworkChange";
            string bootTaskName = config.ExeName + "_Boot";

            try
            {
                if (mode == "install")
                {
                    Directory.CreateDirectory(config.TargetDirectory);

                    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (string file in Directory.GetFiles(currentDir))
                    {
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(config.TargetDirectory, fileName);
                        File.Copy(file, destPath, true);
                    }

                    string exePath = Path.Combine(config.TargetDirectory, config.ExeName);

                    foreach (var task in config.ScheduledTasks)
                    {
                        if (task.CreateLogonTask)
                            CreateScheduledTask(task.TaskName ?? logonTaskName, exePath, task.Arguments, TaskTriggerType.Logon);
                        if (task.CreateNetworkTask)
                            CreateScheduledTask((task.TaskName ?? logonTaskName) + "_NetworkChange", exePath, task.Arguments, TaskTriggerType.NetworkProfile);
                        if (task.CreateBootTask)
                            CreateScheduledTask((task.TaskName ?? logonTaskName) + "_Boot", exePath, task.Arguments, TaskTriggerType.Boot);
                    }

                    if (!string.IsNullOrWhiteSpace(config.ShortcutName))
                        CreateStartMenuShortcut(config.ShortcutName, exePath);

                    Console.WriteLine("Installation complete.");
                }
                else if (mode == "uninstall")
                {
                    using (TaskService ts = new TaskService())
                    {
                        foreach (var task in config.ScheduledTasks)
                        {
                            if (task.CreateLogonTask)
                                ts.RootFolder.DeleteTask(task.TaskName ?? logonTaskName, false);
                            if (task.CreateNetworkTask)
                                ts.RootFolder.DeleteTask((task.TaskName ?? logonTaskName) + "_NetworkChange", false);
                            if (task.CreateBootTask)
                                ts.RootFolder.DeleteTask((task.TaskName ?? logonTaskName) + "_Boot", false);
                        }
                    }

                    if (Directory.Exists(config.TargetDirectory))
                        Directory.Delete(config.TargetDirectory, true);

                    if (!string.IsNullOrWhiteSpace(config.ShortcutName))
                    {
                        string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", config.ShortcutName + ".lnk");
                        if (File.Exists(startMenuPath))
                            File.Delete(startMenuPath);
                    }

                    Console.WriteLine("Uninstallation complete.");
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
                ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, "Authenticated Users", null, TaskLogonType.Group);
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        static void CreateStartMenuShortcut(string shortcutName, string targetPath)
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", shortcutName + ".lnk");

            IShellLink link = (IShellLink)new ShellLink();
            link.SetDescription(shortcutName);
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath));

            ((IPersistFile)link).Save(shortcutPath, false);
        }

        enum TaskTriggerType
        {
            Logon,
            NetworkProfile,
            Boot
        }

        class Config
        {
            public string TargetDirectory { get; set; }
            public string ExeName { get; set; }
            public string ShortcutName { get; set; }
            public List<ScheduledTaskConfig> ScheduledTasks { get; set; }
        }

        class ScheduledTaskConfig
        {
            public string TaskName { get; set; }
            public string Arguments { get; set; }
            public bool CreateLogonTask { get; set; }
            public bool CreateNetworkTask { get; set; }
            public bool CreateBootTask { get; set; }
        }
    }
}
