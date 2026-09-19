namespace JustFlip.Class
{
    public class StringExtensions
    {
        public static string MaskContactNo(string phoneNumber)
        {
            if (phoneNumber.Length >= 12)
            {
                string countryAndPrefix = phoneNumber.Substring(0, 5);
                string lastDigits = phoneNumber.Substring(phoneNumber.Length - 4);
                return $"{countryAndPrefix}*** *** {lastDigits}";
            }
            return phoneNumber;
        }
    }
}
