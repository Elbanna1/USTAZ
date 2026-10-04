import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const SWAGGER_URL =
  process.env.USTAZ_SWAGGER_URL ?? 'https://ustaz.runasp.net/swagger/v1/swagger.json'
const OUT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'api',
  'schema.ts',
)

const res = await fetch(SWAGGER_URL)
if (!res.ok) throw new Error(`Could not read ${SWAGGER_URL} — ${res.status}`)
const sw = await res.json()
const schemas = sw.components.schemas

const refName = (r) => r.split('/').pop()
const tsName = (n) => n.replace(/[^A-Za-z0-9_]/g,'_')

// Skip ApiResponseOf* / PaginatedListOf* wrappers — the client unwraps them.
const skip = (n) => /^ApiResponse/.test(n) || /^PaginatedListOf/.test(n)

function typeOf(s, depth = 0) {
  if (!s) return 'unknown'
  if (s.$ref) {
    const n = refName(s.$ref)
    if (/^PaginatedListOf(.+)$/.test(n)) return `Paginated<${tsName(RegExp.$1)}>`
    if (/^ApiResponseOf(.+)$/.test(n)) return typeOfWrapperInner(RegExp.$1)
    if (n === 'ApiResponse') return 'void'
    return tsName(n)
  }
  if (s.enum) return s.enum.map((e) => (typeof e === 'string' ? `'${e}'` : e)).join(' | ')
  switch (s.type) {
    case 'string': return s.format === 'binary' ? 'File' : 'string'
    case 'integer':
    case 'number': return 'number'
    case 'boolean': return 'boolean'
    case 'array': return `${typeOf(s.items, depth + 1)}[]`
    case 'object': {
      if (s.additionalProperties && typeof s.additionalProperties === 'object')
        return `Record<string, ${typeOf(s.additionalProperties, depth + 1)}>`
      if (s.properties) return objBody(s, depth)
      return 'Record<string, unknown>'
    }
    default:
      if (s.properties) return objBody(s, depth)
      return 'unknown'
  }
}

function typeOfWrapperInner(inner) {
  if (/^IReadOnlyListOf(.+)$/.test(inner)) return `${tsName(RegExp.$1)}[]`
  if (/^PaginatedListOf(.+)$/.test(inner)) return `Paginated<${tsName(RegExp.$1)}>`
  if (inner === 'Int32') return 'number'
  if (inner === 'String') return 'string'
  if (inner === 'Boolean') return 'boolean'
  return tsName(inner)
}

function objBody(s, depth) {
  const req = new Set(s.required || [])
  const pad = '  '.repeat(depth)
  const lines = Object.entries(s.properties || {}).map(([k, v]) => {
    const opt = req.has(k) ? '' : '?'
    const nullable = v.nullable ? ' | null' : ''
    return `${pad}  ${/^[A-Za-z_$][A-Za-z0-9_$]*$/.test(k) ? k : `'${k}'`}${opt}: ${typeOf(v, depth + 1)}${nullable}`
  })
  return `{\n${lines.join('\n')}\n${pad}}`
}

const header = `/* eslint-disable */
/**
 * AUTO-GENERATED from the USTAZ Production OpenAPI document.
 * Source: https://ustaz.runasp.net/swagger/v1/swagger.json
 *
 * Do not edit by hand — regenerate with \`scripts/generate-api-types.mjs\`.
 */

/** Envelope every successful USTAZ response is wrapped in. */
export interface ApiEnvelope<T> {
  success: boolean
  message?: string
  data: T
}

/** Server-side paging envelope used by every list endpoint. */
export interface Paginated<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

/** RFC 7807 problem document returned for every failure. */
export interface ProblemDocument {
  type?: string | null
  title?: string | null
  status?: number | null
  detail?: string | null
  instance?: string | null
  traceId?: string
  /**
   * Machine-readable business-rule code, e.g. FEATURE_NOT_AVAILABLE or
   * TEACHER_NOT_VERIFIED. Present on 422 responses. Swagger does not declare it
   * on ProblemDetails, but the API emits it and the client keys its plan and
   * verification gates off it — so it lives in this template rather than being
   * re-patched by hand after every regeneration.
   */
  code?: string
  errors?: Record<string, string[]>
  success?: boolean
}
`

const out = [header]
for (const [name, s] of Object.entries(schemas)) {
  if (skip(name)) continue
  if (name === 'ProblemDetails') continue
  if (s.enum) {
    out.push(`export type ${tsName(name)} = ${s.enum.map((e) => `'${e}'`).join(' | ')}\n`)
    continue
  }
  const body = typeOf(s, 0)
  if (body.startsWith('{')) out.push(`export interface ${tsName(name)} ${body}\n`)
  else out.push(`export type ${tsName(name)} = ${body}\n`)
}

// Write to the resolved OUT path. A bare 'schema.ts' lands in whatever
// directory the script is run from, silently stranding the output while
// still reporting success.
fs.writeFileSync(OUT, out.join('\n'))
console.log(`Wrote ${out.length - 1} types to ${path.relative(process.cwd(), OUT)}`)
