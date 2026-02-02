---
agent: a-developer
description: Executes api development tasks with high coding standards.
---
# Web Development
- Static content lives only in the web host under wwwroot.
- Application and Domain layers never reference or serve static files.
- Unless specified otherwise, assume the web development task involves creating or modifying code related to ASP.NET Core Web Applications using React for the frontend.
- Use the a-developer agent to perform development tasks specified in the input.
- Request routing should be thin delegating to Controllers. Controllers should not contain business logic. Business logic should not contain HTTP or web specific code.
- Use Dependency Injection to inject services into Controllers.
- Use structured logging with correlation IDs (from message headers / HTTP trace IDs).
- Never log sensitive data (PII/secrets) unless instructed otherwise.
- Map errors to host-appropriate outputs (exit codes / HTTP status / dead-letter).

# Async
- Prefer async all the way; accept CancellationToken in public APIs. Avoid introducing async into business logic.
- All Async methods should be suffixed with Async. Mention with "WARNING: [DESCRIPTION OF ISSUE]" any tasks that are not awaited or async methods that are not suffixed with Async in your final response.
- Use the a-developer agent to perform development tasks specified in the input.

# Performance
- Use `Task.WhenAll()` or Task Parallel Library (TPL) for parallel execution of multiple tasks
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task

# Testing
- Tests should be in folders and projects seperated from the main web project.
- Tests should follow development guidelines, best practices and SOLID principles, but should prioritise simplicity and readability.
- Where necessary introduce interfaces and use mocking frameworks like Moq to isolate units under test.
- Where necessary introduce adapter methods or classes to seperate interfacing to web classes from setup conditions and assertion logic, to allow web classes to change without requiring large changes to multiple tests.
- Look at how the web classes are composed and ensure communication and interaction between classes look correct in the tests.

# File Structure
Use this structure as a guide when creating or updating files, Commands and Queries are CQRS specific folders. Extensions includes classes defining extension methods for classes in that module or built-in types. Persistence for repositories, database contexts and migrations.

```text
src/
  WebApp/                  # Host
    Controllers/
    Filters/
    wwwroot/
      css/
      js/
      images/
      lib/
    Middleware/
    Views/                 # Templates such as Razor pages
    Program.cs
  Application/
    Commands/
    Queries/
    Abstractions/
    Extensions/
    DTOs/
    Validation/
  Domain/
    Entities/
    Extensions/
    ValueObjects/
    Enums/
    Exceptions/
  Infrastructure/
    Persistence/
    Messaging/
    Services/
tests/
  Unit/
  Integration/
```