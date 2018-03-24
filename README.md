# KNet

## What is KNet?
KNet is a C# Multiplayer Networking solution built by me. It is RPC based, and fairly easy to set up and use. All you have to do is implement your own Client and Server class that derive from the defaults, then implement your RPCs (all RPC functions must start with 'RPC'... and then they just work). If you're running locally (the IP the client connects to is the local host), there's no need for a server. However, server commands won't work (server commands are functions that get run on the server. They must start with 'CMD'). If you  do choose to run a server on local host, things just won't work (If someone could fix that it would be nice).
