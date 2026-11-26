---
name: warm-up
description: |
  Carrega o contexto do workspace antes de trabalhar: regras (AGENTS.md), repos do workspace
  (backend, frontend, MCP...), documentação em docs/ e sessões abertas. Use no início de uma
  sessão nova ou quando a IA ainda não conhece o projeto. Na primeira vez, também preenche o
  AGENTS.md e o .agents/workspace.yaml a partir do que encontrar.
---

# 🔥 Warm-up do workspace

Objetivo: em poucos minutos, saber **o que é o projeto, quais repos existem, como cada um roda e o
que está em andamento**, sem despejar pastas inteiras no contexto.

## 1. Regras e mapa do workspace

1. Leia o `AGENTS.md` da raiz (regras gerais do projeto).
2. Leia `.agents/workspace.yaml` e rode `bash .agents/utils/workspace/ws.sh status`.
   - Mostra cada repo (nome, papel, branch atual, alterações pendentes).
   - Repo listado que não existe no disco → avise; pasta com `.git` que não está listada → sugira incluir.
3. Para cada repo, leia o `AGENTS.md` dele (se existir) e o `README.md`. Do `package.json` (ou
   equivalente) anote só: stack, scripts de dev/test/build/lint. **Não** leia `node_modules`, `dist`,
   `.next`, `out`.

## 2. Documentação

- Liste `docs/` (só nomes) e leia `docs/INDEX.md` ou `docs/README.md` se existir.
- Leia o que estiver em `docs/business-context/` e `docs/technical-context/` **só os índices** e o
  que for relevante para o pedido atual.
- Leia `docs/PENDENCIAS.md` (ou equivalente) se existir: é a lista do que falta.

## 3. Trabalho em andamento

- `ls .agents/sessions/` → para cada sessão viva, leia **só o `STATE.md`** (bloco `## NEXT`).
- Em cada repo: `git log --oneline -5` para ver o fio recente.

## 4. Primeira vez no workspace (bootstrap)

Se o `AGENTS.md` ainda tem os marcadores `<!-- PREENCHER -->` ou o `workspace.yaml` está vazio:

1. Monte o `workspace.yaml` com os repos encontrados (nome, path, papel, stack, comandos de
   test/build/lint tirados do `package.json`). Mostre o diff e peça ok antes de salvar.
2. Preencha as seções marcadas do `AGENTS.md` (o que é o projeto, repos, como rodar, convenções)
   com o que você **leu** — nada inventado; o que não souber, deixe como pergunta.
3. Se não existir `docs/business-context/` ou `docs/technical-context/`, recomende
   `/docs-build-business-docs` e `/docs-build-tech-docs` como próximos passos.

## 5. Resumo para a pessoa

Responda num bloco curto:

```
🔥 Warm-up — <projeto>

📦 Repos: <nome (papel) @ branch · N alterações> por linha
📚 Docs: <o que existe | o que falta>
🗂️ Sessões: <slug → próximo passo | nenhuma>
📌 Pendências principais: <3 itens no máximo>

▶️ Próximo passo: <um comando>
```

Não mude código nem faça commit no warm-up. Ele só lê (exceto o bootstrap do item 4, com ok).
