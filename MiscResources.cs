using System;

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

        private static bool _isGuiding = false;
        public static bool IsGuiding
        {
            get => _isGuiding;
            internal set => _isGuiding = value;
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

        private static bool _isPulseGuideInProgress = false;
        public static bool IsPulseGuideInProgress
        {
            get => _isPulseGuideInProgress;
            internal set => _isPulseGuideInProgress = value;
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
    }
}
