using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Newtonsoft.Json; // Replace System.Text.Json with Newtonsoft.Json
using System.Linq;

namespace DriveMapper
{
    class Program
    {
        static void Main(string[] args)
        {
            string configPath = AppDomain.CurrentDomain.BaseDirectory + "config.json";
            if (!File.Exists(configPath))
            {
                Console.WriteLine("Configuration file not found.");
                return;
            }

            var configContent = File.ReadAllText(configPath);
            var mappings = JsonConvert.DeserializeObject<List<DriveMapping>>(configContent); // Use JsonConvert instead of JsonSerializer
            var user = WindowsIdentity.GetCurrent();
            var userName = user.Name.Split('\\')[1];
            var userGroups = GetUserGroups(userName);

            foreach (var mapping in mappings)
            {
                if (userGroups.Any(group => group.Equals(mapping.Group, StringComparison.OrdinalIgnoreCase)))
                {
                    MapDrive(mapping);
                }
            }
        }

        static List<string> GetUserGroups(string username)
        {
            var groups = new List<string>();
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var user = UserPrincipal.FindByIdentity(context, username);
                    if (user != null)
                    {
                        foreach (var group in user.GetAuthorizationGroups())
                        {
                            groups.Add(group.SamAccountName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving groups for user {username}: {ex.Message}");
            }
            return groups;
        }

        static void MapDrive(DriveMapping mapping)
        {
            WNetCancelConnection2(mapping.DriveLetter + ":", 0, true);
            var result = WNetAddConnection2(new NETRESOURCE
            {
                dwType = 1,
                lpLocalName = mapping.DriveLetter + ":",
                lpRemoteName = mapping.Path,
                lpProvider = null
            }, null, null, 0);

            if (result != 0)
            {
                Console.WriteLine($"Failed to map drive {mapping.DriveLetter}: to {mapping.Path}. Error code: {result}");
                // Add specific error handling based on the error code
            }
        }

        [DllImport("mpr.dll")]
        static extern int WNetAddConnection2(NETRESOURCE netResource, string password, string username, int flags);

        [DllImport("mpr.dll")]
        static extern int WNetCancelConnection2(string name, int flags, bool force);

        [StructLayout(LayoutKind.Sequential)]
        public class NETRESOURCE
        {
            public int dwScope = 0;
            public int dwType = 1;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment = "";
            public string lpProvider = "";
        }

        public class DriveMapping
        {
            public string Name { get; set; }
            public string Group { get; set; }
            public string Path { get; set; }
            public string DriveLetter { get; set; }
        }
    }
}