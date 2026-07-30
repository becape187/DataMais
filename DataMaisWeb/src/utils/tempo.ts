/**
 * Formata uma duração em segundos no padrão hh:mm:ss (ex.: 01:23:45).
 * As horas não estouram em 24 — um ensaio de 30 horas vira 30:00:00.
 */
export function formatarDuracao(totalSegundos: number): string {
  const segundosTotais = Number.isFinite(totalSegundos) && totalSegundos > 0
    ? Math.floor(totalSegundos)
    : 0

  const horas = Math.floor(segundosTotais / 3600)
  const minutos = Math.floor((segundosTotais % 3600) / 60)
  const segundos = segundosTotais % 60

  const doisDigitos = (valor: number) => valor.toString().padStart(2, '0')

  return `${doisDigitos(horas)}:${doisDigitos(minutos)}:${doisDigitos(segundos)}`
}
