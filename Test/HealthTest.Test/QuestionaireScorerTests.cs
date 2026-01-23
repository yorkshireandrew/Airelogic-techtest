using Xunit;
using System.Collections.Generic;

namespace HealthTest.Test;

    public class QuestionaireScorerTests
    {

      private AppSettings TestAppSettings(bool isScoreOnNo=false)
      {
        return new AppSettings{
              QuestionScoreSettings = new List<QuestionScoreSetting>
              {
                  new QuestionScoreSetting
                  {
                      IsScoreOnNo = isScoreOnNo,
                      AgeGroupScores = new List<int> { 1, 10 }
                  },
                  new QuestionScoreSetting
                  {
                      IsScoreOnNo = isScoreOnNo,
                      AgeGroupScores = new List<int> { 1, 11 }
                  }
              }
          };
      }

      [Fact]
      public void Score_TrueAnswerAddsToScore_SecondAgeGroup()
      {
          var scorer = new QuestionnaireScorer(TestAppSettings());

          var model = new QuestionnaireFormModel
          {
              AgeGroup = 1, // second age group
              Answers = new List<bool> { true , true}
          };

          var score = scorer.Score(model);
          Assert.Equal(21, score);
      }

      [Fact]
      public void Score_TrueAnswerAddsToScore_FirstAgeGroup()
      {
          var scorer = new QuestionnaireScorer(TestAppSettings());

          var model = new QuestionnaireFormModel
          {
              AgeGroup = 0, // first age group
              Answers = new List<bool> { true , true}
          };

          var score = scorer.Score(model);
          Assert.Equal(2, score);
      }

      [Fact]
      public void Score_FalseAnswer_DoesNotAddToScore()
      {
          var scorer = new QuestionnaireScorer(TestAppSettings());

          var model = new QuestionnaireFormModel
          {
              AgeGroup = 1, // second age group
              Answers = new List<bool> { false , false}
          };

          var score = scorer.Score(model);
          Assert.Equal(0, score);
      }

      [Fact]
      public void Score_IsScoreOnNo_True_TrueAnswerDoesNotAddToScore()
      {
          var scorer = new QuestionnaireScorer(TestAppSettings(isScoreOnNo: true));

          var model = new QuestionnaireFormModel
          {
              AgeGroup = 1, // second age group
              Answers = new List<bool> { true }
          };

          var score = scorer.Score(model);
          Assert.Equal(0, score);
      }

      [Fact]
      public void Score_IsScoreOnNo_True_FalseAnswerAddsToScore()
      {
          var scorer = new QuestionnaireScorer(TestAppSettings(isScoreOnNo: true));

          var model = new QuestionnaireFormModel
          {
              AgeGroup = 1, // second age group
              Answers = new List<bool> { false , false}
          };

          var score = scorer.Score(model);
          Assert.Equal(21, score);
      }
  }

