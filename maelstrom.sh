#!/bin/bash -eu
BIN="$PWD/Loopy.MaelstromNode/bin/Release/net8.0/Loopy.MaelstromNode"
ARGS="Fifo 1"
PARAMS="-w fifo-kv --node-count 4 --concurrency 16 --rate 5 --latency 250 --time-limit 60 --nemesis partition"
LOG="--log-stderr true --log-net-send true --log-net-recv true"
LOG=""

dotnet build Loopy.MaelstromNode -c Release

if [ -d ../maelstrom ]; then
  cd ../maelstrom && exec lein run test --bin $BIN $ARGS $PARAMS $LOG
elif command -v maelstrom; then
  exec maelstrom test --bin $BIN $ARGS $PARAMS $LOG
else
  echo maelstrom not found
  exit -1
fi
