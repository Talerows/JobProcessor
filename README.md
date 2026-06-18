# JobProcessor.Worker

A .NET 10 `BackgroundService` that polls a PostgreSQL database for open jobs, queues them internally, and processes them in parallel — safely across multiple running instances.

---

## Quick Start

### 1. Start container

```bash
docker compose up -d
```

The init script `001_create_jobs_table.sql` runs automatically and seeds 10 open jobs.

This will compile and run an instance of the worker with the name `jobprocessor`

### 2. Connect to the worker container log stream

```bash
docker logs -f jobprocessor
```

### (optional) Run the worker in host

Comment out the service `jobprocessor` in the `docker-compose` file.

To start the database (give it some time to start up) Run

```bash
docker compose up -d
```

Run the worker on the host machine with

```bash
cd src/JobProcessor.Worker
dotnet run
```

## Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "JobProcessor": "Host=localhost;Port=5432;Database=jobprocessor;Username=app_user;Password=secret;"
  },
  "JobProcessor": {
    "PollingIntervalSeconds": 100,
    "MaxQueueSize": 4,
    "MaxParallelJobs": 3,
    "JobTimeoutMinutes": 5,
    "ConnectionStringName": "JobProcessor"
  }
}
```


## Job Lifecycle

```
Open ──(claim, atomic)──▶ InProgress ──(success)──▶ Completed
                                     ╰──(timeout)──▶ Timeout
```

All status transitions are persisted to PostgreSQL with UTC timestamps.

---


## Cleanup

Delete containers and associated volumes

```bash
docker compose down -v
```