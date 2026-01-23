using System;
using System.Linq;
namespace HealthTest;

public static class NhsNumberValidator
{
    /// <summary>
    /// Validates an NHS number using the Modulus 11 algorithm.
    /// </summary>
    /// <returns>true if valid; otherwise false</returns>
    public static bool IsValid(string nhsNumber)
    {
        if (string.IsNullOrWhiteSpace(nhsNumber))
            return false;

        // Must be exactly 10 digits
        if (nhsNumber.Length != 10 || !nhsNumber.All(char.IsDigit))
            return false;

        int sum = 0;

        // Digits 1–9, weights 10 down to 2
        for (int i = 0; i < 9; i++)
        {
            int digit = nhsNumber[i] - '0';
            int weight = 10 - i;
            sum += digit * weight;
        }

        int remainder = sum % 11;
        int calculatedCheckDigit = 11 - remainder;

        // A calculated check digit of 10 is invalid
        if (calculatedCheckDigit == 10)
            return false;

        // A calculated check digit of 11 maps to 0
        if (calculatedCheckDigit == 11)
            calculatedCheckDigit = 0;

        int actualCheckDigit = nhsNumber[9] - '0';

        return calculatedCheckDigit == actualCheckDigit;
    }
}
