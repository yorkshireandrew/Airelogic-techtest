using System.Linq;

namespace HealthTest;

public class QuestionnaireScorer
{
   private List<QuestionScoreSetting> _questionScoreSettings;

   private readonly int _tellOffIfScoreExceeds;
    public QuestionnaireScorer(AppSettings config)
    {
        _questionScoreSettings = config?.QuestionScoreSettings ?? new List<QuestionScoreSetting>();
        _tellOffIfScoreExceeds = config?.TellOffIfScoreExceeds ?? 0;
    }

    // Returns the score (number of 'true' answers) from the provided form model
    public int Score(QuestionnaireFormModel model)
    {
      int score = 0;

      for (int i = 0; i < model.Answers.Count; i++)
      {
        var answer = model.Answers[i];
        var questionSettings = _questionScoreSettings[i];

        var shouldScore = answer;
        if(questionSettings.IsScoreOnNo) shouldScore = !shouldScore;
        if (!shouldScore) continue;

        var mark = questionSettings.AgeGroupScores.ElementAtOrDefault(model.AgeGroup) ;
        score += mark;
      }

      return score;
    }

    public bool IsOverThreshold(int score)
    {
      return score > _tellOffIfScoreExceeds;
    }
}
