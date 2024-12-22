# Outbox Worker - (MongoDB + Azure Service Bus)
[![.NET](https://github.com/msnts/outbox-worker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/msnts/outbox-worker/actions/workflows/dotnet.yml)
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

This repository contains a project whose main objective is the study and evaluation of .NET resources, as well as the application of techniques for implementing high-performance solutions.
With this purpose in mind, a message retransmission service is being developed using the transactional outbox pattern.
Although the focus of this repository is on study, the Worker project is designed to be fully functional and productive. Therefore, everyone is encouraged to use and adapt what they find useful.

## Project Assumptions

* Process with concurrency control.
* Specific to MongoDB.
* Specific to Azure Service Bus.
* Does not guarantee the ordering of published messages.
* High throughput
* Low memory consumption

## Topics / Resources Used

* .NET
  - Concurrency and Asynchrony
  - Parallel Programming
  - Span<T> and Memory<T>
  - Options Validation
* Observability with OpenTelemetry
  - Traces
  - Metrics
* Algorithms
  - Round Robin
* DataBase
  - MongoDB Compression

## Todo

- [ ] Distributed Lock
- [ ] Exception handling
- [ ] Message serialization
- [x] ~~ArrayPool~~
- [ ] Telemetry
- [ ] Circuit Breaker
- [ ] EventSource

## License
[MIT](https://choosealicense.com/licenses/mit/)
