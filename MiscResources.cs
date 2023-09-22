using System;
using System.Xml.Schema;
using ASCOM.DeviceInterface;

namespace ASCOM.TTS160
{
    internal class MiscResources
    {
        private static bool _movingPrimary = false;
        public static bool MovingPrimary
        {
            get => _movingPrimary;
            internal set => _movingPrimary = value;
        }

        private static bool _movingSecondary = false;
        public static bool MovingSecondary
        {
            get => _movingSecondary;
            internal set => _movingSecondary = value;
        }

        private static bool _isPulseGuiding = false;
        public static bool IsPulseGuiding
        {
            get => _isPulseGuiding;
            internal set => _isPulseGuiding = value;
        }

        public class HorizonCoordinates
        {
            public double Altitude { get; set; }
            public double Azimuth { get; set; }
        }
        public class EquatorialCoordinates
        {
            public double RightAscension { get; set; }
            public double Declination { get; set; }
        }

        private static bool _isSlewing = false;
        public static bool IsSlewing
        {
            get => _isSlewing;
            internal set => _isSlewing = value;
        }

        private static bool _isSlewingToTarget = false;
        public static bool IsSlewingToTarget
        {
            get => _isSlewingToTarget;
            internal set => _isSlewingToTarget = value;
        }

        private static bool _isTargetRASet = false;
        public static bool IsTargetRASet
        {
            get => _isTargetRASet;
            internal set => _isTargetRASet = value;
        }

        private static bool _isTargetDecSet = false;
        public static bool IsTargetDecSet
        {
            get => _isTargetDecSet;
            internal set => _isTargetDecSet = value;
        }

        private static bool _isTargetSet = false;
        public static bool IsTargetSet
        {
            get => _isTargetSet;
            internal set => _isTargetSet = value;
        }

        private static EquatorialCoordinates _Target = new EquatorialCoordinates();
        public static EquatorialCoordinates Target
        {
            get => _Target;
            internal set => _Target = value;

        }

        public static short _SettleTime = 2;

        public static short SettleTime
        {
            get => _SettleTime;
            internal set => _SettleTime = value;
        }
        private static Boolean _isSlewingAsync = false;
        public static Boolean IsSlewingAsync
        {
            get => _isSlewingAsync;
            internal set => _isSlewingAsync = value;
        }

        private static DateTime _SlewSettleStart = DateTime.MinValue;

        public static DateTime SlewSettleStart
        {
            get => _SlewSettleStart;
            internal set => _SlewSettleStart = value;
        }

        private static DriveRates _TrackingRateCurrent = DriveRates.driveSidereal;
        public static DriveRates TrackingRateCurrent
        {
            get => _TrackingRateCurrent;
            internal set => _TrackingRateCurrent = value;

        }

        private static bool _IsParked = false;
        public static bool IsParked
        {
            get => _IsParked;
            internal set => _IsParked = value;
        }

        private static bool _SlewAltAzTrackOverride = false;
        public static bool SlewAltAzTrackOverride
        {
            get => _SlewAltAzTrackOverride;
            internal set => _SlewAltAzTrackOverride = value;

        }
        private static bool _TrackSetFollower = true; //try to follow the track setting
        public static bool TrackSetFollower
        {
            get => _TrackSetFollower;
            internal set => _TrackSetFollower = value;
        }
    }
}
