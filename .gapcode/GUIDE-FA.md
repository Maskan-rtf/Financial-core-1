# راهنمای کامل GapCode CLI — Financial-Core

> **تقلب‌نامه (Cheat Sheet)** برای کار با GapCode در WSL  
> نسخه GapCode: `0.137.1` · پروژه: Maskan Financial-Core

---

## فهرست

1. [GapCode چیه؟](#1-gapcode-چیه)
2. [سه نوع «کامند» — مهم!](#2-سه-نوع-کامند--مهم)
3. [شروع سریع](#3-شروع-سریع)
4. [کامندهای CLI (ترمینال)](#4-کامندهای-cli-ترمینال)
5. [حالت تعاملی (TUI)](#5-حالت-تعاملی-tui)
6. [کلیدهای میانبر](#6-کلیدهای-میانبر)
7. [اسلش‌کامندها (`/...`)](#7-اسلشکامندها-)
8. [Vim — توضیح فارسی](#8-vim--توضیح-فارسی)
9. [MCP — ابزارهای اضافه](#9-mcp--ابزارهای-اضافه)
10. [Skills — مهارت‌های پروژه](#10-skills--مهارتهای-پروژه)
11. [مجوزها و Sandbox](#11-مجوزها-و-sandbox)
12. [Session — ادامه مکالمه](#12-session--ادامه-مکالمه)
13. [تنظیمات (`config.toml`)](#13-تنظیمات-configtoml)
14. [راه‌اندازی Financial-Core](#14-راهاندازی-financial-core)
15. [سناریوهای عملی](#15-سناریوهای-عملی)
16. [عیب‌یابی](#16-عیبیابی)
17. [تقلب‌نامه یک‌صفحه‌ای](#17-تقلبنامه-یکصفحهای)

---

## 1. GapCode چیه؟

**GapCode** یک **agent کدنویسی در ترمینال** است (بر پایه OpenAI Codex). مثل Cursor ولی داخل WSL/ترمینال:

| قابلیت | توضیح |
|--------|--------|
| خواندن کد | فایل‌های پروژه رو می‌خونه |
| ویرایش کد | فایل‌ها رو تغییر می‌ده |
| اجرای دستور | `dotnet build`, `git`, `ef` و... |
| بررسی کد | code review |
| استفاده از Skill | قوانین پروژه (`/validation`, ...) |
| اتصال MCP | دیتابیس، مرورگر، GitHub |

**مسیر نصب:** `~/.gapcode/`  
**دستور اجرا:** `gapcode` (در WSL)

---

## 2. سه نوع «کامند» — مهم!

خیلی‌ها گیج می‌شن چون سه جور «کامند» داریم:

```
┌─────────────────────────────────────────────────────────────┐
│  ① CLI Command     →  قبل از ورود به GapCode، در bash می‌زنی │
│     gapcode exec "..."                                       │
│     gapcode review --uncommitted                             │
├─────────────────────────────────────────────────────────────┤
│  ② Slash Command   →  داخل GapCode، با / شروع می‌شه         │
│     /review    /skills    /diff    /permissions              │
├─────────────────────────────────────────────────────────────┤
│  ③ Shell Command   →  داخل GapCode، با ! شروع می‌شه          │
│     !dotnet build                                            │
│     !git status                                              │
└─────────────────────────────────────────────────────────────┘
```

| نوع | پیشوند | کجا | مثال |
|-----|--------|-----|------|
| **CLI** | ندارد | ترمینال WSL | `gapcode resume --last` |
| **Slash** | `/` | داخل TUI | `/review` |
| **Shell** | `!` | داخل TUI | `!dotnet test` |

**Prompt** (دستور زبان طبیعی) کامند نیست — فقط متنیه که به AI می‌گی:
```
یک endpoint جدید برای loan case اضافه کن
```

---

## 3. شروع سریع

### اولین بار

```bash
# 1. برو به پروژه
cd /mnt/d/work/Maskan/Panel/Financial-Core

# 2. لاگین (اگه نشدی)
gapcode login

# 3. شروع چت تعاملی
gapcode
```

### هر بار که می‌خوای کار کنی

```bash
cd /mnt/d/work/Maskan/Panel/Financial-Core
gapcode
```

داخل GapCode بنویس:
```
با /validation تغییرات من رو بررسی کن
```

### یک‌خطی (بدون ورود به TUI)

```bash
gapcode exec "خلاصه تغییرات uncommitted رو بگو"
```

---

## 4. کامندهای CLI (ترمینال)

### دستور اصلی

```bash
gapcode [OPTIONS] [PROMPT]          # حالت تعاملی
gapcode <COMMAND> [ARGS]            # زیردستور
```

### همه زیردستورها

| کامند | مخفف | کاربرد |
|-------|------|--------|
| `gapcode` | — | باز کردن چت تعاملی (TUI) |
| `gapcode exec` | `gapcode e` | یک‌بار اجرا، بدون TUI |
| `gapcode review` | — | بررسی کد بدون چت |
| `gapcode resume` | — | ادامه session قبلی |
| `gapcode fork` | — | شاخه‌گرفتن از session |
| `gapcode apply` | `gapcode a` | اعمال diff با git apply |
| `gapcode login` | — | ورود |
| `gapcode logout` | — | خروج |
| `gapcode doctor` | — | بررسی سلامت نصب |
| `gapcode update` | — | به‌روزرسانی |
| `gapcode mcp` | — | مدیریت MCP |
| `gapcode plugin` | — | مدیریت plugin |
| `gapcode features` | — | feature flagها |
| `gapcode completion` | — | تکمیل خودکار shell |
| `gapcode archive` | — | آرشیو session |
| `gapcode unarchive` | — | بازگردانی session |
| `gapcode --help` | `-h` | راهنما |
| `gapcode --version` | `-V` | نسخه |

### فلگ‌های پرکاربرد

| فلگ | کاربرد | مثال |
|-----|--------|------|
| `-C, --cd <dir>` | پوشه کاری | `gapcode -C /mnt/d/.../Financial-Core` |
| `-m, --model <name>` | انتخاب مدل | `gapcode -m claude-sonnet-4-6` |
| `-i, --image <file>` | ضمیمه عکس | `gapcode -i screenshot.png "این خطا چیه؟"` |
| `-s, --sandbox <mode>` | سطح sandbox | `gapcode -s read-only` |
| `--search` | جستجوی وب | `gapcode --search "EF Core pagination"` |
| `-c key=value` | override تنظیمات | `gapcode -c model="claude-sonnet-4-6"` |
| `--no-alt-screen` | حالت inline (scrollback حفظ شه) | `gapcode --no-alt-screen` |

### `gapcode exec` — اجرای یک‌باره

```bash
# ساده
gapcode exec "Fix the failing test"

# با پوشه مشخص
gapcode exec -C /mnt/d/work/Maskan/Panel/Financial-Core "dotnet build"

# خروجی در فایل
gapcode exec -o /tmp/result.txt "Summarize my changes"

# JSON برای اسکریپت
gapcode exec --json "List changed files"

# ادامه session قبلی
gapcode exec resume --last "ادامه بده"
```

### `gapcode review` — بررسی کد

```bash
# تغییرات commit نشده
gapcode review --uncommitted

# مقایسه با branch
gapcode review --base main

# یک commit خاص
gapcode review --commit abc123

# با دستورالعمل سفارشی
gapcode review --uncommitted "طبق AGENTS.md و gateهای /security بررسی کن"
```

### `gapcode resume` — ادامه مکالمه

```bash
gapcode resume              # لیست sessionها → انتخاب
gapcode resume --last       # آخرین session
gapcode resume --all        # همه sessionها (همه پوشه‌ها)
gapcode resume <session-id> # session مشخص
```

### `gapcode doctor` — عیب‌یابی نصب

```bash
gapcode doctor
```

چک می‌کنه: git، ripgrep، config، auth، نسخه.

---

## 5. حالت تعاملی (TUI)

وقتی `gapcode` می‌زنی، یک **صفحه تمام‌صفحه** باز می‌شه:

```
┌──────────────────────────────────────────────────────────┐
│  [مدل] [context باقی‌مانده] [پوشه فعلی]     ← status line │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ← خروجی agent (متن، کد، diff، نتیجه دستورات)           │
│                                                          │
│                                                          │
├──────────────────────────────────────────────────────────┤
│  > prompt اینجا تایپ می‌کنی...              ← composer   │
└──────────────────────────────────────────────────────────┘
```

### چه اتفاقی می‌افته وقتی prompt می‌فرستی؟

1. Agent فایل‌های پروژه رو می‌خونه (`AGENTS.md`, کد، ...)
2. یک **plan** می‌سازه
3. ممکنه **دستور shell** پیشنهاد بده → از تو **تأیید** می‌خواد
4. فایل‌ها رو ویرایش می‌کنه
5. نتیجه رو نشون می‌ده

### وقتی agent مشغوله

| عمل | کلید | معنی |
|-----|------|------|
| تزریق به turn فعلی | **Enter** | همین الان بگو چی عوض شه |
| صف برای turn بعد | **Tab** | بعد از تموم شدن اجرا شه |

---

## 6. کلیدهای میانبر

### عمومی (بدون Vim)

| کلید | کار |
|------|-----|
| **Enter** | ارسال prompt |
| **Tab** | صف کردن برای turn بعد (وقتی agent مشغوله) |
| **Ctrl+O** | کپی آخرین پاسخ agent |
| **Ctrl+L** | پاک کردن صفحه (مکالمه حفظ می‌شه) |
| **Ctrl+G** | باز کردن prompt در ویرایشگر خارجی (`$EDITOR`) |
| **Ctrl+R** | جستجو در تاریخ promptها |
| **Ctrl+C** | قطع / لغو |
| **Esc + Esc** | ویرایش پیام قبلی (می‌تونه fork کنه) |
| **Up / Down** | پیش‌نویس‌های قبلی |
| **@** | جستجوی فایل → ضمیمه به prompt |
| **!cmd** | اجرای shell (مثلاً `!dotnet build`) |
| **/** | باز کردن منوی slash command |

### `@` — ضمیمه فایل

```
@CaseService.cs          ← فایل رو به prompt اضافه کن
@Core.Application/       ← پوشه
```

### `!` — دستور shell

```
!dotnet build
!git status
!git diff --stat
```

خروجی مثل نتیجه دستور به agent داده می‌شه. باز هم ممکنه تأیید بخواد.

---

## 7. اسلش‌کامندها (`/...`)

داخل composer تایپ کن `/` → منو باز می‌شه.

### پرکاربردترین‌ها

| اسلش | کار | کی استفاده کنی |
|------|-----|----------------|
| `/skills` | انتخاب skill | قبل از task تخصصی |
| `/review` | بررسی working tree | قبل از commit |
| `/diff` | نمایش git diff | بعد از ویرایش agent |
| `/model` | تغییر مدل | وقتی مدل قوی‌تر/سریع‌تر می‌خوای |
| `/permissions` | Auto / Read-only / Full | کنترل دسترسی |
| `/status` | وضعیت session | مدل، توکن، sandbox |
| `/mcp` | لیست MCPها | ببین postgres/playwright فعاله |
| `/clear` | چت جدید + پاک صفحه | شروع تازه |
| `/new` | چت جدید | بدون پاک کردن صفحه |
| `/resume` | ادامه session | برگشت به کار قبلی |
| `/fork` | شاخه از مکالمه | امتحان روش دیگه |
| `/compact` | خلاصه‌سازی چت | وقتی context پر شده |
| `/copy` | کپی آخرین پاسخ | مثل Ctrl+O |
| `/plan` | حالت برنامه‌ریزی | قبل از پیاده‌سازی بزرگ |
| `/init` | ساخت AGENTS.md | پروژه جدید |
| `/vim` | روشن/خاموش Vim | اختیاری |
| `/keymap` | تغییر کلیدها | سفارشی‌سازی |
| `/theme` | تم syntax | ظاهر کد |
| `/quit` | خروج | پایان session |

### مدیریت session

| اسلش | کار |
|------|-----|
| `/resume` | انتخاب session ذخیره‌شده |
| `/fork` | کپی مکالمه → مسیر جدید |
| `/archive` | آرشیو + خروج |
| `/side` | مکالمه جانبی موقت |

### بررسی و debug

| اسلش | کار |
|------|-----|
| `/diff` | تغییرات git |
| `/review` | code review |
| `/status` | مدل، توکن، sandbox |
| `/debug-config` | لایه‌های config |
| `/mcp` | ابزارهای MCP |
| `/mcp verbose` | جزئیات MCP |

---

## 8. Vim — توضیح فارسی

### لازمه؟ **نه.**

بدون Vim کاملاً کار می‌کنه. فقط جعبه prompt پایین صفحه Vim-style می‌شه.

### روشن/خاموش

```
/vim
```

یا در `~/.gapcode/config.toml`:
```toml
[tui]
vim_mode_default = true   # همیشه روشن
```

### دو حالت

| حالت | معنی | چطور بفهمی |
|------|------|------------|
| **INSERT** | تایپ عادی | پایین صفحه: `INSERT` |
| **NORMAL** | حرکت/ویرایش با کلید | پایین صفحه: `NORMAL` |

```
NORMAL ──[ i ]──► INSERT ──[ Esc ]──► NORMAL
   │                  │
   │ dd, yy, p        │ تایپ معمولی
   │ h,j,k,l          │
```

### کلیدهای NORMAL

| کلید | کار | فارسی |
|------|-----|--------|
| `i` | برو INSERT | شروع تایپ |
| `a` | بعد از کرسر INSERT | تایپ بعد از حرف |
| `o` | خط جدید پایین | خط جدید |
| `Esc` | برگرد NORMAL | خروج از تایپ |
| `h` `j` `k` `l` | چپ پایین بالا راست | حرکت |
| `w` | کلمه بعد | — |
| `b` | کلمه قبل | — |
| `0` | اول خط | — |
| `$` | آخر خط | — |
| `dd` | پاک کردن خط | — |
| `yy` | کپی خط | — |
| `p` | paste | چسباندن |
| `x` | پاک یک حرف | — |
| `u` | undo | برگشت |

**توصیه:** اگه Vim بلد نیستی، `/vim` نزن.

---

## 9. MCP — ابزارهای اضافه

**MCP** = پروتکل وصل کردن ابزار خارجی به agent.

### MCPهای نصب‌شده در این پروژه

| MCP | کار | وضعیت |
|-----|-----|--------|
| **postgres** | query روی DB FinancialCore (فقط SELECT) | ✅ نصب |
| **playwright** | مرورگر — تست Frontend | ✅ نصب |
| **github** | PR، issue، repo | ⏳ نیاز به token |

### بررسی

```bash
gapcode mcp list        # در ترمینال
/mcp                    # داخل GapCode
```

### نصب / به‌روزرسانی

```bash
bash tools/setup-gapcode-mcp.sh
```

رمزها در `~/.gapcode/secrets.env` (commit **نمی‌شه**).

### postgres — مثال prompt

```
با MCP postgres لیست schemaها رو بده
جدول InvestmentCases چند رکورد داره؟
ساختار جدول LoanCases رو نشون بده
```

> فقط **خواندن** — `INSERT`/`UPDATE`/`DELETE` نداره (امن‌تر از DBeaver برای agent).

### playwright — مثال prompt

```
Frontend رو با python -m http.server 5500 بالا بیار و صفحه login رو باز کن
```

### github — فعال‌سازی

1. https://github.com/settings/tokens → token با دسترسی `repo`
2. در `~/.gapcode/secrets.env`:
   ```
   GITHUB_PERSONAL_ACCESS_TOKEN=ghp_xxxx
   ```
3. `bash tools/setup-gapcode-mcp.sh`

---

## 10. Skills — مهارت‌های پروژه

Skill = دستورالعمل تخصصی. با `/skills` یا نوشتن نام در prompt.

### بک‌اند — Quality Gates

| Skill | فارسی | کاربرد |
|-------|-------|--------|
| `/validation` | اعتبارسنجی | FluentValidation برای هر Request DTO |
| `/mapping` | نگاشت DTO | Mapster — هیچ فیلدی گم نشه |
| `/logging` | لاگ | Serilog + BusinessProcess + CorrelationId |
| `/persistence` | ذخیره‌سازی | DTO→DB با read-back اثبات |
| `/security` | امنیت | auth، IDOR، نشت secret |
| `/repository` | لایه داده | UoW + Repository، بدون DbContext در App |
| `/ef` | عملکرد EF | N+1، pagination، AsNoTracking |
| `/cqrs` | تفکیک read/write | فقط با توجیه — نه CRUD ساده |
| `/refactor` | بازسازی امن | تغییر ساختار، رفتار ثابت |

### فرانت‌اند

| Skill | فارسی | کاربرد |
|-------|-------|--------|
| `/frontend-api` | API | `TestPanel.apiRequest` + unwrap envelope |
| `/frontend-ui` | UI | loading / error / empty + ui-components |
| `/frontend-workflow` | workflow | investment/guarantee/loan model + portal |

### مستندسازی و ابزار

| Skill | فارسی | کاربرد |
|-------|-------|--------|
| `/backend-developer-docs` | مستند بک | `docs/backend/` |
| `/frontend-developer-docs` | مستند فرانت | `docs/frontend/` |
| `/organize-git-commits` | commit مرتب | تقسیم تغییرات به commitهای منطقی |

### نقشه انتخاب skill

```
endpoint جدید     → validation → mapping → logging → persistence → security → ef
باگ داده          → logging → persistence
باگ auth          → security
query کند         → ef
UI جدید           → frontend-ui + frontend-api
workflow step     → frontend-workflow
قبل از merge      → همه gateهای مربوط
```

### sync skills بعد از تغییر

```bash
bash tools/sync-gapcode.sh
```

---

## 11. مجوزها و Sandbox

### سه حالت دسترسی (`/permissions`)

| حالت | معنی |
|------|------|
| **Auto** (پیش‌فرض) | ویرایش و دستور در پروژه؛ خارج از پروژه/شبکه → تأیید |
| **Read-only** | فقط نگاه — بدون ویرایش تا تأیید |
| **Full access** | کمترین محدودیت — با احتیاط |

### Sandbox (`-s`)

| مقدار | معنی |
|-------|------|
| `read-only` | فقط خواندن فایل |
| `workspace-write` | نوشتن در workspace |
| `danger-full-access` | تقریباً بدون محدودیت |

### وقتی agent دستور پیشنهاد می‌ده

```
Agent: می‌خوام dotnet build بزنم
  → [Approve]  [Decline]
```

- **Approve** = اجرا
- **Decline** = رد — agent راه دیگه می‌ره

---

## 12. Session — ادامه مکالمه

GapCode مکالمات رو ذخیره می‌کنه (`~/.gapcode/sessions/`).

```bash
gapcode resume --last          # آخرین
gapcode resume                 # انتخاب از لیست
gapcode fork --last            # کپی برای امتحان روش دیگه
```

داخل TUI: `/resume`, `/fork`, `/archive`

---

## 13. تنظیمات (`config.toml`)

مسیر: `~/.gapcode/config.toml`

```toml
model = "claude-sonnet-4-6"
model_provider = "gapgpt"
model_reasoning_effort = "medium"

[model_providers.gapgpt]
name = "GapGPT"
base_url = "https://api.gapgpt.app/v1"

[tui]
status_line = ["model-with-reasoning", "context-remaining", "current-dir"]

# بارگذاری AGENTS.md پروژه
project_doc_fallback_filenames = [".gapcode/AGENTS.md", ".cursor/AGENTS.md"]
project_doc_max_bytes = 65536

[projects."/mnt/d/work/Maskan/Panel/Financial-Core"]
trust_level = "trusted"
```

### فایل‌های مهم

| فایل | محل | محتوا |
|------|-----|--------|
| config | `~/.gapcode/config.toml` | مدل، TUI، پروژه‌های trusted |
| secrets | `~/.gapcode/secrets.env` | DB URL، GitHub token |
| skills | `~/.gapcode/skills/` | ۱۵ skill |
| sessions | `~/.gapcode/sessions/` | تاریخچه مکالمات |
| AGENTS | `AGENTS.md` (ریشه repo) | قوانین پروژه |

---

## 14. راه‌اندازی Financial-Core

### چک‌لیست

```bash
# 1. پروژه
cd /mnt/d/work/Maskan/Panel/Financial-Core

# 2. skills
bash tools/sync-gapcode.sh

# 3. MCP
bash tools/setup-gapcode-mcp.sh

# 4. سلامت
gapcode doctor

# 5. شروع
gapcode
```

### AGENTS.md

در ریشه repo — GapCode خودکار می‌خونه:
- معماری لایه‌ها
- قوانین بک‌اند و فرانت
- لیست skills
- دستورات `dotnet build`, `ef migrations`

---

## 15. سناریوهای عملی

### Feature جدید (API)

```
1. gapcode
2. /plan
3. "یک endpoint برای لیست loan installments اضافه کن طبق AGENTS.md"
4. تأیید دستورات dotnet/ef
5. /diff
6. /review
7. !dotnet build
```

### بررسی قبل از commit

```
/review
/diff
با /validation و /security و /persistence تغییرات من رو بررسی کن
```

### باگ

```
با /logging و /persistence باگ [توضیح] رو پیدا و fix کن
!dotnet test
```

### فرانت‌اند

```
با /frontend-ui و /frontend-api به tab جدید loading/error/empty اضافه کن
```

### دیتابیس

```
با MCP postgres جدول X رو بررسی کن — آیا فیلد Y null داره؟
```

### یک‌خطی CI/اسکریپت

```bash
gapcode exec -C /mnt/d/work/Maskan/Panel/Financial-Core \
  "Run dotnet build and report errors"
```

---

## 16. عیب‌یابی

| مشکل | راه‌حل |
|------|--------|
| `gapcode: command not found` | WSL باز کن؛ PATH شامل `~/.gapcode/bin` باشه |
| login نشده | `gapcode login` |
| skill پیدا نمی‌شه | `bash tools/sync-gapcode.sh` |
| MCP خالی | `bash tools/setup-gapcode-mcp.sh` |
| `rg not found` در doctor | `sudo apt install ripgrep` |
| agent گیر کرده | Ctrl+C |
| context پر شده | `/compact` |
| می‌خوام از اول | `/clear` یا `/new` |
| تغییرات agent رو نمی‌خوام | `git checkout .` یا `git stash` |

```bash
gapcode doctor          # تشخیص کلی
gapcode mcp list        # MCPها
gapcode login status    # وضعیت لاگین
```

---

## 17. تقلب‌نامه یک‌صفحه‌ای

```
┌─────────────────────────────────────────────────────────────────┐
│                        GAPCODE CHEAT SHEET                       │
├─────────────────────────────────────────────────────────────────┤
│ شروع:     cd .../Financial-Core && gapcode                      │
│ یک‌خطی:   gapcode exec "task"                                   │
│ review:   gapcode review --uncommitted                          │
│ ادامه:    gapcode resume --last                                 │
├─────────────────────────────────────────────────────────────────┤
│ داخل TUI:                                                       │
│   /skills      /review      /diff       /status                 │
│   /permissions /mcp         /compact    /clear                  │
│   @file        !dotnet build                                    │
│   Ctrl+O کپی   Ctrl+L پاک صفحه   Ctrl+C قطع   Tab صف            │
├─────────────────────────────────────────────────────────────────┤
│ Skills بک:  /validation /mapping /logging /persistence          │
│             /security /repository /ef                           │
│ Skills فرانت: /frontend-api /frontend-ui /frontend-workflow     │
├─────────────────────────────────────────────────────────────────┤
│ MCP: postgres (DB) | playwright (browser) | github (نیاز token) │
│ Sync: bash tools/sync-gapcode.sh                                │
│ MCP:  bash tools/setup-gapcode-mcp.sh                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## پیوست — تفاوت با Cursor

| موضوع | Cursor (IDE) | GapCode (WSL CLI) |
|-------|--------------|-------------------|
| محیط | داخل VS Code/Cursor | ترمینال |
| ویرایش فایل | در editor | agent مستقیم |
| قوانین | `.cursor/rules/` | `AGENTS.md` + skills |
| Skills | `.cursor/skills/` | `~/.gapcode/skills/` |
| MCP | Cursor settings | `gapcode mcp` |
| مناسب برای | کار روزانه در IDE | automation، WSL، script |

هر دو از همان `AGENTS.md` و skills پروژه Financial-Core استفاده می‌کنن.

---

*آخرین به‌روزرسانی: ژوئن ۲۰۲۶ · Financial-Core*
