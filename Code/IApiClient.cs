namespace HealthTest;

public interface IApiClient
{
    Task<IPatientModel?> GetPatientFromNhsNumberAsync(string lookupValue);
}

