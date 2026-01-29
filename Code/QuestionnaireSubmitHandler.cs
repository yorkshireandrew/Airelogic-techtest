namespace HealthTest;

public class QuestionnaireSubmitHandler
{
  private QuestionnaireFormParser _parser;
  private QuestionnaireScorer _scorer;

  private readonly string _welldoneMessage;
  private readonly string _tellOffMessage;

  public QuestionnaireSubmitHandler(QuestionnaireFormParser parser, QuestionnaireScorer scorer, AppSettings config)
  {
      _parser = parser;
      _scorer = scorer;
      _welldoneMessage = config.WelldoneMessage;
      _tellOffMessage = config.TellOffMessage;
  }
    
  // No-op stub for questionnaire submission handling
  public async Task<QuestionnaireSubmitHandlerResponseJson> Handle(HttpContext ctx)
  {
    var form = await ctx.Request.ReadFormAsync();
    var answers = _parser.Parse(form);
    var score = _scorer.Score(answers);
    var isOverThreshold = _scorer.IsOverThreshold(score);

    if(isOverThreshold){
      return Answer(_tellOffMessage);
    }else{
      return Answer(_welldoneMessage);
    }
  }
  private QuestionnaireSubmitHandlerResponseJson Answer(string message)
    {
        return new QuestionnaireSubmitHandlerResponseJson { message = message };
    }
}
