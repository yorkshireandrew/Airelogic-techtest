namespace HealthTest
{
    public class LandingFormModel
    {
        public string nhs { get; set; } = string.Empty;
        public string surname { get; set; } = string.Empty;
        public string day { get; set; } = string.Empty;
        public string month { get; set; } = string.Empty;
        public string year { get; set; } = string.Empty;

        public bool NhsIsValid(string nhs)
        {
            if (nhs.Length != 9 && nhs.Length != 10) return false;
            foreach (char c in nhs)
            {
                if (!char.IsDigit(c)) return false;
            }
            if (nhs.Length == 10)
            {
                return NhsNumberValidator.IsValid(nhs);
            }
            return true;
        }
    }
}
