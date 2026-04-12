pkgname="wsteam"
pkgdesc="CLI tool for downloading Steam games"
pkgver="0.1"
pkgrel="1"
arch=('aarch64' 'x86_64')
url="https://github.com/kietelmuis/wsteam"
license=('MIT')
depends=("steam")
options=('!strip' '!debug')

source_aarch64=("https://github.com/kietelmuis/wsteam/releases/download/v${pkgver}/wsteam-linux-arm64.tar.gz")
source_x86_64=("https://github.com/kietelmuis/wsteam/releases/download/v${pkgver}/wsteam-linux-x64.tar.gz")
sha512sums_x86_64=('SKIP')
sha512sums_aarch64=('SKIP')

package() {
  cd "$srcdir"

  install -d "$pkgdir/usr/lib/$pkgname"
  install -Dm755 wsteam "$pkgdir/usr/lib/$pkgname/wsteam"

  install -d "$pkgdir/usr/bin"
  ln -s "/usr/lib/$pkgname/wsteam" "$pkgdir/usr/bin/wsteam"
}
