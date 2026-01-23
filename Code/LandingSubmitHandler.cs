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
    private readonly LandingFormParser _landingFormParser;

    public LandingSubmitHandler(IApiClient apiClient, IAgeBandCalculator ageBandCalculator, ILogger<LandingSubmitHandler>? logger = null, AppSettings? config = null, LandingFormParser? landingFormParser = null)
    {
        _apiClient = apiClient;
        _logger = logger;
        _ageBandCalculator = ageBandCalculator;
        _logPersonallyIdentifiableData = config?.LogPersonallyIdentifiableData ?? false;
        _patientNotFoundMessage = config?.PatientNotFoundMessage ?? "Your details could not be found";
        _notEligibleMessage = config?.NotEligibleMessage ?? "You are not eligible for this service";
        _informUserWhenNhsNumberFormatIncorrect = config?.InformUserWhenNhsNumberFormatIncorrect ?? false;
        _landingFormParser = landingFormParser ?? new LandingFormParser();
    }

    public async Task<IResult> Handle(HttpContext ctx)
    {
        var form = await ctx.Request.ReadFormAsync();

        var landing = _landingFormParser.Parse(form);
        if (_logPersonallyIdentifiableData) _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; DOB={landing.day}-{landing.month}-{landing.year}");

        if (!landing.NhsIsValid(landing.nhs))
        {
            LogInvalidNhsFormat(landing);
            return SendInvalidNhsNumberResponse();
        }

        // Call the API client with the provided NHS number
        try{
            return await CreateRedirectToQustionare(landing).ConfigureAwait(false);
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
    }

    private async Task<IResult> CreateRedirectToQustionare(LandingFormModel landing)
    {
        var nineDigitNhs = landing.nhs.Length == 10 ? landing.nhs.Substring(0, 9) : landing.nhs;
        var patient = await _apiClient.GetPatientFromNhsNumberAsync(nineDigitNhs).ConfigureAwait(false);
        if (patient == null) return Answer(_patientNotFoundMessage);

        if (patient.SurnameMatches(landing.surname) == false || patient.NhsNumberMatches(nineDigitNhs) == false || patient.DateOfBirthMatches(landing.day, landing.month, landing.year) == false)
        {
            return Answer(_patientNotFoundMessage);
        }

        var age = patient.CalculateAge(System.DateTime.Today);
        if (_logPersonallyIdentifiableData) _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; Age={age}");
        var ageBand = _ageBandCalculator.CalculateAgeBand(age);

        if (ageBand == -1) return Answer(_notEligibleMessage);

        return Results.Redirect($"/Questionnaire?ab={ageBand}");
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

    private IResult SendInvalidNhsNumberResponse(){
        if (_informUserWhenNhsNumberFormatIncorrect == true) return Answer("NHS number format is incorrect"); 
        return Answer(_patientNotFoundMessage);
    }

    private IResult Answer(string message)
    {
        var encodedMessage = Uri.EscapeDataString(message);
        return Results.Redirect($"/Answer?message={encodedMessage}");
    }
}

