# Introduction

- Implements the lifestyle checker as described at: https://github.com/airelogic/tech-test-portal/tree/main/T2-Lifestyle-Checker

## Development deployment steps:
- Configure host and port in appsettings.json
- For VSCode development copy files in VsCodeConfig into .vscode folder in project root. 
- To debug using HTTP ensure the environment variable "ASPNETCORE_ENVIRONMENT": "Development"

```powershell
dotnet test Test/HealthTest.Test/HealthTest.Test.csproj
```
or in VS Code 
Run Task: Terminal → Run Task → test

```powershell
dotnet run
```

## Production deployment steps
IMPORTANT: The questionnaire handles personal sensitive data. It should only be used behind HTTPS 
1) Install prerequisites: IIS (Web Server role) + .NET 8 Hosting Bundle on the Windows server.
2) Publish the app: dotnet publish -c Release -o ./publish.
3) Ensure web.config/ANCM present (publish normally adds it); choose InProcess hosting (recommended) or OutOfProcess (IIS as reverse proxy to Kestrel).
4) Create an IIS Site pointing to the publish folder, set App Pool to No Managed Code and proper identity/permissions.

For in-process hosting (best performance), you can ensure the csproj includes (optional — publish usually configures this automatically):
```
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>
</PropertyGroup>
```

## Configuration
- The configuration is validated. Most configuration errors will be logged and prevent the application starting.
- Feature flag LogPersonallyIdentifiableData allows the app to log personally identifiable info if the level is debug, such as NHS number. Only enable it if absolutely necessary.
- Feature flag ValidateNhsCheckDigit allows the app to check the check digit of the NHS number. This was not asked for in the requirements, but real users will submit with the check digit, checking it shields the API from DDOS attacks or bad actors trying to mine NHS Numbers. The flag allows it to be disabled if issues arise.
- Feature flag InformUserWhenNhsNumberFormatIncorrect informs the user if the NHS Number format is incorrect, This was not in the requirements, so is set false, setting true may aid users, but also allows valid NHS number mining.
- AgeBands. The age ranges are considered inclusive, they include the start and end ages, this is standard practice in law etc.
- AgeBands. If the users age is not covered by AgeBands are treated as not eligable. The service will warn at startup if multiple gaps are found, but still run.
- QuestionScoreSettings. If IsScoreOnNo is true the score is added if the user answers NO. AgeGroupScores provides the score for each age group.
- TellOffIfScoreExceeds. If the score EXCEEDS this value the user is given the TellOffMessage instead of the WelldoneMessage.

## Project layout and design
- PageGenerators folder - Contains the page generation code. These read and cache the templates to reduce IO overhead under peak load.
- Templates folder - Contains the HTML templates. These contain a small amount of clientside javascript.
- Parsers folder - Contains the code to turn IFormCollection into domain objects.
- Program.cs configures DI and provides endpoint mapping
- LandingPageSubmitHandler handles the form POST calls the API when the user enters their information.
- QuestionareSubmitHandler handles the form POST and scores the answers to the questionaire and sends the appropriate answer.
- POST requests redirect to a GET. This feature allows the browser back button to work without raising resubmitting data warnings.
- The app does not use cookies, it passes messages and age group in the url query parameters (secured by HTTPS). This avoids the user having to answer a GDPR cookie banner.
- Page JS clears down all its form inputs when pages load, this is deliberate so other if another user uses the browser they should not see previous input values cached by the browser.

## Assumptions made
- In the requirements (https://github.com/airelogic/tech-test-portal/tree/main/T2-Lifestyle-Checker) there are overlapping bands for ages 64,65. In appsettings.json I moved the last band to start at 66 to avoid overlap.
- The NB note on the end of requirements is unclear, as on your birthday you are one year older than the day before. I questioned this, I take the response to mean: The DOB returned by the API CAN be used to calculate the Age of someone using standard formula, but it might not be their actual DOB (to avoid the API sharing personally identifiable information), so their DOB should used immediately only for age calculation and not stored.

## Suggested Improvements
- It is not super pretty. Pretty was not in the requirements, so I kept things small and easy to follow. The templates could be improved to make the pages include images etc for beauty.
- We could add a mapping that serves static content like JS or CSS making it easier to improve the look.
- Simple C# replace alters the templates before rendering. Using something like RazorLight might improve performance, but I though keep-it-simple.


