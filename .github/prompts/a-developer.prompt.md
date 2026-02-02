---
agent: a-developer
description: Executes general console/dll development tasks with high coding standards.
---
# Async
- All Async methods should be suffixed with Async. Mention with "WARNING: [DESCRIPTION OF ISSUE]" any tasks that are not awaited or async methods that are not suffixed with Async in your final response.
- Use the a-developer agent to perform development tasks specified in the input.

# Performance
- Use `Task.WhenAll()` or Task Parallel Library (TPL) for parallel execution of multiple tasks
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task

## Testing
- Tests should be in folders and projects seperated from the main application project.
- Tests should follow development guidelines, best practices and SOLID principles, but should prioritise simplicity and readability.
- Where necessary introduce interfaces and use mocking frameworks like Moq to isolate units under test.
- Where necessary introduce adapter methods or classes to seperate interfacing to API classes from setup conditions and assertion logic, to allow application classes to change without requiring large changes to multiple tests.
- Look at how the application classes are composed and ensure communication and interaction between classes look correct in the tests.

## File Structure
Use this structure as a guide when creating or updating files, Commands and Queries are CQRS specific folders. Extensions are for classes defining extension methods. Persistence for repositories, database contexts and migrations.
```text
src/
  Application/        # console or application entry point: args, DI, config, logging, exit codes
  BusinessLogic/      # use-cases (commands and queries), services, DTOs, validation, interfaces
  Domain/             # entities/value objects/enums, pure rules
  Infrastructure/     # DB/files/http, implementations of interfaces, repositories, HTTPClients, etc
tests/
  Unit/
  Integration/
```