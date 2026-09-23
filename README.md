# 协商岔路图（SDP Offer/Answer 审阅器）

面向实时通信排障的本地审阅器：输入多轮 SDP offer/answer 与 ICE restart 记录，
按 offer-answer 规则重建每轮协商图，回答“某个 media section 为何被拒绝、
沿用了哪次 transport”。原文、行序、换行方式与会话轮次全部保留。

## 运行

```bash
dotnet restore && dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/App -- --urls http://127.0.0.1:5990
# 打开 http://127.0.0.1:5990 查看“协商岔路图”
```

全部验证在本机完成，不依赖账号、云服务或随机网络时序。

## 结构

- `src/Core/Sdp/` — SDP 解析器。每行保留 `Raw`，序列化逐行回写，
  未理解的属性按原顺序原样保留；换行风格（CRLF/LF）随文档保留。
- `src/Core/Analysis/` — 协商分析器。逐轮计算 codec 交集（按 codec 身份而非 PT 号）、
  方向协商（offerer 视角）、BUNDLE 传输归属与 ICE 代次。
- `src/Core/Fixtures/` — 内置三轮记录：rejected m-line 回收、ICE restart、
  bundle master 迁移、同 PT 异 codec、晚到候选。
- `src/App/` — ASP.NET Core 站点（`/` 页面、`/api/graph` JSON）与 SQLite 持久化。
- `src/App/Migrations/001_init.sql` — SQLite 迁移，按 `PRAGMA user_version` 顺序应用。
- `tests/Core.Tests/` — xUnit 测试（19 个）。

## 规则要点

- m-line 顺序即身份：端口 0 拒绝的 section 保留在原位置，列表不压缩。
- PT 仅在对应 media 内解释：动态 PT（96–127）同号不代表同 codec，
  跨 section 冲突产生 `pt-codec-mismatch` 诊断。
- ICE ufrag/pwd 任一变化 → 代次 +1 并留下 `ice-credential-change` 诊断。
- bundle master 改选 → `bundle-master-migration`；传输归属跟随 answer 的
  `group:BUNDLE` 首个 mid。
- answer 完成后到达的 candidate → `late-trickle-candidate`。
- offer/answer 同 id 不同 URI 的 extmap → `extmap-conflict`。

## 持久化

首次启动在输出目录创建 `reviewer.db`（可用环境变量 `REVIEWER_DB` 覆盖路径），
应用迁移后把内置 fixture 的原始 SDP、逐 media 协商状态与诊断落库；
页面渲染时基于原始记录重新分析，保证可复现。

## 依赖锁定

各项目含 `packages.lock.json`（`RestorePackagesWithLockMode=true`）。
更新依赖后执行 `dotnet restore --use-lock-file --force-evaluate` 重新生成；
CI 可用 `dotnet restore --locked-mode` 强制校验。
