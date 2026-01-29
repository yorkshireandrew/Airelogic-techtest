namespace HealthTest;

public class LandingSubmitHandler
{
    private readonly ILogger<LandingSubmitHandler>? _logger;
    private readonly IApiClient _apiClient;
    private readonly IAgeBandCalculator _ageBandCalculator;
    private readonly bool _logPersonallyIdentifiableData;
    private readonly bool _informUserWhenNhsNumberFormatIncorrect;
    private readonly string _patientNotFoundMessage;
    private readonly string _notEligibleMessage;
    private readonly ILandingFormParser _landingFormParser;

    private readonly bool _validateNhsCheckDigit;

    public LandingSubmitHandler(IApiClient apiClient, IAgeBandCalculator ageBandCalculator, ILogger<LandingSubmitHandler>? logger, AppSettings config, ILandingFormParser? landingFormParser)
    {
        _apiClient = apiClient;
        _logger = logger;
        _ageBandCalculator = ageBandCalculator;
        _logPersonallyIdentifiableData = config.LogPersonallyIdentifiableData;
        _patientNotFoundMessage = config.PatientNotFoundMessage;
        _notEligibleMessage = config.NotEligibleMessage;
        _informUserWhenNhsNumberFormatIncorrect = config.InformUserWhenNhsNumberFormatIncorrect;
        _validateNhsCheckDigit = config.ValidateNhsCheckDigit;
        _landingFormParser = landingFormParser ?? new LandingFormParser();
    }

    public async Task<LandingSubmitHandlerResponseJson> Handle(HttpContext ctx)
    {
        var form = await ctx.Request.ReadFormAsync();

        var landing = _landingFormParser.Parse(form);
        if (_logPersonallyIdentifiableData) _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; DOB={landing.day}-{landing.month}-{landing.year}");

        if (!landing.NhsIsValid(landing.nhs, _validateNhsCheckDigit))
        {
            LogInvalidNhsFormat(landing);
            return SendInvalidNhsNumberResponse();
        }

        try{
            return await CreateRedirectToQustionare(landing).ConfigureAwait(false); // core logic
        }
        catch(ApiServerException ex)
        {
            if(ex.Message.ToString().Contains("invalid nhs number", StringComparison.CurrentCultureIgnoreCase))
            {
                return SendInvalidNhsNumberResponse();
            }

            _logger?.LogError($"API server error: {ex.Message}");
            return Answer("An error occurred while processing your request. Please try again later.");
        }
        catch(Exception ex)
        {
            _logger?.LogError(ex, $"Unexpected error: {ex.Message}");
            return Answer("An unexpected error occurred while processing your request. Please try again later.");
        }
    }

    private async Task<LandingSubmitHandlerResponseJson> CreateRedirectToQustionare(LandingFormModel landing)
    {
        var nineDigitNhs = landing.nhs.Length == 10 ? landing.nhs.Substring(0, 9) : landing.nhs;
        var patient = await _apiClient.GetPatientFromNhsNumberAsync(nineDigitNhs).ConfigureAwait(false);
        if (patient == null) return new LandingSubmitHandlerResponseJson { Message = _patientNotFoundMessage };

        if (patient.SurnameMatches(landing.surname) == false || patient.NhsNumberMatches(nineDigitNhs) == false || patient.DateOfBirthMatches(landing.day, landing.month, landing.year) == false)
        {
            return new LandingSubmitHandlerResponseJson { Message = _patientNotFoundMessage };
        }

        var age = patient.CalculateAge(System.DateTime.Today);
        if (_logPersonallyIdentifiableData) _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; Age={age}");
        var ageBand = _ageBandCalculator.CalculateAgeBand(age);

        if (ageBand == -1) return new LandingSubmitHandlerResponseJson { Message = _notEligibleMessage };

        return new LandingSubmitHandlerResponseJson { AgeBand = ageBand };
    }

    private void LogInvalidNhsFormat(LandingFormModel landing)
    {
        if (_logPersonallyIdentifiableData)
        {
            _logger?.LogWarning($"Invalid NHS number format received: {landing.nhs}");
        }
        else
        {
            _logger?.LogWarning("Invalid NHS number format received.");
        }
    }

    private LandingSubmitHandlerResponseJson SendInvalidNhsNumberResponse(){
        if (_informUserWhenNhsNumberFormatIncorrect == true) return Answer("NHS number format is incorrect"); 
        return Answer(_patientNotFoundMessage);
    }

    private LandingSubmitHandlerResponseJson Answer(string message)
    {
        return new LandingSubmitHandlerResponseJson { Message = message };
    }
}

