#!/bin/bash
set -e

dotnet restore
dotnet test tests/VStore.UnitTests -c release
rm -rf $(pwd)/publish/vstore
dotnet publish src/VStore.Host/project.json -c release -r ubuntu.16.04-x64 -o $(pwd)/publish/vstore
