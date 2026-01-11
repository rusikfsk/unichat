export function relativeTimeRu(iso: string) {
  const t = new Date(iso).getTime()
  const now = Date.now()
  const diffSec = Math.max(0, Math.floor((now - t) / 1000))

  if (diffSec < 10) return "только что"

  const min = Math.floor(diffSec / 60)
  if (min < 60) return `${min} ${pluralRu(min, "минуту", "минуты", "минут")} назад`

  const h = Math.floor(min / 60)
  if (h < 24) return `${h} ${pluralRu(h, "час", "часа", "часов")} назад`

  const d = Math.floor(h / 24)
  if (d < 7) return `${d} ${pluralRu(d, "день", "дня", "дней")} назад`

  return new Date(iso).toLocaleString()
}

function pluralRu(n: number, one: string, few: string, many: string) {
  const mod10 = n % 10
  const mod100 = n % 100
  if (mod10 === 1 && mod100 !== 11) return one
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few
  return many
}
