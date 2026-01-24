namespace HealthTest;

public interface ILandingFormParser
{
    LandingFormModel Parse(Microsoft.AspNetCore.Http.IFormCollection form);
}
