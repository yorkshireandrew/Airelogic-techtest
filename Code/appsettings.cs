namespace HealthTest
{
    public class AppSettings
  {
     public int Port {get; set;}= 5000;
     public string Host {get; set;}= "localhost";
     public bool LogPersonallyIdentifiableData {get; set;}= false;
     public string ApiEndpoint { get; set; } = string.Empty;
     public string ApiSecret { get; set; } = string.Empty;
     public bool InformUserWhenNhsNumberFormatIncorrect { get; set; } = false;
     public string PatientNotFoundMessage { get; set; } = "Your details could not be found";
     public string NotEligibleMessage { get; set; } = "You are not eligible for this service";
     public List<List<int>> AgeBands { get; set; } = new List<List<int>>();

  }
}
