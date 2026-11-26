---
name: liz
description: >
  Guia do Liz Framework: diz por onde começar e qual é o próximo passo no caminho de produto e
  desenvolvimento (ideia → tarefa → código → Pull Request), em qualquer repo do workspace. Use quando
  a pessoa perguntar "por onde começo?", "o que faço agora?", "qual o próximo passo?", "como
  funciona?", "qual comando uso para X?", mesmo sem dizer "liz". Ative também quando a mensagem for
  só a palavra "liz" ou "Liz". NÃO ative por menções a "liz" dentro de frases (nomes de arquivo,
  código, o mascote, o repo liz-mcp).
---
<!-- GERADO pelo Liz Framework a partir de .agents/ — não edite aqui; edite a fonte e rode update.sh -->

# 🦊 Liz — orientação

## 1. Veja onde a pessoa está (rode, não pergunte)

```bash
bash .agents/utils/workspace/ws.sh status        # repos do workspace, branch e alterações de cada um
ls .agents/sessions/ 2>/dev/null                 # sessões de trabalho abertas
bash .agents/utils/task-manager/env-check.sh --provider 2>/dev/null || echo none   # gerenciador de tarefas
```

Nunca abra nem imprima o `.env`: ele guarda chaves. O provedor vem do comando acima.

## 2. Como responder

Responda em português simples, em poucas linhas, e termine sempre com **o próximo comando**.
Uma recomendação só, não um cardápio.

- **Workspace sem `docs/` ou sem `AGENTS.md` preenchido** → `/warm-up`.
- **Nenhuma sessão aberta e nada combinado** → `/product-collect`.
- **Tem uma ideia, mas ainda sem tarefa** → `/product-feature` (ou `/product-refine` antes, se tem dúvida).
- **Tem tarefa e ainda não começou** → `/engineer-start`.
- **Sessão aberta, mas uma dúvida técnica trava o plano** → `/engineer-spike`.
- **Tem sessão aberta** → `/engineer-work` (ou `/catch-up` se a pessoa não lembra onde parou).
- **Terminou as fases** → `/engineer-pre-pr` e depois `/engineer-pr`.

## 3. O caminho completo

```
/warm-up → /product-collect → /product-refine → /product-spec → /product-feature
         → /engineer-start → (/engineer-spike) → /engineer-plan → /engineer-work → /engineer-pre-pr → /engineer-pr
```

| Passo | Comando | Para quê |
|---|---|---|
| Conhecer o workspace | `/warm-up` | lê os repos e os `docs/` antes de mexer |
| Documentar o negócio | `/docs-build-business-docs` | gera `docs/business-context/` |
| Documentar a técnica | `/docs-build-tech-docs` | gera `docs/technical-context/` |
| Anotar a ideia | `/product-collect` | registrar funcionalidade nova ou bug |
| Tirar as dúvidas | `/product-refine` | perguntas até a ideia ficar clara (opcional) |
| Escrever o que será feito | `/product-spec` | especificação curta (opcional) |
| Criar a tarefa | `/product-feature` | a ideia vira tarefa pronta, já dizendo em quais repos mexe |
| Começar | `/engineer-start` | cria a branch em cada repo afetado, a sessão e o desenho da mudança |
| Tirar uma dúvida técnica | `/engineer-spike` | teste curto e descartável antes de planejar (opcional) |
| Planejar | `/engineer-plan` | divide em fases pequenas, cada uma num repo |
| Trabalhar | `/engineer-work` | uma fase por vez, com aprovação |
| Revisar | `/engineer-pre-pr` | revisão e testes antes do PR |
| Entregar | `/engineer-pr` | um Pull Request por repo afetado |
| Voltar de uma pausa | `/catch-up` | reconstrói onde vocês pararam |

No Codex os comandos são chamados com `$` (ex.: `$engineer-start`); no Claude Code e no
Antigravity IDE, com `/`. No Antigravity CLI (`agy`), peça pelo nome: "rode a skill warm-up".

## 4. Quem ajuda

| Precisa de | Chame |
|---|---|
| Organizar e priorizar ideias | `@product-agent` |
| Quebrar uma tarefa grande | `@task-specialist` |
| Desenhar a arquitetura ou decidir entre abordagens | `@software-architect` |
| Revisar o código | `@code-reviewer` |
| Escrever testes | `@test-engineer` |

## 5. Gerenciador de tarefas

Sem configuração, tudo funciona offline: as tarefas ficam em `docs/specs/` e nas sessões. Para
usar Jira, ClickUp, Asana, Linear ou Zoho Projects, a pessoa põe `TASK_MANAGER_PROVIDER` e as
variáveis do gerenciador num `.env` na raiz do workspace (lista em
`.agents/utils/task-manager/README.md`).
