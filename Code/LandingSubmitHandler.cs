using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace HealthTest
{
    public class LandingSubmitHandler
    {
        private readonly ILogger<LandingSubmitHandler>? _logger;
        private readonly IApiClient _apiClient;
        private readonly bool _logPersonallyIdentifiableData;
        private readonly string _patientNotFoundMessage;
        public LandingSubmitHandler(IApiClient apiClient, ILogger<LandingSubmitHandler>? logger = null, AppSettings? config = null)
        {
            _apiClient = apiClient;
            _logger = logger;
            _logPersonallyIdentifiableData = config?.LogPersonallyIdentifiableData ?? false;
            _patientNotFoundMessage = config?.PatientNotFoundMessage ?? "Your details could not be found";
        }

        public async Task<IResult> Handle(HttpContext ctx)
        {
            var form = await ctx.Request.ReadFormAsync();
            
            var landing = CreateLandingFormModelFromForm(form);

            if (!landing.NhsIsValidFormat(landing.nhs))
            {
                LogInvalidNhsFormat(landing);
                return Answer("Invalid NHS number format. It should be a 10-digit number.");
            }

            if (_logPersonallyIdentifiableData)
            {
                _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; DOB={landing.day}-{landing.month}-{landing.year}");
            }

            // Call the API client with the provided NHS number
            try{
                var patient = await _apiClient.GetPatientFromNhsNumberAsync(landing.nhs).ConfigureAwait(false);
                if (patient == null)  return Answer(_patientNotFoundMessage);
                return Results.Ok(patient);
            }
            catch(ApiServerException ex)
            {
                if(ex.Message.ToString().Contains("invalid nhs number", StringComparison.CurrentCultureIgnoreCase))
                {
                    return Answer(_patientNotFoundMessage);
                }
                
                _logger?.LogError($"API server error: {ex.Message}");
                return Answer("An error occurred while processing your request. Please try again later.");
            }
        }

        protected virtual void LogInvalidNhsFormat(LandingFormModel landing)
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

        protected virtual LandingFormModel CreateLandingFormModelFromForm(IFormCollection form)
        {
            return new LandingFormModel
            {
                nhs = form["nhs"].ToString().Trim(),
                surname = form["surname"].ToString().Trim(),
                day = form["dob_day"].ToString().Trim(),
                month = form["dob_month"].ToString().Trim(),
                year = form["dob_year"].ToString().Trim()
            };
        }

        protected Microsoft.AspNetCore.Http.IResult Answer(string message)
        {
            var encodedMessage = Uri.EscapeDataString(message);
            return Results.Redirect($"/Answer?message={encodedMessage}");
        }


    }
}
