namespace HealthTest
{
    public class LandingFormModel
    {
        public string nhs { get; set; } = string.Empty;
        public string surname { get; set; } = string.Empty;
        public string day { get; set; } = string.Empty;
        public string month { get; set; } = string.Empty;
        public string year { get; set; } = string.Empty;

        public bool NhsIsValidFormat(string nhs, int expectedLength = 10)
        {
            if (nhs.Length != expectedLength) return false;
            foreach (char c in nhs)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }
    }
}
