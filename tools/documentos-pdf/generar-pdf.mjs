// Genera los documentos del proyecto en PDF (carpeta docs/) a partir de sus fuentes en Markdown
// (carpeta fuentes/). Les da el formato institucional de la Fundación y los imprime con Chrome o Edge.
//
// Uso (desde esta carpeta):
//   npm install
//   node generar-pdf.mjs                 # todos los documentos
//   node generar-pdf.mjs manuales        # solo los que estén en fuentes/manuales
// El navegador se busca en las rutas habituales de Windows, macOS y Linux, o en la variable NAVEGADOR.

import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { marked } from 'marked';

const AQUI = dirname(fileURLToPath(import.meta.url));
const FUENTES = join(AQUI, 'fuentes');
const DESTINO = resolve(AQUI, '..', '..', 'docs');
const filtro = process.argv[2] ?? '';

const NAVEGADORES = [
  process.env.NAVEGADOR,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
].filter(Boolean);
const navegador = NAVEGADORES.find(existsSync);
if (!navegador) { console.error('No se encontró Chrome ni Edge. Indica la ruta en la variable NAVEGADOR.'); process.exit(1); }

const fuentes = (dir) => readdirSync(dir).flatMap((n) => {
  const ruta = join(dir, n);
  return statSync(ruta).isDirectory() ? fuentes(ruta) : n.endsWith('.md') ? [ruta] : [];
});

const escapar = (s) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

const ESTILOS = `
@page { size: A4; margin: 22mm 18mm 20mm 18mm;
  @bottom-left { content: "Fundación Unión de los Jaguares"; font: 8pt 'Segoe UI', Arial, sans-serif; color: #6b7280; }
  @bottom-right { content: "Página " counter(page) " de " counter(pages); font: 8pt 'Segoe UI', Arial, sans-serif; color: #6b7280; } }
* { box-sizing: border-box; }
body { font: 10.5pt/1.55 'Segoe UI', Calibri, Arial, sans-serif; color: #1f2937; margin: 0; }
.encabezado { display: flex; align-items: center; justify-content: space-between; border-bottom: 3px solid #f59e0b;
  padding-bottom: 8px; margin-bottom: 18px; }
.marca { display: flex; align-items: center; gap: 10px; }
.logo { width: 34px; height: 34px; border-radius: 50%; background: #b45309; color: #fff; font-weight: 700;
  display: flex; align-items: center; justify-content: center; font-size: 15pt; }
.marca strong { display: block; color: #92400e; font-size: 11pt; }
.marca span, .fecha { color: #6b7280; font-size: 8.5pt; }
h1 { font-size: 19pt; color: #111827; margin: 0 0 14px; line-height: 1.25; }
h2 { font-size: 13.5pt; color: #92400e; margin: 22px 0 8px; padding-bottom: 3px; border-bottom: 1px solid #fde68a; break-after: avoid; }
h3 { font-size: 11.5pt; color: #1f2937; margin: 16px 0 6px; break-after: avoid; }
p { margin: 0 0 8px; }
ul, ol { margin: 0 0 10px; padding-left: 22px; }
li { margin: 2px 0; }
a { color: #b45309; text-decoration: none; }
strong { color: #111827; }
code { font: 9pt Consolas, 'Courier New', monospace; background: #f3f4f6; padding: 1px 4px; border-radius: 3px; }
pre { background: #f9fafb; border: 1px solid #e5e7eb; border-left: 3px solid #f59e0b; padding: 10px 12px; border-radius: 4px;
  white-space: pre-wrap; word-break: break-word; break-inside: avoid; margin: 0 0 12px; }
pre code { background: none; padding: 0; font-size: 8.5pt; }
blockquote { margin: 0 0 12px; padding: 8px 12px; background: #fffbeb; border-left: 3px solid #f59e0b; color: #374151; }
blockquote p:last-child { margin: 0; }
table { width: 100%; border-collapse: collapse; margin: 6px 0 14px; font-size: 9pt; break-inside: auto; }
thead { display: table-header-group; }
tr { break-inside: avoid; }
th { background: #fef3c7; color: #78350f; text-align: left; font-weight: 600; }
th, td { border: 1px solid #e5e7eb; padding: 5px 7px; vertical-align: top; }
tbody tr:nth-child(even) td { background: #fafafa; }
hr { border: none; border-top: 1px solid #e5e7eb; margin: 18px 0; }
em { color: #4b5563; }
`;

const hoy = new Intl.DateTimeFormat('es-BO', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'America/La_Paz' }).format(new Date());
const temporal = join(tmpdir(), `jaguares-pdf-${process.pid}`);
mkdirSync(temporal, { recursive: true });

let generados = 0;
for (const fuente of fuentes(FUENTES).sort()) {
  const rel = relative(FUENTES, fuente).replace(/\\/g, '/');
  if (filtro && !rel.startsWith(filtro)) continue;
  // Las referencias a otros documentos apuntan a su PDF
  const markdown = readFileSync(fuente, 'utf8').replace(/([\w./-]+)\.md\b/g, (m, ruta) => (/README$/i.test(ruta) ? m : `${ruta}.pdf`));
  const titulo = (markdown.match(/^#\s+(.+)$/m)?.[1] ?? basename(rel, '.md')).replace(/[*`]/g, '');
  const html = `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>${escapar(titulo)}</title><style>${ESTILOS}</style></head>
<body>
  <div class="encabezado">
    <div class="marca"><div class="logo">J</div><div><strong>Fundación Unión de los Jaguares</strong><span>Plataforma web de gestión</span></div></div>
    <div class="fecha">${hoy}</div>
  </div>
  ${marked.parse(markdown, { gfm: true })}
</body></html>`;
  const archivoHtml = join(temporal, rel.replace(/\//g, '__').replace(/\.md$/, '.html'));
  writeFileSync(archivoHtml, html);
  const pdf = join(DESTINO, rel.replace(/\.md$/, '.pdf'));
  mkdirSync(dirname(pdf), { recursive: true });
  execFileSync(navegador, ['--headless=new', '--disable-gpu', '--no-pdf-header-footer', `--user-data-dir=${join(temporal, 'perfil')}`,
    `--print-to-pdf=${pdf}`, pathToFileURL(archivoHtml).href], { stdio: 'ignore', timeout: 120_000 });
  if (!existsSync(pdf)) throw new Error(`No se generó ${pdf}`);
  console.log(`✔ docs/${rel.replace(/\.md$/, '.pdf')}`);
  generados++;
}
rmSync(temporal, { recursive: true, force: true });
console.log(`\n${generados} documento(s) en PDF.`);
