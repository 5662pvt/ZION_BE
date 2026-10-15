// WorkerHostTests and WorkerDevelopmentHostTests both mutate process-wide Environment variables
// (ConnectionStrings__DefaultConnection, EventBus__Provider, Payments__Fake__WebhookSecret, ...) to
// feed Program.cs's own configuration sources -- xUnit's default cross-class parallelization would
// race the two test classes' setup/teardown against each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
