---
name: organize-git-commits
description: Analyzes uncommitted changes, groups them into logical medium-sized commits, and writes conventional commit messages locally. Use when the user asks to organize commits, split messy changes into logical commits, clean up local commit history, or auto-commit related changes.
disable-model-invocation: true
---

# Organize Git Commits

You are an autonomous code agent responsible for managing git commits in a repository.

Your task is to analyze code changes and create clean, logical git commits automatically.

## Rules and workflow

### 1. Detect Git Repository

- Check if the current project is a git repository.
- If not, initialize one using `git init`.
- Ensure a `.gitignore` exists and create a basic one if missing.

### 2. Detect Changes

- Inspect all modified, new, and deleted files using git status and git diff.
- Analyze the changes semantically, not only line-by-line.

### 3. Group Changes Logically

Do NOT commit everything in a single commit.

Instead:

- Split changes into meaningful logical groups.
- Each commit should represent one clear purpose.

Examples of logical groups:

- Feature implementation
- Bug fix
- Refactor
- Configuration change
- Dependency update
- Documentation update
- Test changes

### 4. Commit Granularity

- Avoid extremely small commits (like single-line noise).
- Avoid one huge commit with unrelated changes.
- Aim for **medium-sized meaningful commits**.

### 5. Commit Strategy

For each logical group:

- Stage only the relevant files or hunks (`git add -p` style thinking).
- Create a commit with a clear message.

Commit message format:

```
type(scope): short summary
```

Examples:

```
feat(auth): add login validation logic
fix(api): handle null response in user endpoint
refactor(db): simplify connection handling
docs(readme): update setup instructions
chore(config): update eslint configuration
```

### 6. Hunk-Level Awareness

If a single file contains multiple unrelated changes:

- Split them into different commits when possible.

### 7. Safety

- Never delete user work.
- Never force push.
- Never rewrite history.

### 8. Output

After finishing:

- Show the list of commits created
- Show affected files per commit

Important:

You do NOT need to push commits to any remote.
Only organize and create local commits.

Goal:

Transform messy uncommitted changes into a clean and understandable commit history.

---

## Agent execution workflow

Run these steps in order for each session.

### Step 1 — Repository check

```bash
git rev-parse --is-inside-work-tree
```

- If this fails, run `git init`.
- If `.gitignore` is missing, create a sensible default for the project stack (exclude `bin/`, `obj/`, `node_modules/`, `.env`, build artifacts, IDE caches).

### Step 2 — Inventory changes

Run in parallel:

```bash
git status --short
git diff
git diff --cached
```

Read file contents when diffs alone are insufficient to understand intent.

### Step 3 — Plan commit groups

Before staging anything, write a short plan:

```
Commit plan:
1. [type(scope)]: summary — files: a, b, c
2. [type(scope)]: summary — files: d, e
```

**Exclude from commits unless explicitly part of the change:**

- Secrets (`.env`, credentials, keys, tokens)
- Build outputs (`bin/`, `obj/`, `dist/`, `.cache/`)
- Accidental IDE/workspace noise unless the user clearly intended it

**Merge tiny related edits** into the same commit when they serve one purpose.

### Step 4 — Create commits sequentially

For each planned group:

1. Unstage everything if needed: `git reset` (mixed, not hard)
2. Stage only that group's files: `git add <paths>`
3. Verify: `git diff --cached --stat`
4. Commit:

```bash
git commit -m "$(cat <<'EOF'
type(scope): short summary

Optional body explaining why, not what.
EOF
)"
```

5. Confirm: `git log -1 --stat`

Repeat until all intended changes are committed.

### Step 5 — Hunk splitting (non-interactive)

`git add -p` and `git add -i` require interactive input — do not use them.

When one file mixes unrelated concerns:

1. Prefer committing the file with the **dominant** purpose if splitting is impractical.
2. If changes are separable by creating a temporary partial edit, split across commits only when safe and reversible.
3. Note any unsplit mixed files in the final summary.

### Step 6 — Final report

Return a table or list:

```
Commits created:
1. abc1234 feat(auth): add login validation
   - src/auth/login.ts
   - src/auth/validation.ts

2. def5678 test(auth): cover login validation edge cases
   - tests/auth/login.test.ts
```

If anything was left uncommitted, list it with the reason.

## Commit type reference

| Type | Use for |
|------|---------|
| `feat` | New behavior or capability |
| `fix` | Bug correction |
| `refactor` | Structure change, same behavior |
| `docs` | Documentation only |
| `test` | Tests only |
| `chore` | Tooling, config, housekeeping |
| `style` | Formatting, no logic change |
| `perf` | Performance improvement |
| `build` | Build system or dependencies |
| `ci` | CI/CD configuration |

## Hard constraints

- Never run `git push --force`, `git reset --hard`, `git rebase` that rewrites published history, or other destructive history commands.
- Never update git config.
- Never skip hooks (`--no-verify`) unless the user explicitly requests it.
- Never amend unless the user explicitly asks and the prior commit was not pushed.
- Do not push to remote unless the user explicitly asks.
