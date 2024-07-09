#!/bin/bash -eu
export PATH=Loopy.ClientShell/bin/Debug/net8.0:Loopy.Node/bin/Debug/net8.0:$PATH
tmux new-session "Loopy.Node 1 --peers 2 3 4 --launch-peers" \; \
     split-window -h "Loopy.ClientShell"
