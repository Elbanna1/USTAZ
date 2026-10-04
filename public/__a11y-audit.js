/* Dev-only contrast auditor. Loaded manually from the console during QA. */
window.__contrast = () => {
  const srgb = (c) => { c /= 255; return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4) }
  const lum = (a) => 0.2126 * srgb(a[0]) + 0.7152 * srgb(a[1]) + 0.0722 * srgb(a[2])
  const parse = (s) => { const m = s.match(/[\d.]+/g); return m ? m.slice(0, 4).map(Number) : null }
  const over = (fg, bg) => { const a = fg[3] === undefined ? 1 : fg[3]; return [0,1,2].map(i => fg[i] * a + bg[i] * (1 - a)) }
  function bgOf(el) {
    let node = el, acc = null
    while (node) {
      const cs = getComputedStyle(node)
      if (cs.backgroundImage && cs.backgroundImage !== 'none') return null
      const c = parse(cs.backgroundColor)
      if (c) { const a = c[3] === undefined ? 1 : c[3]
        if (a > 0) { acc = acc ? over(acc.concat(1), c) : c; if (a === 1) return acc.slice(0,3) } }
      node = node.parentElement
    }
    return (acc || [255,255,255]).slice(0,3)
  }
  const ratio = (a, b) => { const l = [lum(a), lum(b)].sort((x,y)=>y-x); return (l[0] + 0.05) / (l[1] + 0.05) }
  const out = []
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
  const seen = new Set()
  let n
  while ((n = walker.nextNode())) {
    const text = n.nodeValue.trim(); if (!text) continue
    const el = n.parentElement; if (!el || seen.has(el)) continue
    seen.add(el)
    const cs = getComputedStyle(el)
    if (cs.visibility === 'hidden' || cs.display === 'none' || parseFloat(cs.opacity) < 0.15) continue
    if (cs.webkitTextFillColor === 'rgba(0, 0, 0, 0)' || cs.color === 'rgba(0, 0, 0, 0)') continue
    if (el.closest('svg')) continue
    const r = el.getBoundingClientRect(); if (!r.width || !r.height) continue
    const fg = parse(cs.color); if (!fg) continue
    const bg = bgOf(el); if (!bg) continue
    const fa = fg[3] === undefined ? 1 : fg[3]
    const painted = fa < 1 ? over(fg, bg) : fg.slice(0,3)
    const size = parseFloat(cs.fontSize)
    const large = size >= 24 || (size >= 18.66 && parseInt(cs.fontWeight,10) >= 700)
    const need = large ? 3 : 4.5
    const cr = ratio(painted, bg)
    if (cr < need) out.push({ t: text.slice(0, 26), r: +cr.toFixed(2), px: Math.round(size), cls: el.className.toString().slice(0, 64) })
  }
  const uniq = new Map()
  for (const o of out) { const k = o.cls + '|' + o.r; if (!uniq.has(k)) uniq.set(k, o) }
  return [...uniq.values()].sort((a,b)=>a.r-b.r).slice(0, 10)
}
/* Overflow + dead-control checks, same session. */
window.__layout = () => ({
  overflowX: document.documentElement.scrollWidth - window.innerWidth,
  wideNodes: [...document.querySelectorAll('body *')]
    .filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && (r.right > window.innerWidth + 2 || r.left < -2) })
    .slice(0, 6).map(e => ({ cls: e.className.toString().slice(0, 60), right: Math.round(e.getBoundingClientRect().right) })),
  deadControls: [...document.querySelectorAll('button, a')]
    .filter(e => {
      if (e.tagName === 'A') return !e.getAttribute('href')
      return !e.onclick && !e.getAttribute('type') && !e.closest('form') && e.disabled === false &&
        !e.hasAttribute('data-state') && !e.hasAttribute('aria-haspopup') && !e.hasAttribute('aria-expanded')
    })
    .slice(0, 8).map(e => ({ tag: e.tagName, text: (e.textContent || e.getAttribute('aria-label') || '').trim().slice(0, 30) })),
})
console.log('[audit] ready')
