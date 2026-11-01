# Spec Delta

## Purpose

Defines how the TaskFlow API reports failures: one response shape for every error, a fixed mapping from failure kind to HTTP status, and a guarantee that internal details never reach the client. All of it is handled in one place rather than per endpoint.

## ADDED Requirements

### Requirement: Uniform error body
Every error response produced by application code (status 400 and above) SHALL use the RFC 9457 problem-details JSON shape with `status`, `title`, `detail` and `traceId`. Bodies of the form `{ "error": ... }` or `{ "message": ... }` SHALL NOT be returned.

#### Scenario: Business rule failure
- **WHEN** a client asks to delete the last active Administrator
- **THEN** the response is 400 with a problem-details body whose `detail` carries the human-readable reason, and the body has no `error` or `message` property

#### Scenario: Authentication pipeline responses unchanged
- **WHEN** a request is rejected by the authentication or authorization policy pipeline before reaching an endpoint (401, or a policy 403)
- **THEN** the response is the same as before this change

#### Scenario: Trace id present
- **WHEN** any error response is returned
- **THEN** its body includes a `traceId` that matches the request's trace identifier in the server log

### Requirement: Validation failures list field errors
When a request fails input validation, the API SHALL respond 400 with a validation problem-details body whose `errors` object maps each invalid field to its messages. The handler SHALL NOT run.

#### Scenario: Invalid task creation
- **WHEN** a manager submits a new task with an empty title
- **THEN** the response is 400, `errors` contains an entry for the title field, and no task is created

#### Scenario: Validation runs without per-endpoint code
- **WHEN** a new endpoint is added whose request type has a validator
- **THEN** that validator runs automatically for the endpoint, with no validation call written in the endpoint

### Requirement: Status mapping for known failures
The API SHALL map each known failure kind to a fixed status: invalid request or broken business rule → 400; caller lacks permission for this resource → 403; resource not found → 404; conflicting or stale change → 409; rate limit exceeded → 429. A failure kind's existing `title` (for example "Too many requests") SHALL be kept.

#### Scenario: Missing resource
- **WHEN** a client requests analytics for a project id that does not exist
- **THEN** the response is 404 with a problem-details body

#### Scenario: Forbidden resource
- **WHEN** an employee requests a task they neither created nor are assigned to
- **THEN** the response is 403

#### Scenario: Hidden task stays hidden
- **WHEN** a user requests a task that is missing or deleted
- **THEN** the response is 403, as today, so the API does not reveal whether the task exists

#### Scenario: Stale update
- **WHEN** two users save the same task and the second save is based on an outdated version
- **THEN** the second response is 409

#### Scenario: Assistant rate limit
- **WHEN** a user exceeds the assistant's message rate limit
- **THEN** the response is 429 with title "Too many requests"

### Requirement: Unexpected failures do not leak internals
Any failure that is not a known failure kind SHALL produce a 500 response with a generic `detail` that contains no exception message, type name or stack trace. The full exception SHALL be written to the server log with the same `traceId`.

#### Scenario: Framework exception
- **WHEN** an unexpected internal error occurs while handling a request, such as a query that finds no rows where one was assumed
- **THEN** the client receives 500 with a generic message, not the internal exception text, and the server log has the full exception under the response's `traceId`

### Requirement: Request logging
The API SHALL log one entry per request with method, path, response status, duration and, when signed in, the user id, from a single place in the request pipeline.

#### Scenario: Request is logged
- **WHEN** a signed-in user calls any endpoint
- **THEN** the log has one entry for that request with method, path, status, elapsed milliseconds and user id, and no request or response body content
