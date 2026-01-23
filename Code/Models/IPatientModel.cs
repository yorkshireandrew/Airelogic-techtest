namespace HealthTest
{
    public interface IPatientModel
    {
        string nhsNumber { get; set; }
        string name { get; set; }
        string born { get; set; }

        bool SurnameMatches(string surname);
        bool NhsNumberMatches(string nhs);
        bool DateOfBirthMatches(string day, string month, string year);
        int CalculateAge(System.DateTime today);
    }
}
