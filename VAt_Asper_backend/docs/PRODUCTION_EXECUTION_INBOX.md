# Manufacturing execution-status inbox

The Manufacturing owner consumes `ProductionExecutionAcceptedV1` and `ProductionCostPostedV1` from the dedicated durable `vat.manufacturing.execution.v1` queue. The financial owner remains authoritative for stock, WIP and cost; Manufacturing stores only delivery evidence and the latest order execution projection.

Each event commits its inbox row, checkpoint and order status in one Manufacturing database transaction before RabbitMQ acknowledgement. Identical redelivery returns `Duplicate`. A lower or equal session version is retained as `Ignored` and cannot move the projection backwards. Malformed metadata/payload, unknown orders, conflicting event IDs and a second session for one order are durably quarantined and acknowledged only after the dead-letter row commits.

The projection changes an approved order to `Running` on execution acceptance or a posted operation, and to `Completed` only on the financial `Close` operation. WIP and received quantities are exact strings copied from the financial event; Manufacturing does not recalculate cost.

Migration: `20261101064158_ProductionExecutionInbox`. Apply it with the Manufacturing migration-owner connection before enabling messaging. Broker-enabled environments must use a TLS RabbitMQ URI, except loopback development may use AMQP. Live broker recovery and deployment validation remain staging gates.
