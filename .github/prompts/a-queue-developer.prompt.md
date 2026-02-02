---
agent: a-developer
description: Executes queue processing development tasks with high coding standards.
---
# Message processing
- Unless specified otherwise, assume the development task involves creating or modifying code related to processing messages from a queue (e.g., Azure Service Bus, RabbitMQ, AWS SQS).
- If the type of queue or messaging system is not specified ask what to use, otherwise start with a generic approach that can be adapted to common systems.
- Use the a-developer agent to perform development tasks specified in the input.
- If no dispatching or routing mechanism is already specified for handling different message types, ask if we should use MediatR as the dispatcher. If the answer is yes, raw events should be passed to MediatR which passes them too a handler that deserializes them into the appropriate class object, those objects should be then sent back to MediatR to be routed to the appropriate Command handler.

# Async
- Prefer async all the way; accept CancellationToken in public APIs. Avoid introducing async into business logic.
- All Async methods should be suffixed with Async. Mention with "WARNING: [DESCRIPTION OF ISSUE]" any tasks that are not awaited or async methods that are not suffixed with Async in your final response.
- Use the a-developer agent to perform development tasks specified in the input.

# Transactionality
- When processing a queue message involves multiple operations (e.g., database updates, external service calls), ensure that these operations are wrapped in a transaction where appropriate to maintain data integrity. If you are unsure whether transactionality is required ask for clarification before proceeding. If transactionality is required the service could crash at any point during processing. 
- You should never send an ACK or quietly acknowledge a message read from the queue until all operations are successfully completed, or unless instructed to do so.

# Performance
- Ensure concurrent processing of queue messages is handled only by classes or services designed for concurrency to avoid race conditions and data corruption. In some cases a dedicated background service (reactor pattern) me necessary.
- Use `Task.WhenAll()` or Task Parallel Library (TPL) for parallel execution of multiple tasks.
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task

# Exceptions and Retries
- Exceptions should be split into two categories: transient (or system) exceptions and business/domain exceptions. Transient exceptions are temporary and may succeed if retried, while business/domain exceptions indicate a problem with the message, state of business entities or processing logic and should not be retried automatically.

# Testing
- Tests should be in folders and projects seperated from the main project.
- Tests should follow development guidelines, best practices and SOLID principles, but should prioritise simplicity and readability.
- Where necessary introduce interfaces and use mocking frameworks like Moq to isolate units under test.
- Where necessary introduce adapter methods or classes to seperate interfacing to main project classes from setup conditions and assertion logic, to allow main project classes to change without requiring large changes to multiple tests.
- Look at how the main project classes are composed and ensure communication and interaction between classes look correct in the tests.

# File Structure
Use this structure as a guide when creating or updating files, Commands and Queries are CQRS specific folders. Extensions are for classes defining extension methods. Persistence for repositories, database contexts and migrations.

```text
src/
  Worker/                      # queue listener + composition root
    Subscribers/               # message handlers/adapters (thin)
    Dispatching/               # routing to Commands/Queries
    Program.cs
  Messaging.Contracts/         # message DTOs, event names, schemas (optional)
    Models/
  Application/
    Commands/
    Abstractions/              # ports: IQueueClient, IUnitOfWork, IRepository, etc.
    Validation/
  Domain/
    Entities/
    Extensions/
    ValueObjects/
    Enums/
    Exceptions/
  Infrastructure/
    Messaging/                 # queue client impl, serialization, retries, DLQ policy
    Persistence/
    Services/                  # implementations of interfaces, repositories, HTTPClients, etc
tests/
  Unit/
  Integration/
```