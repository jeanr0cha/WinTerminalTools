---
title: Estimativa de consumo de tokens
category: concepts
status: reference
date: "2026-11-26"
---

# Estimativa de consumo de tokens

> Como o Liz estima quanto de IA uma tarefa consome, por que mostra **faixas** e como o histórico
> deixa a estimativa melhor. Ferramenta: `.agents/utils/tokens/tokens.sh`. Usada por `/product-feature`,
> `/engineer-plan`, `/engineer-work`, `/engineer-pre-pr` e `/engineer-pr`.

## 1. Por que faixa, e não um número

O consumo depende de coisas que ninguém sabe de antemão: quantas correções a tarefa pede, o tamanho da
conversa até ali, quantos arquivos a IA precisa ler. Um número único seria falsa precisão. O Liz mostra
**mínimo e máximo** e diz quando a faixa ainda é uma hipótese (`não calibrado`).

## 2. Entrada, saída e cache

| | O que é | Peso |
|---|---|---|
| **Saída** | o que a IA escreve (código, textos) | é a parte cara; mede o "trabalho" |
| **Entrada** | tudo o que a IA lê a cada passo: o pedido, os arquivos e **a conversa inteira até ali** | cresce com o tamanho da sessão |
| **Cache** | a parte da entrada que é releitura do mesmo contexto | custa bem menos que a entrada nova |

A faixa de **entrada** é larga de propósito: a maior parte dela é releitura do contexto, que se repete a
cada passo e é barata. Por isso o `tokens.sh` mostra a entrada como "inclui releitura em cache". Quem
quiser separar, pode registrar o cache real com `--cache` ao informar o consumo.

## 3. Tamanho P / M / G

Pela **saída máxima** da faixa: **P** abaixo de 15 mil tokens, **M** de 15 mil a 60 mil, **G** acima de
60 mil. Uma tarefa G de 13 pontos ou mais deve ser quebrada em fases ou histórias menores.

## 4. A tabela de hipóteses

Vale **até haver histórico**. São hipóteses, não medições: quem as definiu estimou a ordem de grandeza,
e a calibração existe justamente para trocá-las pela realidade de cada pessoa.

| Story points | Entrada (tokens) | Saída (tokens) |
|---|---|---|
| 1 | 50 mil – 150 mil | 3 mil – 8 mil |
| 2 | 100 mil – 300 mil | 6 mil – 15 mil |
| 3 | 200 mil – 500 mil | 10 mil – 25 mil |
| 5 | 400 mil – 1 milhão | 20 mil – 50 mil |
| 8 | 800 mil – 2 milhões | 40 mil – 90 mil |
| 13 | 1,5 milhão – 4 milhões | 70 mil – 150 mil |
| 20 ou mais | não estima: é épico, quebre em histórias menores | |

Pontos fora da escala (4, 6, 7, 9...) usam o degrau acima; de 14 a 19 usa o 13, com aviso.

**Ajustes** (só na tabela de hipóteses): cada repo além do primeiro soma 25% (no máximo o dobro);
complexidade alta (tecnologia nova, requisito nebuloso, integração externa) multiplica por 1,3;
complexidade baixa (padrão que o projeto já usa, mudança mecânica) por 0,75.

## 5. Calibração

1. Cada fase concluída pode ganhar o seu **consumo real** (`tokens.sh actual`).
2. Com **5 registros ou mais** da mesma ferramenta, o Liz calcula o consumo real por story point e usa o
   1º e o 3º quartil como faixa (a mediana vira o "típico"). A faixa nunca fica mais estreita que ±15%.
3. Entram os **20 registros mais recentes**. Uma tarefa fora do normal pode ser excluída gravando a nota
   começando por `outlier` (ex.: `--note "outlier: migração enorme"`).
4. A calibração é **por ferramenta** (`claude`, `codex`, `antigravity`): o mesmo trabalho gasta
   quantidades diferentes em cada uma.

## 6. Como registrar o consumo real

O registro é **opcional e manual**: a ferramenta em que você trabalha mostra o uso da sessão (no Claude
Code, por exemplo, o comando de uso/custo; confira o nome na sua versão). Pegue os tokens de entrada e de
saída do trecho que corresponde à fase e rode:

```bash
bash .agents/utils/tokens/tokens.sh actual --slug <tarefa> --phase <N> --in 450k --out 22k
```

Se o `/engineer-work` perguntar ao fim de uma fase, é isso que ele pede. Pode responder "pular": nada é
bloqueado por falta de registro.

## 7. O que isto NÃO faz

- **Não converte em % do limite semanal ou da janela de horas.** Os planos não publicam o limite em
  tokens, ele muda por plano, modelo e horário, e qualquer porcentagem seria chute com cara de precisão.
- **Não converte em dinheiro.** Os preços mudam; consulte a tabela do seu provedor.
- **Não promete**: é uma faixa, sempre com a confiança dita (`não calibrado` ou `calibrado com N`).
- **Não lê o conteúdo das suas conversas.** Só guarda os números que você informa.
- **Não é igual em todas as ferramentas.** Por isso a calibração é separada por ferramenta.

## 8. Onde fica o histórico

`.agents/token-history.tsv`, na raiz do workspace. É dado do projeto: o `update.sh` não o toca. Decida
com o resto do `.agents/sessions/` se ele entra no controle de versão ou fica só na sua máquina.
Formato das colunas em `.agents/utils/tokens/README.md`.
