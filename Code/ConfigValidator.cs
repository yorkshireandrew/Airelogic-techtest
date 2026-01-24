namespace HealthTest;

public class ConfigValidator
{
  private readonly ILogger<ConfigValidator>? _logger;
  private readonly AppSettings _config;

  public ConfigValidator(ILogger<ConfigValidator> logger, AppSettings config)
  {
    _logger = logger;
    _config = config;
  }

  public void ValidateSettings(){
    ValidateApiSettings();
    ValidateAgeBands();
    ValidateQuestions();
    ValidateQuestionScoreSettings();
    ValidateAgeBandsNonOverlapping();
    ValidateAgeBandsContinuity();
    ValidateLogPersonallyIdentifiableData();
    ValidateTellOffIfScoreExceeds();
  }

  public void ValidateAgeBands()
  {
    if (_config.AgeBands == null || _config.AgeBands.Count == 0)
    {
      var message = "Configuration invalid: AgeBands must contain at least one age band.";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }
  }

  public void ValidateQuestions()
  {
    if (_config.Questions == null || _config.Questions.Count == 0)
    {
      var message = "Configuration invalid: Questions must contain at least one question.";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }

    for (var i = 0; i < _config.Questions.Count; i++)
    {
      if (string.IsNullOrWhiteSpace(_config.Questions[i]))
      {
        var message = $"Configuration invalid: Question at index {i} is empty or whitespace.";
        _logger?.LogCritical(message);
        throw new InvalidOperationException(message);
      }
    }
  }

  public void ValidateQuestionScoreSettings()
  {
    if (_config.QuestionScoreSettings == null)
    {
      var message = "Configuration invalid: QuestionScoreSettings is null.";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }

    var questionsCount = _config.Questions?.Count ?? 0;
    var scoreSettingsCount = _config.QuestionScoreSettings.Count;

    if (questionsCount != scoreSettingsCount)
    {
      var message = $"Configuration invalid: Questions count ({questionsCount}) does not match QuestionScoreSettings count ({scoreSettingsCount}).";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }

    // Ensure each QuestionScoreSetting has AgeGroupScores matching the number of AgeBands
    var ageBandsCount = _config.AgeBands?.Count ?? 0;
    for (var i = 0; i < _config.QuestionScoreSettings.Count; i++)
    {
      var qs = _config.QuestionScoreSettings[i];
      if (qs == null)
      {
        var message = $"Configuration invalid: QuestionScoreSetting at index {i} is null.";
        _logger?.LogCritical(message);
        throw new InvalidOperationException(message);
      }

      if (qs.AgeGroupScores == null)
      {
        var message = $"Configuration invalid: AgeGroupScores for question index {i} is null.";
        _logger?.LogCritical(message);
        throw new InvalidOperationException(message);
      }

      if (qs.AgeGroupScores.Count != ageBandsCount)
      {
        var message = $"Configuration invalid: AgeGroupScores count ({qs.AgeGroupScores.Count}) for question index {i} does not match AgeBands count ({ageBandsCount}).";
        _logger?.LogCritical(message);
        throw new InvalidOperationException(message);
      }
    }
  }

  public void ValidateAgeBandsNonOverlapping()
  {
    // Verify each age in range 0..200 is covered by at most one age band
    var bands = _config.AgeBands ?? new List<List<int>>();
    for (var age = 0; age <= 200; age++)
    {
      var matches = 0;
      for (var i = 0; i < bands.Count; i++)
      {
          var band = bands[i];
          if (band == null)
          {
            var message = $"Configuration invalid: AgeBand at index {i} is null.";
            _logger?.LogCritical(message);
            throw new InvalidOperationException(message);
          }

          if (band.Count != 2)
          {
            var message = $"Configuration invalid: AgeBand at index {i} must contain exactly 2 elements (min,max).";
            _logger?.LogCritical(message);
            throw new InvalidOperationException(message);
          }

          var min = band[0];
          var max = band[1];
        if (min <= age && age <= max) matches++;
        if (matches > 1)
        {
          var message = $"Configuration invalid: overlapping AgeBands detected for age {age}.";
          _logger?.LogCritical(message);
          throw new InvalidOperationException(message);
        }
      }
    }
  }

  public void ValidateAgeBandsContinuity()
  {
    var bands = _config.AgeBands ?? new List<List<int>>();
    bool seenMatch = false;

    for (var age = 0; age <= 200; age++)
    {
      var matches = 0;
      for (var i = 0; i < bands.Count; i++)
      {
        var band = bands[i];
        if (band == null || band.Count != 2) continue; // malformed bands handled elsewhere
        var min = band[0];
        var max = band[1];
        if (min <= age && age <= max) matches++;
        if (matches > 1) break; // overlapping handled by other validator
      }

      if (matches > 0)
      {
        seenMatch = true;
      }
      else if (seenMatch && matches == 0)
      {
        var message = $"WARNING!!!  Gap detected in AgeBands starting at age {age} after matches began.";
        _logger?.LogWarning(message);
        return; // only warn once
      }
    }
  }

  public void ValidateTellOffIfScoreExceeds()
  {
    if (_config.TellOffIfScoreExceeds <= 0)
    {
      var message = "Configuration invalid: TellOffIfScoreExceeds must be greater than zero.";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }
  }

  public void ValidateApiSettings()
  {
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(_config.ApiEndpoint)) missing.Add(nameof(_config.ApiEndpoint));
    if (string.IsNullOrWhiteSpace(_config.ApiSecret)) missing.Add(nameof(_config.ApiSecret));

    if (missing.Count > 0)
    {
      var message = $"Configuration invalid: missing {string.Join(", ", missing)}";
      _logger?.LogCritical(message);
      throw new InvalidOperationException(message);
    }
  }

  public void ValidateLogPersonallyIdentifiableData()
  {
    if (_config.LogPersonallyIdentifiableData)
    {
      var message = "WARNING!!! LogPersonallyIdentifiableData is enabled — this will log PID. Ensure this is intended.";
      _logger?.LogWarning(message);
    }
  }
}