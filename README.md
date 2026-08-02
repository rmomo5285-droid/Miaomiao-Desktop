# 喵喵桌面端 (Miaomiao Desktop)

喵喵桌面端是基于 [v2rayN](https://github.com/2dust/v2rayN) 的 GPL-3.0 衍生客户端，
面向 Windows、macOS 和 Linux。项目保留 v2rayN 的代理能力，在客户端内增加喵喵账户、
套餐购买、公告和托管订阅能力。

## 客户端策略

- 登录后由账户接口下发唯一的 HTTPS 订阅地址，用户不需要手工粘贴机场订阅。
- 托管订阅每 48 小时自动更新；登录、支付完成和用户主动刷新可立即更新。
- 更新失败时保留本地节点，并按 15 分钟、60 分钟、6 小时退避重试。
- 服务入口由 ECDSA P-256 签名清单迁移；清单只允许下发 HTTPS 入口和展示型公告，不能执行远程命令。
- Hysteria2 节点固定使用 sing-box 内核，TUN 默认 MTU 为 1280。

## 支持平台

| 平台 | 架构 | 发布格式 |
| --- | --- | --- |
| Windows | x64, arm64 | ZIP |
| macOS | x64, arm64 | DMG |
| Linux | x64, arm64 | DEB, RPM |

正式安装包只通过 [GitHub Actions](.github/workflows/release-desktop.yml) 构建。稳定版发布要求：

- Windows 安装包使用主题包含 `Miaomiao` 的代码签名证书；
- macOS 应用使用 `Miaomiao` 品牌的 Developer ID，并完成公证；
- 所有发布文件生成 SHA-256 清单和喵喵发布密钥的 GPG 签名。

## 开发与许可

源代码继承上游 v2rayN，并继续遵循仓库中的 [GNU GPL v3](LICENSE)。分发修改版时必须同时满足
GPL-3.0 的源代码提供义务。上游项目、Xray、sing-box 及打包内核仍分别归其原作者所有；
`Miaomiao` / `喵喵` 仅表示本衍生客户端的产品品牌。

上游文档和协议兼容说明：[v2rayN Wiki](https://github.com/2dust/v2rayN/wiki)。
