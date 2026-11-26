---
name: engineer-spike
description: Spike ou PoC antes de construir. Use quando houver dúvida técnica que trava o desenho ("isso é possível?", "a lib aguenta?", "funciona no nosso contexto?"): uma pergunta fechada, critério de morte escrito antes de testar, tempo-caixa e pasta descartável.
---

# 🧪 Engineer Spike — responder uma pergunta antes de construir

<arguments>
{argumentos do usuário}
</arguments>

Um spike responde **uma pergunta fechada** ("isto é possível? como?") dentro de um **tempo-caixa**, e é
**descartável**: o código do spike morre e a lição fica registrada. Uma PoC é o mesmo molde com outra
pergunta ("funciona ponta a ponta no nosso contexto?"); só muda o tamanho da caixa. Nenhum dos dois vira
produto sem passar pelo `/engineer-plan`.

Onde entra no caminho: **antes** do `/engineer-plan`, quando a incerteza é alta. Costuma nascer na seção
Arquitetura do `/engineer-start` ou de uma dúvida aberta do `@software-architect`.

## Quando NÃO é spike

- **"Como o mercado faz isso?"** é pesquisa, não spike. Pesquise e cite as fontes.
- **"Qual das duas opções é melhor?"** sem nada a testar é decisão de desenho: vá ao `@software-architect`.
- **Já se sabe a resposta** e falta só fazer: vá direto ao `/engineer-plan`.

## Procedimento

1. **Ache a sessão.** Se já há `.agents/sessions/<feature-slug>/`, a ficha vai para
   `.agents/sessions/<feature-slug>/spike-<slug>.md`. Sem sessão, use `.agents/sessions/spike-<slug>/spike.md`.
2. **Feche a pergunta.** Uma só, com resposta sim/não ou "como". Saíram duas? São dois spikes.
3. **Preencha a ficha** (modelo abaixo) com o humano: critério de morte, critério de sucesso, tempo-caixa,
   o que não toca e como desfazer.
4. **Registre o critério ANTES de testar.** Se a pasta da sessão é versionada, faça o commit da ficha antes
   de colher o resultado. Se não é, mostre a ficha ao humano e peça o "ok" antes de rodar. Trocar o critério
   depois de ver o resultado é mover a régua.
5. **Rode dentro da caixa**, numa pasta descartável fora do produto (ex.: um diretório temporário ou um
   branch `spike/<slug>` que nunca vai para PR). Não toque no que a ficha diz que não toca: produção,
   dados reais, outros repos do workspace.
6. **Estourou o tempo?** "Não respondeu no tempo" também é resultado. Registre e decida com o humano:
   matar, ou abrir um spike **novo** com pergunta menor. Nunca estique a caixa em silêncio.
7. **Registre o resultado** na ficha: o que mediu, com o comando ou o trecho que prova.
8. **Decida o próximo degrau:**
   - `discard` — o critério de morte disparou: apague a pasta, a lição fica na ficha.
   - `poc` — deu certo no pequeno, falta provar ponta a ponta: abra um spike novo com pergunta de PoC.
   - `slice` — respondido: siga para o `/engineer-plan` citando a ficha. Se a resposta mudou o desenho,
     atualize o `architecture.md` da sessão; se é decisão cara de desfazer, proponha um ADR
     (ver `.agents/knowledge/c4-adr-patterns.md`).
9. **Desfaça.** Siga o "como desfazer" da ficha e confirme que não sobrou nada no produto.

## A ficha do spike

```markdown
# Spike: <slug>

- **Pergunta fechada:** <uma só, com resposta sim/não ou "como">
- **Destrava:** <feature ou decisão que depende da resposta>
- **Critério de morte** (escrito antes de testar): <morre se ...>
- **Critério de sucesso:** <passa se ...>
- **Tempo-caixa:** <horas, não dias> · **prazo:** <AAAA-MM-DD>
- **O que prova:** <o que o resultado mostra, e o que NÃO mostra>
- **Não toca:** <produção, dados reais, repos e serviços que ficam fora>
- **Pasta descartável:** <onde mora o código do spike, fora do produto>
- **Como desfazer:** <passos para apagar sem deixar resto>
- **Critério registrado em:** <sha do commit ou "ok do humano em AAAA-MM-DD">

## Resultado (depois de testar)

- **Respondeu?** sim | não | não no tempo
- **Evidência:** <o que foi medido, com o comando ou o trecho>
- **Custo:** <tempo gasto; tokens e agentes, se houve>
- **Próximo degrau:** discard | poc | slice, e por quê
```

## O que esta skill NÃO faz

- **Não pesquisa mercado.** Pergunta de mercado é pesquisa.
- **Não promove.** Spike que deu certo não é feature pronta: a construção passa pelo `/engineer-plan`.
- **Não guarda o código do spike** no produto. A pasta é descartável; o que fica é a ficha.

---

*Adaptado do `/engineer:spike` do Onion (Marcio Carvalho). Ver `NOTICE.md`.*
