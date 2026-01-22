namespace HealthTest
{
    public class AppSettings
  {
     public int Port {get; set;}= 5000;
     public string Host {get; set;}= "localhost";
     public int ExpectedNhsLength {get; set;}= 10;
     public bool LogPersonallyIdentifiableData {get; set;}= false;
     public string ApiEndpoint { get; set; } = string.Empty;
     public string ApiSecret { get; set; } = string.Empty;
     public string PatientNotFoundMessage { get; set; } = "Your details could not be found";
  }
}
