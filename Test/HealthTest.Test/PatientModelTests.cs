using System;
using Xunit;

namespace HealthTest.Test
{
    public class PatientModelTests
    {
        [Theory]
        [InlineData("Smith, John", "Smith", true)]
        [InlineData("Smith, John", "smith", true)]
        [InlineData("Smith, John", "Smyth", false)]
        [InlineData("O'Neil, Jane", "O'Neil", true)]
        [InlineData("O'Neil, Jane", "O'neil", true)]
        [InlineData("Brown, Bob", "Brown", true)]
        [InlineData("Brown, Bob", "Browne", false)]
        public void SurnameMatches_WorksAsExpected(string name, string inputSurname, bool expected)
        {
            var patient = new PatientModel { name = name };
            Assert.Equal(expected, patient.SurnameMatches(inputSurname));
        }

        [Theory]
        [InlineData("123456789", "123456789", true)]
        [InlineData("123456789", "123456788", false)]
        [InlineData("123456789", "12345678", false)] // too short, should throw
        [InlineData("123456789", "1234567890", false)] // too long, should throw
        public void NhsNumberMatches_WorksAsExpected(string nhsNumber, string inputNhs, bool expected)
        {
            var patient = new PatientModel { nhsNumber = nhsNumber };
            if (inputNhs.Length != 9)
            {
                Assert.Throws<System.ArgumentException>(() => patient.NhsNumberMatches(inputNhs));
            }
            else
            {
                Assert.Equal(expected, patient.NhsNumberMatches(inputNhs));
            }
        }
        
        [Theory]
        // Matching, with and without leading zeros
        [InlineData("01-02-1990", "1", "2", "1990", true)]
        [InlineData("01-02-1990", "01", "02", "1990", true)]
        [InlineData("1-2-1990", "01", "02", "1990", true)]
        [InlineData("10-12-2000", "10", "12", "2000", true)]
        [InlineData("10-12-2000", "10", "12", "2001", false)]
        [InlineData("10-12-2000", "11", "12", "2000", false)]
        [InlineData("10-12-2000", "10", "11", "2000", false)]
        public void DateOfBirthMatches_WorksAsExpected(string born, string day, string month, string year, bool expected)
        {
            var patient = new PatientModel { born = born };
            Assert.Equal(expected, patient.DateOfBirthMatches(day, month, year));
        }

        [Theory]
        [InlineData("")]
        [InlineData("01-02")]
        [InlineData("01-02-1990-05")]
        public void DateOfBirthMatches_ThrowsFormatException_OnInvalidFormat(string born)
        {
            var patient = new PatientModel { born = born };
            Assert.Throws<System.FormatException>(() => patient.DateOfBirthMatches("1", "2", "1990"));
        }

        [Theory]
        [InlineData("01-01-2000", "2026-01-02", 26)]  // Birthday has occurred this year
        [InlineData("22-01-2000", "2026-01-22", 26)]  // Birthday is today
        [InlineData("23-01-2000", "2026-01-22", 25)]  // Birthday has not occurred yet this year
        [InlineData("29-02-2000", "2025-02-28", 24)]  // Leap year birthday, not leap year now
        [InlineData("29-02-2000", "2024-02-29", 24)]  // Leap year birthday, leap year now
        [InlineData("15-06-2000", "2026-01-22", 25)]  // Birthday month not reached yet, but day is higher than birth day
        public void CalculateAge_WorksAsExpected(string born, string nowString, int expectedAge)
        {
            var now = DateTime.Parse(nowString);
            var patient = new PatientModel { born = born };
            Assert.Equal(expectedAge, patient.CalculateAge(now));
        }

        [Theory]
        [InlineData("")]
        [InlineData("01-02")]
        [InlineData("01-02-1990-05")]
        public void CalculateAge_ThrowsFormatException_OnInvalidFormat(string born)
        {
            var patient = new PatientModel { born = born };
            Assert.Throws<System.FormatException>(() => patient.CalculateAge(DateTime.Today));
        }
    }
}
