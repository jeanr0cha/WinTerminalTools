#!/usr/bin/env bash
# resolve-context-dir.sh — diz ONDE mora o contexto de uma dimensão (business, technical, compliance, design).
#
# Ordem (a primeira que responder vence):
#   1. chave `context_<dim>:` no topo de .agents/workspace.yaml (ex.: `context_technical: docs/tecnico`);
#   2. o padrão `docs/<dim>-context`.
#
# Uso  : resolve-context-dir.sh <business|technical|compliance|design> [--source]
# Saída: caminho relativo à raiz do workspace (com --source: "<caminho><TAB><workspace|default>").
# Exit : 0 ok · 2 uso · 3 declaração inválida (absoluta ou com ..). Só lê; não cria a pasta.
# Adaptado do resolve-context-dir.sh do Onion (MIT, Marcio Carvalho).
set -uo pipefail
DIM=""; SRC=0
for a in "$@"; do case "$a" in --source) SRC=1 ;; -*) echo "uso: $0 <dim> [--source]" >&2; exit 2 ;; *) DIM="$a" ;; esac; done
case "$DIM" in business|technical|compliance|design) : ;; *) echo "uso: $0 <business|technical|compliance|design> [--source]" >&2; exit 2 ;; esac
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
YAML="$ROOT/.agents/workspace.yaml"
out() { if [ "$SRC" -eq 1 ]; then printf '%s\t%s\n' "$1" "$2"; else printf '%s\n' "$1"; fi; exit 0; }
if [ -f "$YAML" ]; then
  v="$(awk -v k="context_${DIM}" '$0 ~ "^"k":" { sub(/^[^:]*:[ \t]*/, ""); sub(/[ \t]+#.*$/, ""); gsub(/["'\'']/, ""); print; exit }' "$YAML")"
  v="${v%/}"
  if [ -n "$v" ]; then
    case "/$v/" in /*/../*|//*) echo "ERRO: context_${DIM} = '$v' precisa ser relativo e sem '..'" >&2; exit 3 ;; esac
    out "$v" workspace
  fi
fi
out "docs/${DIM}-context" default
