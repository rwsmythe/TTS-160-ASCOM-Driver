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
        public int CompatMode { get; set; }  //Attempts to improve specific app compatibility by "enabling" benign, unimplemented ASCOM functions
        public bool CanSetTrackingOverride { get; set; }
        public bool CanSetGuideRatesOverride { get; set; }
        public bool SyncTimeOnConnect { get; set; }
        public int GuideComp { get; set; }  //Improves guiding by compensating pulse length and/or pulse direction
        public int GuideCompMaxDelta { get; set; }
        public int GuideCompBuffer { get; set; }

           
    }
}
