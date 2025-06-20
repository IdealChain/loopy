Loopy: distributed KV data store
================================

Prototype implementation for my diploma thesis:

Daniel Achleitner. *An Efficient Data Store for a Dependable Distributed Control Unit*. Diploma Thesis, Technische Universität Wien, 2024. DOI: [10.34726/hss.2024.115921](https://doi.org/10.34726/hss.2024.115921).

Features
--------

- based on the node-wide, dot-based clocks (NDC) framework by Gonçalves et al. [^1]
  - replication of writes to all nodes
  - periodic background anti-entropy synchronization
  - eventual consistency for concurrent writes
- configurable consistency level per client session
  - eventual consistency (all replicas eventually converge to a common state)
  - FIFO/PRAM consistency (writes executed by the same client are observed in the order they were issued in)
  - priority-aware FIFO consistency (high-priority writes are not delayed by lost writes of lower priority, avoiding priority inversion)
- evaluated using the Maelstrom/Jepsen simulation workbench/framework [^2]
  - FIFO/PRAM consistency evaluated using the Read-Centric Algorithm (VPC-MU) by Wei et al. [^3] [^4]
  - see also [maelstrom](https://github.com/IdealChain/maelstrom) and [pram-consistency-checker](https://github.com/IdealChain/pram-consistency-checker)

Project Structure
-----------------

- Loopy.Core: main NDC data store functions and interfaces
- Loopy.Comm: network messages, sockets and serialization functions
- Loopy.Node: standalone node process using Protobuf/ZeroMQ sockets
- Loopy.ClientShell: simple CLI shell to execute get/put/del commands on a node
- Loopy.MaelstromNode: node process for Maelstrom using its JSON RPC protocol over STDIN/STDOUT
- Loopy.MaelstromTest: quick automation to run parameterized series of Maelstrom tests

Requirements
------------

- C#/.NET 8
- protobuf-net: message serialization (Protocol Buffers compatible)
- NetMQ: lightweight messaging middleware (ZeroMQ compatible)
- Spectre.Console, NLog, NUnit


References
----------

[^1]: Ricardo Jorge Tomé Gonçalves et al. “DottedDB: Anti-Entropy without Merkle Trees, Deletes without Tombstones”. In: 2017 IEEE 36th Symposium on Reliable Distributed Systems (SRDS). 2017, pp. 194–203. DOI: [10.1109/SRDS.2017.28](https://doi.org/10.1109/SRDS.2017.28).

[^2]: Kyle Kingsbury. *Maelstrom: A workbench for writing toy implementations of distributed systems*. https://github.com/jepsen-io/maelstrom.

[^3]: Hengfeng Wei et al. “Verifying Pipelined-RAM Consistency over Read/Write Traces of Data Replicas”. In: IEEE Transactions on Parallel and Distributed Systems 27.5 (2016), pp. 1511–1523. DOI: [10.1109/TPDS.2015.2453985](https://doi.org/10.1109/TPDS.2015.2453985).

[^4]: Hengfeng Wei. *PRAM consistency checking in the context of distributed shared memory systems*. https://github.com/hengxin/ConsistencyChecking.