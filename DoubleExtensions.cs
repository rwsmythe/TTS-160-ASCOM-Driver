namespace ASCOM.TTS160
{
    public static class DoubleExtensions
    {
        public static bool InRange(this double value, double low, double high)
        {
            if (value < low)
                return false;
            if (value > high)
                return false;
            return true;
        }

        public static ComparisonResult Compare(this double value, double comparison)
        {
            var result = value.CompareTo(comparison);

            if (result < 0)
                return ComparisonResult.Lower;

            if (result == 0)
                return ComparisonResult.Equals;

            return ComparisonResult.Greater;
        }
    }
}