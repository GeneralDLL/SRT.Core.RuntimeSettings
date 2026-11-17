# SRT.Core.RuntimeSettings

Library that lets a service register runtime settings, take the initial value from configuration, and keep a panel override in that service's main database. After a restart the stored override is still the value that configuration, options, and `IRuntimeSettings` return.

The source is added only when the service calls `SRT_AddRuntimeSettings`. The plugin registers the database, the controllers, and the reload loop when that call happened.

Package documentation (nine files) lives in `src/SRT.Core.RuntimeSettings/docs/`. Start with `SRT.Core.RuntimeSettings.README.md`.

Public package. Examples in the docs use a generic `Orders` service. Do not put passwords, connection strings, or private host names in this repository.
