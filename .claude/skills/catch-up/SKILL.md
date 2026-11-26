---
name: catch-up
description: |
  Reconstrói "onde paramos" depois de uma pausa, de uma sessão que caiu ou de uma compactação de
  contexto, olhando todos os repos do workspace. Use quando a pessoa disser "onde paramos?",
  "o que eu estava fazendo?", "continua de onde parou". Só lê e orienta; não executa nada.
---
<!-- GERADO pelo Liz Framework a partir de .agents/ — não edite aqui; edite a fonte e rode update.sh -->

# 🔁 Catch-up — onde paramos

Leia na ordem, do barato para o caro, e pare quando já souber o suficiente. Nunca faça `cat` de
pastas inteiras.

## 1. Sessão formal aberta (fonte autoritativa)

- `find .agents/sessions -maxdepth 2 -name STATE.md -not -path '*/archived/*'`
- Para a mais recente, leia **só o `STATE.md`**: o bloco `## NEXT` é o próximo passo.
- Existe? Então a retomada é `/engineer-work <slug>`. O catch-up só aponta.

## 2. Git de cada repo

```bash
bash .agents/utils/workspace/ws.sh status      # branch e alterações de cada repo
bash .agents/utils/workspace/ws.sh each "git log --oneline -8"
```

- Trabalho não commitado é o sinal mais forte de "onde parei": `ws.sh each "git diff --stat"`.
- Branch `feature/*`, `fix/*` ou `hotfix/*` indica uma sessão (passo 1).

## 3. Docs e pendências

- `docs/PENDENCIAS.md` e `docs/specs/` (só os arquivos alterados recentemente).
- `git -C . log --oneline -5` na raiz do workspace, se ela for um repo.

## 4. Formato da resposta

```
🔁 Catch-up — <projeto>

📍 Última atividade: <tema, por repo>
🌳 Alterações pendentes: <repo: N arquivos | tudo limpo>
🗂️ Sessão aberta: <slug + próximo passo do STATE.md | nenhuma>
📌 Pendências: <PRs abertos, diff não commitado, itens de PENDENCIAS>

▶️ Próximo passo provável: <um comando>
```

Não execute nada automaticamente: o briefing orienta, a pessoa decide. Para conhecer o projeto
(e não "onde parei"), use `/warm-up`. Mecânica de retomada: `.agents/knowledge/worklog-protocol.md`.
