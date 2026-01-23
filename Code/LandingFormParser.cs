using Microsoft.AspNetCore.Http;

namespace HealthTest;

public class LandingFormParser
{
    public virtual LandingFormModel Parse(IFormCollection form)
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
}
