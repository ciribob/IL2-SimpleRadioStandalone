using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public class Analytics
    {
        public static void Log(string eventCategory, string eventAction, string guid)
        {
#if !DEBUG
            var http = new HttpClient()
            {
                BaseAddress = new Uri("http://www.google-analytics.com/")
            };
            http.DefaultRequestHeaders.Add("User-Agent", "IL2-SRS");
            http.DefaultRequestHeaders.ExpectContinue = false;

            try
            {
                var content =
                    new StringContent(
                        $"v=1&tid=UA-115685293-1&cid={guid}&t=event&ec={eventCategory}&ea={eventAction}&el={UpdaterChecker.VERSION}",
                        Encoding.ASCII, "application/x-www-form-urlencoded");
                http.PostAsync("collect", content);
            }
            catch
            {
            }
#endif
        }
    }
}