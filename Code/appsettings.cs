namespace HealthTest
{

  // Application configuration settings mapped from appsettings.json. The strings in here are recognisable defaults.
  public class AppSettings
  {
     public int Port {get; set;}= 5000;
     public string Host {get; set;}= "localhost";
     public bool LogPersonallyIdentifiableData {get; set;}= false;
     public string ApiEndpoint { get; set; } = string.Empty;
     public string ApiSecret { get; set; } = string.Empty;
     public bool InformUserWhenNhsNumberFormatIncorrect { get; set; } = false;
     public string PatientNotFoundMessage { get; set; } = "Your details could not be found...";
     public string NotEligibleMessage { get; set; } = "You are not eligible for this service...";
     public string WelldoneMessage { get; set; } = "Thank you for completing the questionnaire...";
     public string TellOffMessage { get; set; } = "Based on your answers, we recommend you seek further medical advice...";
     public List<List<int>> AgeBands { get; set; } = new List<List<int>>();
     public List<string> Questions { get; set; } = new List<string>();
     public List<QuestionScoreSetting> QuestionScoreSettings { get; set; } = new List<QuestionScoreSetting>();
     public int TellOffIfScoreExceeds { get; set; } = 0;
  }
}
