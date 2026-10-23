#!/bin/sh
# Build an Ubuntu 24.04 amd64 package from a clean checkout.
# The package is self-contained for .NET. It does not include PowerShell 7
# or GNOME Terminal, and it does not install them.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"
. "$root/packaging/ubuntu/debian-version.sh"

csproj="$root/src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"
informational=$(sed -n 's:.*<InformationalVersion>\([^<]*\)</InformationalVersion>.*:\1:p' "$csproj" | head -n 1)
if [ -z "$informational" ]; then
  echo "InformationalVersion was not found in the application project." >&2
  exit 1
fi
debian_version=$(debian_version_from_informational "$informational")
package=powershell-session-recorder
filename="${package}_${debian_version}_amd64.deb"

stage=$(mktemp -d)
cleanup() {
  rm -rf "$stage"
}
trap cleanup EXIT

publish="$stage/publish"
pkg="$stage/pkg"
mkdir -p "$publish" "$pkg"

revision_args=""
if [ -n "${SOURCE_REVISION_ID:-}" ]; then
  revision_args="-p:SourceRevisionId=${SOURCE_REVISION_ID} -p:ContinuousIntegrationBuild=true"
fi
# shellcheck disable=SC2086
dotnet publish "$csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$publish" \
  $revision_args

mkdir -p \
  "$pkg/opt/powershell-session-recorder" \
  "$pkg/usr/bin" \
  "$pkg/usr/share/applications" \
  "$pkg/usr/share/doc/$package" \
  "$pkg/usr/share/icons/hicolor" \
  "$pkg/DEBIAN"

cp -a "$publish/." "$pkg/opt/powershell-session-recorder/"
rm -f "$pkg/opt/powershell-session-recorder/"*.pdb
. "$root/packaging/reject-user-data.sh"
reject_user_data "$pkg/opt/powershell-session-recorder"
ln -s /opt/powershell-session-recorder/PowerShellSessionRecorder \
  "$pkg/usr/bin/powershell-session-recorder"
cp "$root/packaging/ubuntu/powershell-session-recorder.desktop" \
  "$pkg/usr/share/applications/powershell-session-recorder.desktop"
cp -a "$root/packaging/ubuntu/icons/hicolor/." "$pkg/usr/share/icons/hicolor/"
cp "$root/LICENSE" "$pkg/usr/share/doc/$package/copyright"
find "$pkg" -type d -exec chmod 755 {} +
find "$pkg" -type f -exec chmod 644 {} +
chmod 755 "$pkg/opt/powershell-session-recorder/PowerShellSessionRecorder"
find "$pkg/opt/powershell-session-recorder" -type f \( -name '*.so' -o -name createdump \) -exec chmod 755 {} +

installed_size=$(du -sk --apparent-size "$pkg/opt" "$pkg/usr" | awk '{sum += $1} END {print sum}')
echo "Installed-Size: $installed_size"
cat > "$pkg/DEBIAN/control" <<EOF
Package: $package
Version: $debian_version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: TimeWizard007 <timewizard007@users.noreply.github.com>
Homepage: https://github.com/TimeWizard007/powershell-session-recorder
Installed-Size: $installed_size
Depends: libc6 (>= 2.35), libgcc-s1, libstdc++6, libssl3t64, zlib1g, libicu74, libfontconfig1, libfreetype6, libpng16-16t64, libexpat1, libbrotli1, libbz2-1.0, libx11-6, libice6, libsm6
Suggests: gnome-terminal, xdg-desktop-portal, xdg-desktop-portal-gtk
Description: Local desktop recorder for PowerShell 7 sessions
 PowerShell Session Recorder opens an independent GNOME Terminal running
 PowerShell 7 with a transcript already started. Session metadata stays on
 this computer. PowerShell 7 is not part of this package and is not in the
 default Ubuntu 24.04 repositories. The application does not install packages.
EOF

(
  cd "$pkg"
  find . -type f ! -path './DEBIAN/*' -print0 | sort -z | xargs -0 md5sum > DEBIAN/md5sums
)
chmod 644 "$pkg/DEBIAN/control" "$pkg/DEBIAN/md5sums"
cat > "$pkg/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f -t /usr/share/icons/hicolor || true
fi
EOF
cat > "$pkg/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f -t /usr/share/icons/hicolor || true
fi
EOF
chmod 755 "$pkg/DEBIAN/postinst" "$pkg/DEBIAN/postrm"

mkdir -p "$root/artifacts"
dpkg-deb --root-owner-group --build "$pkg" "$root/artifacts/$filename"
(
  cd "$root/artifacts"
  sha256sum "$filename" > "$filename.sha256"
)
echo "$root/artifacts/$filename"
echo "$root/artifacts/$filename.sha256"
