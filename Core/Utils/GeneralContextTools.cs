namespace graphnotelm.Core.Utils
{
    public class GeneralContextTools
    {
        public static string GetTimeDate(string timezone)
        {
            var zone = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(x => x.StandardName == timezone);
            if (zone == null)
            {
                return "Failed to retreive time.";
            }
            return zone.ToString();
        }
    }
}
