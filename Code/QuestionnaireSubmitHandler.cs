using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HealthTest;

public class QuestionnaireSubmitHandler
{
    // No-op stub for questionnaire submission handling
    public Task<IResult> Handle(HttpContext ctx)
    {
        // Currently a noop; return HTTP 200 OK
        return Task.FromResult(Results.Ok());
    }
}
