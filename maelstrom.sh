#!/bin/bash -eu
BIN="$PWD/Loopy.MaelstromNode/bin/Release/net8.0/Loopy.MaelstromNode"
ARGS="Fifo 1"
PARAMS="-w fifo-kv --node-count 4 --concurrency 16 --rate 5 --latency 0 --time-limit 30"
LOG="--log-net-send --log-net-recv"

dotnet build Loopy.MaelstromNode -c Release
(cd ../maelstrom && lein run test --bin $BIN $ARGS $PARAMS $LOG)
java -jar ../Checker/target/consistency-1.0-SNAPSHOT-jar-with-dependencies.jar ../maelstrom/store/latest/history.edn