using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public class GeneralContextTools
    {
        [Description("Gets the current date and time in the specified Windows timezone (e.g. 'Eastern Standard Time', 'UTC', 'Pacific Standard Time').")]
        public string GetTimeDate(
            [Description("The Windows timezone name, e.g. 'Eastern Standard Time'.")]
            string timezone)
        {
            var zone = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(x => x.StandardName.Equals(timezone, StringComparison.OrdinalIgnoreCase)
                                  || x.Id.Equals(timezone, StringComparison.OrdinalIgnoreCase));
            if (zone == null)
                return $"Unknown timezone '{timezone}'. Use a Windows timezone name like 'Eastern Standard Time'.";

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            return localTime.ToString("yyyy-MM-dd HH:mm:ss zzz");
        }
    }
}
