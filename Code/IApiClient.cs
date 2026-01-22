using System.Threading.Tasks;

namespace HealthTest
{
    public interface IApiClient
    {
        Task<PatientModel?> GetPatientFromNhsNumberAsync(string lookupValue);
    }
}
