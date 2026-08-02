# Miaomiao third-party notices

Miaomiao release packages aggregate unmodified runtime binaries and routing
data with the GPL-3.0-or-later Miaomiao client. These components remain under
their own licenses. The exact container archive URL and SHA-256 for each
platform are recorded in `CORE-BUNDLES.lock.tsv`.

Only the following runtime files are copied from the locked core archive:

- Xray and, on Windows, its bundled `wintun.dll`;
- sing-box, without the optional Cronet library;
- `geoip.dat`, `geosite.dat`, and the bundled sing-box rule sets.

Mihomo, Cronet, optional MMDB/MetaDB files, and the legacy EnableLoopback
utility are deliberately not distributed by Miaomiao.

## Xray-core 26.7.28

- Project: https://github.com/XTLS/Xray-core
- Corresponding source: https://github.com/XTLS/Xray-core/tree/v26.7.28
- License: Mozilla Public License 2.0
- Included license text: `licenses/XRAY-MPL-2.0.txt`

## sing-box 1.13.14

- Project: https://github.com/SagerNet/sing-box
- Corresponding source: https://github.com/SagerNet/sing-box/tree/v1.13.14
- License: GPL-3.0-or-later with the additional upstream name and association condition
- Included upstream notice: `licenses/SING-BOX-LICENSE.txt`
- The complete GPL version 3 text is also supplied as the package `LICENSE`.

## Wintun prebuilt binary

- Project: https://www.wintun.net/
- Source mirror: https://github.com/WireGuard/wintun
- License: WireGuard LLC Prebuilt Binaries License
- Included license text: `licenses/WINTUN-LICENSE.txt`

The unmodified Wintun DLL is distributed only alongside Xray and sing-box,
which use it through the permitted Wintun API.

## Routing data

- Locked bundle source: https://github.com/2dust/v2rayN-core-bin/tree/753dae71e7260b8374739f18bc683912aee8dfe6
- `geoip.dat` and `geosite.dat` source: https://github.com/Loyalsoldier/v2ray-rules-dat
- sing-box rule-set source: https://github.com/2dust/sing-box-rules
- Licenses: the locked bundle and source rule repository are distributed under GPL-3.0; the complete GPL version 3 text is supplied as `LICENSE`.

The lock commit is the reproducible corresponding source for the exact routing
files included in a Miaomiao release. It also records the upstream generation
workflow and the original data-source URLs used for those files.
