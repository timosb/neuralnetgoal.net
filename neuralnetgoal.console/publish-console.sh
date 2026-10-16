#!/usr/bin/env bash
# Builds a self-contained version of the console for THIS Mac (or Linux): no .NET needs to be installed to run it.
# Usage:  ./neuralnetgoal.console/publish-console.sh       (run once:  chmod +x neuralnetgoal.console/publish-console.sh)
# Result: publish/console-mac/neuralnetgoal.console   (run it from a terminal; it needs the secrets, see the README)
set -euo pipefail
cd "$(dirname "$0")/.."

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64)  RID=osx-arm64 ;;
  Darwin-x86_64) RID=osx-x64 ;;
  Linux-x86_64)  RID=linux-x64 ;;
  Linux-aarch64) RID=linux-arm64 ;;
  *) echo "Unsupported platform: $(uname -s) $(uname -m)"; exit 1 ;;
esac

echo "Building for $RID ..."
dotnet publish neuralnetgoal.console -c Release -r "$RID" --self-contained -p:PublishSingleFile=true -o publish/console-mac
echo
echo "Done. Run it with:  ./publish/console-mac/neuralnetgoal.console"
