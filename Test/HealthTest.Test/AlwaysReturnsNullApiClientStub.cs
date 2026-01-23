using System.Threading.Tasks;

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
