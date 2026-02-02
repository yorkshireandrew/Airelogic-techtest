---
agent: a-developer
description: Executes api development tasks with high coding standards.
---

# Async
- Prefer async all the way; accept CancellationToken in public APIs. Avoid introducing async into business logic.
- All Async methods should be suffixed with Async. Mention with "WARNING: [DESCRIPTION OF ISSUE]" any tasks that are not awaited or async methods that are not suffixed with Async in your final response.
- Use the a-developer agent to perform development tasks specified in the input.

# Performance
- Use `Task.WhenAll()` or Task Parallel Library (TPL) for parallel execution of multiple tasks
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task

# Testing
- Tests should be in folders and projects seperated from the main API project.
- Tests should follow development guidelines, best practices and SOLID principles, but should prioritise simplicity and readability.
- Where necessary introduce interfaces and use mocking frameworks like Moq to isolate units under test.
- Where necessary introduce adapter methods or classes to seperate interfacing to API classes from setup conditions and assertion logic, to allow API classes to change without requiring large changes to multiple tests.
- Look at how the API classes are composed and ensure communication and interaction between classes look correct in the tests.

# File Structure
Use this structure as a guide when creating or updating files, Commands and Queries are CQRS specific folders. Extensions includes classes defining extension methods for classes in that module or built-in types. Persistence for repositories, database contexts and migrations.

```text
src/
  Api/
    Controllers/
    Models/
  Application/
    Commands/
    Queries/
    BusinessLogic/
    Interfaces/
    Extensions/
  Domain/
    Entities/
    Extensions/
    Enums/
    ValueObjects/
    Exceptions/
  Infrastructure/
    Persistence/
    Services/
tests/
  Unit/
  Integration/
```
