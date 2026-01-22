namespace HealthTest
{
    public class AppSettings
  {
     public int Port {get; set;}= 5000;
     public string Host {get; set;}= "localhost";

     public bool LogPersonallyIdentifiableData {get; set;}= false;
     public string ApiEndpoint { get; set; } = string.Empty;
  }
}
