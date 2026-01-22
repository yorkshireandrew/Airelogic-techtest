using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HealthTest
{
    public class LandingSubmitHandler
    {
        public LandingSubmitHandler()
        {
        }

        public async Task<IResult> Handle(HttpContext ctx)
        {
            var form = await ctx.Request.ReadFormAsync();
            var landing = new LandingFormRaw
            {
                nhs = form["nhs"].ToString(),
                surname = form["surname"].ToString(),
                day = form["dob_day"].ToString(),
                month = form["dob_month"].ToString(),
                year = form["dob_year"].ToString()
            };

            if (!landing.NhsIsValidFormat(landing.nhs))
            {
                return Results.BadRequest("Invalid NHS number format. It should be a 10-digit number.");
            }

            var response = $"Received: NHS={landing.nhs}; Surname={landing.surname}; DOB={landing.day}-{landing.month}-{landing.year}";
            return Results.Text(response, "text/plain");
        }
    }
}
