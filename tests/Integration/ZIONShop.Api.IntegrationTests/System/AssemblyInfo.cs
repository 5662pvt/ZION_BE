// WorkerReadinessIsolationTests builds its own WebApplicationFactory<Program> (own Postgres +
// RabbitMq containers, EventBus:Provider=RabbitMq) and mutates process-wide Environment variables to
// feed Program.cs's configuration sources, the same way ApiFactory (ApiTestCollection) does for
// EventBus:Provider=InMemory. Without every test class sharing one xUnit collection (today,
// ApiTestCollection already covers the rest of this assembly), xUnit's default cross-collection
// parallelization would race the two hosts' environment variable setup against each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
