using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.TTS160
{
    public class ProfileProperties
    {
        public bool TraceLogger { get; set; }
        public string ComPort { get; set; }
        public double SiteElevation {  get; set; }
        public short SlewSettleTime { get; set; }
        public double SiteLatitude { get; set; }
        public double SiteLongitude { get; set; }

    }
}
