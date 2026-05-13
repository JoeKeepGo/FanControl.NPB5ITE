using System;
using Microsoft.Win32;

namespace FanControl.NPB5ITE
{
    public sealed class HardwareIdentity
    {
        public static readonly HardwareIdentity Unknown = new HardwareIdentity(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        public HardwareIdentity(
            string baseBoardManufacturer,
            string baseBoardProduct,
            string systemManufacturer,
            string systemProductName,
            string biosVendor,
            string biosVersion)
        {
            BaseBoardManufacturer = baseBoardManufacturer ?? string.Empty;
            BaseBoardProduct = baseBoardProduct ?? string.Empty;
            SystemManufacturer = systemManufacturer ?? string.Empty;
            SystemProductName = systemProductName ?? string.Empty;
            BiosVendor = biosVendor ?? string.Empty;
            BiosVersion = biosVersion ?? string.Empty;
        }

        public string BaseBoardManufacturer { get; }

        public string BaseBoardProduct { get; }

        public string SystemManufacturer { get; }

        public string SystemProductName { get; }

        public string BiosVendor { get; }

        public string BiosVersion { get; }

        public bool IsTestedNpb5RpBnb =>
            Contains(BaseBoardManufacturer, "Shenzhen Meigao") &&
            Contains(BaseBoardProduct, "RPBNB") &&
            Contains(SystemProductName, "Venus Series") &&
            Contains(BiosVendor, "American Megatrends");

        public string Summary =>
            "BaseBoardManufacturer='" + BaseBoardManufacturer +
            "', BaseBoardProduct='" + BaseBoardProduct +
            "', SystemManufacturer='" + SystemManufacturer +
            "', SystemProductName='" + SystemProductName +
            "', BiosVendor='" + BiosVendor +
            "', BiosVersion='" + BiosVersion + "'";

        public static HardwareIdentity DetectSystem()
        {
            if (!OperatingSystem.IsWindows())
            {
                return Unknown;
            }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false))
                {
                    if (key == null)
                    {
                        return Unknown;
                    }

                    return new HardwareIdentity(
                        GetString(key, "BaseBoardManufacturer"),
                        GetString(key, "BaseBoardProduct"),
                        GetString(key, "SystemManufacturer"),
                        GetString(key, "SystemProductName"),
                        GetString(key, "BIOSVendor"),
                        GetString(key, "BIOSVersion"));
                }
            }
            catch
            {
                return Unknown;
            }
        }

        private static string GetString(RegistryKey key, string name)
        {
            return key.GetValue(name)?.ToString() ?? string.Empty;
        }

        private static bool Contains(string value, string expected)
        {
            return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
