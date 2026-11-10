#!/bin/zsh
set -eu
cd "${0:A:h}/.."
editor="${MOMO_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$editor" ]]; then
  echo "Unity editor not found. Set MOMO_UNITY_EDITOR to its executable path."
  exit 1
fi
mkdir -p artifacts
case "${1:-mac}" in
  mac) method=Momo.Editor.ProjectBuilder.BuildMac ;;
  webgl) method=Momo.Editor.ProjectBuilder.BuildWebGL ;;
  ios) method=Momo.Editor.ProjectBuilder.BuildIOS ;;
  ios-simulator) method=Momo.Editor.ProjectBuilder.BuildIOSSimulator ;;
  ios-window) method=Momo.Editor.ProjectBuilder.BuildIOSWindow ;;
  *) echo "Usage: $0 {mac|webgl|ios|ios-simulator|ios-window}"; exit 2 ;;
esac
if [[ "${1:-mac}" == "ios-window" ]]; then
  python3 scripts/ios-signing.py capture Builds/iOS-Window/Unity-iPhone.xcodeproj/project.pbxproj artifacts/ios-window-signing.json
fi
"$editor" -batchmode -quit -projectPath "$PWD" -executeMethod "$method" -logFile "$PWD/artifacts/unity-build-${1:-mac}.log"
if [[ "${1:-mac}" == "ios-window" ]]; then
  python3 scripts/ios-signing.py restore Builds/iOS-Window/Unity-iPhone.xcodeproj/project.pbxproj artifacts/ios-window-signing.json
fi
