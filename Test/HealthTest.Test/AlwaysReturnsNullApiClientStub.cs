using System.Threading.Tasks;
using HealthTest;

namespace HealthTest.Test
{
    public class AlwaysReturnsNullApiClientStub : IApiClient
    {
        public Task<IPatientModel?> GetPatientFromNhsNumberAsync(string lookupValue)
        {
            return Task.FromResult<IPatientModel?>(null);
        }
    }
}
