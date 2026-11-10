#!/bin/zsh
set -eu
cd "${0:A:h}/.."
mkdir -p artifacts
./scripts/test-core.sh > artifacts/quiet-hud-core.log 2>&1
dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore -p:DefineConstants=UNITY_IOS > artifacts/quiet-hud-ios-csharp.log 2>&1
./scripts/build.sh mac
review_player="$PWD/Builds/macOS/Momo.app/Contents/MacOS/Momo"
"$review_player" -batchmode --quiet-hud-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/quiet-hud-player.log"
test -f artifacts/quiet-hud/PASS.txt
test ! -f artifacts/quiet-hud/FAIL.txt
"$review_player" -batchmode --bath-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/quiet-hud-bath-regression.log"
test -f artifacts/bathing/PASS.txt
test ! -f artifacts/bathing/FAIL.txt
"$review_player" -batchmode --garden-feeding-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/quiet-hud-feeding-regression.log"
test -f artifacts/garden-feeding/PASS.txt
test ! -f artifacts/garden-feeding/FAIL.txt
"$review_player" -batchmode --multi-cat-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/quiet-hud-multicat-regression.log"
test -f artifacts/multicat/PASS.txt
test ! -f artifacts/multicat/FAIL.txt
cat artifacts/quiet-hud/PASS.txt
