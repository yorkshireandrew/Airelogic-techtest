---
agent: a-developer
description: Executes development tasks with high coding standards.
---
# Task Execution Prompt
- All Async methods should be suffixed with Async. Mention with "WARNING: [DESCRIPTION OF ISSUE]" any tasks that are not awaited or async methods that are not suffixed with Async in your final response.
- Use the a-developer agent to perform development tasks as specified in the input.

## 
## Performance
- Use `Task.WhenAll()` for parallel execution of multiple tasks
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task
