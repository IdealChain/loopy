Loopy: distributed KV data store
================================

Prototype implementation

Features
--------

- based on the node-wide, dot-based clocks (NDC) framework by Gonçalves et al. [Gon+17]
  - replication of writes to all nodes
  - periodic background anti-entropy synchronization
  - eventual consistency for concurrent writes
- configurable consistency level per session
  - eventual consistency (all replicas converge to a common state)
  - FIFO/PRAM consistency (writes issued by a specific client are always observed in their order)
  - prioritized FIFO consistency (lost writes do not delay visiblity of higher-priority writes)

Requirements
------------

- C#/.NET 8
- protobuf-net: message serialization (Protocol Buffers compatible)
- NetMQ: lightweight messaging middleware (ZeroMQ compatible)

### [Gon+17]
Ricardo Jorge Tomé Gonçalves et al. “DottedDB: Anti-Entropy without
Merkle Trees, Deletes without Tombstones”. In: 2017 IEEE 36th Symposium
on Reliable Distributed Systems (SRDS). 2017, pp. 194–203. DOI: [10.1109/
SRDS.2017.28](https://doi.org/10.1109/SRDS.2017.28).
