
namespace HealthTest
{
    public class PatientModel
    {
        public string nhsNumber { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string born { get; set; } = string.Empty;

        public bool SurnameMatches(string surname)
        {   
            var surnameFromName = name.Split(',')[0];
            var trimmedSurname = surnameFromName.Trim();
            return trimmedSurname.Equals(surname, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool NhsNumberMatches(string nhs)
        {
            if(nhs.Length != 9) throw new System.ArgumentException("NHS number must be 9 digits for comparison.");
            return nhsNumber.Equals(nhs, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool DateOfBirthMatches(string day, string month, string year)
        {
            var dobParts = born.Split('-');
            if(dobParts.Length != 3) throw new System.FormatException("API Date of birth is not in the expected format DD-MM-YYYY");

            var birthDay   = dobParts[0].TrimStart('0');
            var birthMonth = dobParts[1].TrimStart('0');
            var birthYear  = dobParts[2].TrimStart('0');

            var inputDay = day.TrimStart('0');
            var inputMonth = month.TrimStart('0');
            var inputYear = year.TrimStart('0');

            return birthDay == inputDay && birthMonth == inputMonth && birthYear == inputYear;
        }

        public int GetAge(DateTime today)
        {
            var dobParts = born.Split('-');
            if (dobParts.Length != 3)
                throw new System.FormatException("API Date of birth is not in the expected format DD-MM-YYYY");

            int day = int.Parse(dobParts[0]);
            int month = int.Parse(dobParts[1]);
            int year = int.Parse(dobParts[2]);

            var birthDate = new System.DateTime(year, month, day);
            int age = today.Year - birthDate.Year;
            if (today.Month < birthDate.Month || (today.Month == birthDate.Month && today.Day < birthDate.Day))
                age--;
            return age;
        }

        public int CalculateAge(DateTime today)
        {
            var dobParts = born.Split('-');
            if (dobParts.Length != 3)
                throw new System.FormatException("API Date of birth is not in the expected format DD-MM-YYYY");

            int day = int.Parse(dobParts[0]);
            int month = int.Parse(dobParts[1]);
            int year = int.Parse(dobParts[2]);
            var dateOfBirth = new System.DateTime(year, month, day);

            int age = today.Year - dateOfBirth.Year;

            // If we step back the year difference and we're before the birth date, we need to subtract one
            // In other words, if the birthday hasn't occurred yet this year
            if (today.Date.AddYears(-age) < dateOfBirth.Date)
            {
                age--;
            }

            return age;
        }
    }
}
