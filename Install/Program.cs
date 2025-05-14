using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DriveMapperInstaller
{
    class Installer
    {
        static void Main(string[] args)
        {
            //string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\DriveMapper\bin\Release\DriveMapper.exe");
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            CreateScheduledTask("DriveMapperLogon", exePath, "AtLogon");
            CreateScheduledTask("DriveMapperNetworkChange", exePath, "OnEvent");
        }

        static void CreateScheduledTask(string taskName, string exePath, string trigger)
        {
            var xml = $@"
<Task version='1.2' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>
  <Triggers>
    {(trigger == "AtLogon" ? "<LogonTrigger><Enabled>true</Enabled></LogonTrigger>" : "<EventTrigger><Subscription><QueryList><Query Id='0'><Select Path='Microsoft-Windows-NetworkProfile/Operational'>*[System[Provider[@Name='Microsoft-Windows-NetworkProfile'] and (EventID=10000 or EventID=10001)]]</Select></Query></QueryList></Subscription></EventTrigger>")}
  </Triggers>
  <Principals>
    <Principal id='Author'>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context='Author'>
    <Exec>
      <Command>{exePath}</Command>
    </Exec>
  </Actions>
</Task>";

            string tempXml = Path.GetTempFileName();
            File.WriteAllText(tempXml, xml);
            System.Diagnostics.Process.Start("schtasks.exe", $"/Create /TN {taskName} /XML {tempXml} /F").WaitForExit();
            File.Delete(tempXml);
        }
    }
}