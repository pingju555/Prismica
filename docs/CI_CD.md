# Prismica CI/CD 与代码签名

本文说明如何让 Prismica 在每次提交/打 tag 时**自动构建、测试、打包安装包并签名**。

> 当前 `build/Publish.ps1` 已支持 `-Sign` 参数（Authenticode 签名），工作流文件位于 `.github/workflows/`。
> 仓库目前**还不是 git 仓库**——GitHub Actions 只有在代码推到 GitHub 后才生效。见文末「启用步骤」。

---

## 1. 两条流水线

| 文件 | 触发 | 做什么 |
|---|---|---|
| `ci.yml` | 任意 push / PR | `dotnet restore` → `build -c Release` → `dotnet test`（264 项全过才放行）→ 上传测试报告 |
| `release.yml` | 打 `v*` tag / 手动触发 | `restore -r win-x64` → `Publish.ps1` 出单文件 + Inno Setup 安装包 →（可选）签名 → 上传产物 + 建 GitHub Release |

CI 是质量门禁（测试不过就红灯）；Release 才产出可分发安装包。

---

## 2. 代码签名（两种方案）

### 方案 A：PFX 证书（传统，推荐先用这个）

1. 购买一张 **Authenticode 代码签名证书**（含 EV 证书可免 SmartScreen 信誉积累）。
2. 导出为 **PFX**（含私钥）。
3. 在仓库 `Settings > Secrets and variables > Actions` 增加两个 secret：
   - `SIGNING_CERT_PFX`：`[Convert]::ToBase64String((Get-Content cert.pfx -AsByteStream))` 的输出
   - `SIGNING_CERT_PASSWORD`：PFX 密码
4. 打 tag 推送，或手动 `Run workflow` 勾选 `sign`。工作流会自动解码证书并调用 `Publish.ps1 -Sign`。

生成 base64 的命令（PowerShell）：
```powershell
[Convert]::ToBase64String((Get-Content ".\your.pfx" -AsByteStream)) | Set-Clipboard
```

### 方案 B：Azure Trusted Signing（免证书管理，推荐长期）

微软官方托管签名服务，无需自己养证书，按次计费，原生对接 CI。

把 `release.yml` 里「Publish + Installer (+ Sign)」这一步替换为（需先在 Azure 配置 Trusted Signing 账户，并加 `AZURE_*` 三个 secret + 权限 `id-token: write`）：

```yaml
      - name: Azure Trusted Signing
        if: startsWith(github.ref, 'refs/tags/')
        uses: azure/trusted-signing-action@v0
        with:
          azure-tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          azure-client-id: ${{ secrets.AZURE_CLIENT_ID }}
          azure-client-secret: ${{ secrets.AZURE_CLIENT_SECRET }}
          endpoint: https://eus.codesigning.azure.net/
          trusted-signing-account-name: prismica
          certificate-profile-name: prismica-signing
          files-folder: dist/publish
          files-folder-filter: exe
          file-extension: exe
        # 然后对生成的 setup.exe 再单独签一次
```

签名会在 exe/setup 写入时间戳（`http://timestamp.digicert.com`），即使证书未来过期，已发布版本仍长期有效。

---

## 3. 本地手动签名（调试用）

```powershell
# PFX 方式
.\build\Publish.ps1 -Sign -CertFile .\my.pfx -CertPassword "***"

# 本机证书存储里的指纹方式
.\build\Publish.ps1 -Sign -CertThumbprint A1B2C3...
```

要求本机装有 **Windows SDK**（提供 `signtool.exe`）。未提供证书时带 `-Sign` 会直接报错提醒。

---

## 4. 启用步骤（首次）

> 以下命令在你本机 PowerShell 执行。**需要你有 GitHub 账号 `pingju555` 且已建好空仓库**；本助手未执行这些步骤（无凭证）。

```powershell
cd D:\Main\AI_Quest_Project\Prismica

# 1) 初始化仓库（作者信息按你的约定）
git init
git config user.name "pingju555"
git config user.email "2336317586@qq.com"

# 2) 先确认 .gitignore 已忽略 dist/、bin/、obj/ 等（已加 dist/）

# 3) 提交
git add .
git commit -m "chore: add CI/CD workflows and Authenticode signing support"

# 4) 关联远端并推送（替换 <repo> 为你的仓库名）
git branch -M main
git remote add origin https://github.com/pingju555/<repo>.git
git push -u origin main

# 5) 之后打 tag 即触发 Release 流水线
git tag v0.1.0-alpha
git push origin v0.1.0-alpha
```

推送后：
- 到仓库 **Actions** 页看 CI 是否全绿；
- 到 **Settings > Secrets** 填 `SIGNING_CERT_PFX` / `SIGNING_CERT_PASSWORD`；
- 打 tag 后到 **Releases** 页下载 `Prismica-0.1.0-alpha-setup.exe`。

---

## 5. 已知限制 / 备注

- 沙箱里无法端到端跑 GitHub Actions；`Publish.ps1` 的发布链路已在本机/沙箱用 `--no-restore` 验证通过（见 DEVPLAN #30）。
- 未配置证书时，Release 仍会产出**未签名**安装包（SmartScreen 会警告），功能不受影响。
- `dotnet test` 默认关闭 `PRISMICA_RUN_PERF_BASELINE`，不会写性能采样日志（见 DEVPLAN #26 相关说明）。
