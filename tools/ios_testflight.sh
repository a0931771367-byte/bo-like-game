#!/usr/bin/env bash
# Archive the Unity-generated Xcode project and upload it to App Store Connect (TestFlight).
#
# Run on the Mac after BuildRequestWatcher has produced unity/Builds/iOS (a "requested" build keeps the hidden
# QA entry, which is fine for TestFlight; use "store" for an App Store submission build).
#
#   TEAM_ID=XXXXXXXXXX tools/ios_testflight.sh
#
# Signing is automatic (Xcode account signed in with the paid team). The bundle id comes from Unity's
# ProjectSettings (com.pocketadventurers.game) and is not overridden here. Upload uses the Xcode account's
# App Store Connect session; to avoid interactive prompts pass an API key instead:
#   ASC_KEY_ID=… ASC_ISSUER_ID=… ASC_KEY_PATH=~/private_keys/AuthKey_….p8 TEAM_ID=… tools/ios_testflight.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/unity/Builds/iOS/Unity-iPhone.xcodeproj"
OUT="$ROOT/unity/Builds/testflight"
: "${TEAM_ID:?set TEAM_ID to the paid Apple Developer team id (Xcode > Settings > Accounts)}"

[ -d "$PROJ" ] || { echo "no Xcode project at $PROJ — request a build first (unity/Builds/build-request.txt)"; exit 1; }
mkdir -p "$OUT"
BUILD_NUMBER="$(cat "$ROOT/unity/Assets/Resources/build-number.txt" 2>/dev/null || echo unknown)"
ARCHIVE="$OUT/PocketAdventurers-$BUILD_NUMBER.xcarchive"
PLIST="$OUT/ExportOptions.plist"

cat > "$PLIST" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>upload</string>
  <key>teamID</key><string>$TEAM_ID</string>
  <key>signingStyle</key><string>automatic</string>
  <key>uploadSymbols</key><true/>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
PLIST

echo "== archive build $BUILD_NUMBER"
xcodebuild -project "$PROJ" -scheme Unity-iPhone -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" \
  -allowProvisioningUpdates DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic \
  archive | tail -n 20

AUTH=()
if [ -n "${ASC_KEY_ID:-}" ]; then
  AUTH=(-authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" -authenticationKeyPath "$ASC_KEY_PATH")
fi

echo "== upload to App Store Connect"
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$PLIST" -exportPath "$OUT/export-$BUILD_NUMBER" \
  -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} | tail -n 20

echo "== done: build $BUILD_NUMBER uploaded; it appears in App Store Connect > TestFlight after processing (5–30 min)."
