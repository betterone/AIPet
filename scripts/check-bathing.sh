#!/bin/zsh
set -eu
cd "${0:A:h}/.."
mkdir -p artifacts
./scripts/test-core.sh > artifacts/bath-core-tests.log 2>&1
dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore -p:DefineConstants=UNITY_IOS > artifacts/bath-ios-csharp.log 2>&1
./scripts/build.sh mac
review_player="$PWD/Builds/macOS/Momo.app/Contents/MacOS/Momo"
"$review_player" -batchmode --bath-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/bath-player.log"
test -f artifacts/bathing/PASS.txt
test ! -f artifacts/bathing/FAIL.txt
"$review_player" -batchmode --garden-feeding-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/bath-garden-regression.log"
test -f artifacts/garden-feeding/PASS.txt
test ! -f artifacts/garden-feeding/FAIL.txt
"$review_player" -batchmode --multi-cat-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/bath-multicat-regression.log"
test -f artifacts/multicat/PASS.txt
test ! -f artifacts/multicat/FAIL.txt
cat artifacts/bathing/PASS.txt
