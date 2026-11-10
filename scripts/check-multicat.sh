#!/bin/zsh
set -eu
cd "${0:A:h}/.."
mkdir -p artifacts
./scripts/test-core.sh > artifacts/multicat-core-tests.log 2>&1
dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore -p:DefineConstants=UNITY_IOS > artifacts/multicat-ios-csharp.log 2>&1
./scripts/build.sh mac
review_player="$PWD/Builds/macOS/Momo.app/Contents/MacOS/Momo"
"$review_player" -batchmode --multi-cat-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/multicat-player.log"
test -f artifacts/multicat/PASS.txt
test ! -f artifacts/multicat/FAIL.txt
"$review_player" -batchmode --garden-feeding-review -screen-width 390 -screen-height 844 -logFile "$PWD/artifacts/multicat-garden-regression.log"
test -f artifacts/garden-feeding/PASS.txt
test ! -f artifacts/garden-feeding/FAIL.txt
"$review_player" -batchmode --boutique-review -screen-width 640 -screen-height 480 -logFile "$PWD/artifacts/multicat-boutique-regression.log"
test -f artifacts/boutique/PASS.txt
cat artifacts/multicat/PASS.txt
