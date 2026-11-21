# patches

本目录保存「本分支相对上游 PCL-CE 的全部改动」。`dev` 分支可以随上游更新，
真正的改版效果来自这些补丁在构建时被重新应用。

## 当前补丁

| 文件 | 作用 |
| --- | --- |
| `0001-disable-region-restriction.patch` | 关闭「国家和地区」下载禁令（`RegionPolicy`、`RestrictedFeatures`）＋ 关于页署名（`PageSetupAbout.axaml` ＋ 4 套语言包） |

## 重新生成

补丁 = 上游基线提交到本分支工作区的 diff，只包含改版涉及的文件：

```powershell
git diff <上游基线提交> HEAD -- `
  PCL.Core/App/Region/RegionPolicy.cs `
  PCL.Launcher.Core/Ui/LogFilter.cs `
  PCL.Core.Test/RegionPolicyTest.cs `
  PCL.Launcher/Views/Pages/Setup/PageSetupAbout.axaml `
  PCL.Localization/Languages/zh-CN.axaml `
  PCL.Localization/Languages/zh-TW.axaml `
  PCL.Localization/Languages/en-US.axaml `
  PCL.Localization/Languages/en-GB.axaml `
  | Out-File -Encoding utf8NoBOM patches/0001-disable-region-restriction.patch
```

> 注意：不要用 PowerShell 的 `>` 重定向生成补丁，它会写成 UTF-16 导致 `git apply` 报
> `No valid patches in input`。用 `Out-File -Encoding utf8NoBOM` 或 `[IO.File]::WriteAllText`。

## 验证能否应用到最新上游

```powershell
git fetch upstream dev
git worktree add --detach .tmp-verify upstream/dev
git -C .tmp-verify apply --3way --check ..\patches\0001-disable-region-restriction.patch
git worktree remove .tmp-verify --force
```

## 上游改了相关文件怎么办

`sync-upstream` 工作流应用补丁失败时会自动开 issue。处理方式：

1. `git checkout dev && git merge upstream/dev`，手动解决冲突；
2. 在解决后的代码里重新确认「任何时区/地区都不受限」的语义没被上游改回去；
3. 按上面的命令重新生成补丁，验证、提交、推送；
4. 重新跑一次 `Sync upstream and build`。
