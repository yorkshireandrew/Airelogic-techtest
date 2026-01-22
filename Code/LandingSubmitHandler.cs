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
        public LandingSubmitHandler(IApiClient apiClient, ILogger<LandingSubmitHandler>? logger = null, AppSettings? config = null)
        {
            _apiClient = apiClient;
            _logger = logger;
            _logPersonallyIdentifiableData = config?.LogPersonallyIdentifiableData ?? false;
        }

        public async Task<IResult> Handle(HttpContext ctx)
        {
            var form = await ctx.Request.ReadFormAsync();
            
            var landing = CreateLandingFormModelFromForm(form);

            if (!landing.NhsIsValidFormat(landing.nhs))
            {
                LogInvalidNhsFormat(landing);
                return Results.BadRequest("Invalid NHS number format. It should be a 10-digit number.");
            }

            if (_logPersonallyIdentifiableData)
            {
                _logger?.LogDebug($"Received: NHS={landing.nhs}; Surname={landing.surname}; DOB={landing.day}-{landing.month}-{landing.year}");
            }

            // Call the API client with the provided NHS number
            var patient = await _apiClient.GetPatientFromNhsNumberAsync(landing.nhs.Trim()).ConfigureAwait(false);

            if (patient == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(patient);
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
                nhs = form["nhs"].ToString(),
                surname = form["surname"].ToString(),
                day = form["dob_day"].ToString(),
                month = form["dob_month"].ToString(),
                year = form["dob_year"].ToString()
            };
        }
    }
}
