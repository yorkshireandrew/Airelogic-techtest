using System.Threading.Tasks;
using HealthTest;

namespace HealthTest.Test
{
    public class StubApiClient : IApiClient
    {
        public Task<PatientModel?> GetPatientFromNhsNumberAsync(string lookupValue)
        {
            return Task.FromResult<PatientModel?>(null);
        }
    }
}
