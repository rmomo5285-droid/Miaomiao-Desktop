# 喵喵桌面端 (Miaomiao Desktop)

喵喵桌面端是基于 [v2rayN](https://github.com/2dust/v2rayN) 的 GPL-3.0 衍生客户端，
面向 Windows、macOS 和 Linux。项目保留 v2rayN 的代理能力，在客户端内增加喵喵账户、
套餐购买、公告和托管订阅能力。

## 客户端策略

- 登录后由账户接口下发唯一的 HTTPS 订阅地址，用户不需要手工粘贴机场订阅。
- 托管订阅每 48 小时自动更新；登录、支付完成和用户主动刷新可立即更新。
- 更新失败时保留本地节点，并按 15 分钟、60 分钟、6 小时退避重试。
- 服务入口由 ECDSA P-256 签名清单迁移；清单只允许下发 HTTPS 入口和展示型公告，不能执行远程命令。
- 镜像刷新会扫描全部有效响应并稳定选择最高版本，同版本时保留清单中的首选顺序；下载页可回退到内置 HTTPS 地址。
- 登录令牌使用本机随机 AES-GCM 密钥加密并以仅当前用户可读权限持久化；401 或退出登录会清除会话。
- 工具页提供核心更新检查和签名清单下载入口。
- Hysteria2 节点固定使用 sing-box 内核，TUN 默认 MTU 为 1280。

## 支持平台

| 平台 | 架构 | 发布格式 |
| --- | --- | --- |
| Windows | x64, arm64 | ZIP |
| macOS | x64, arm64 | DMG |
| Linux | x64, arm64 | DEB, RPM |

正式安装包只通过 [GitHub Actions](.github/workflows/release-desktop.yml) 构建。发布方式与
v2rayN 上游一致：所有平台安装包均生成喵喵发布密钥的 GPG 分离签名，并同时发布公钥和
`SHA256SUMS` 校验清单。Windows 安装包不包含 Authenticode 签名，macOS 安装包不包含
Apple Developer ID 签名与公证票据，首次运行时可能显示系统安全提示。

## Orange 图标

品牌母版位于 `branding/orange-icon.png` 和 `branding/orange-icon.ico`。安装 ImageMagick 与
`icnsutils` 后运行 `bash branding/generate-orange-icons.sh` 可重建 Linux PNG、Windows ICO、
四种托盘状态图标和 macOS ICNS；`bash branding/verify-orange-icons.sh` 校验母版哈希、格式、
尺寸和 Avalonia 引用。普通 push/PR 的 CI 会上传 `miaomiao-desktop-orange-icon-review` 供视觉审核。

## 开发与许可

源代码继承上游 v2rayN，并继续遵循仓库中的 [GNU GPL v3](LICENSE)。分发修改版时必须同时满足
GPL-3.0 的源代码提供义务。上游项目、Xray、sing-box 及打包内核仍分别归其原作者所有；
`Miaomiao` / `喵喵` 仅表示本衍生客户端的产品品牌。

上游文档和协议兼容说明：[v2rayN Wiki](https://github.com/2dust/v2rayN/wiki)。
