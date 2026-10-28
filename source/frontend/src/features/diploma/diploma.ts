import * as fontkitModule from '@pdf-lib/fontkit';
import { PDFDocument, type PDFFont, type PDFPage, rgb } from 'pdf-lib';

type Fontkit = typeof fontkitModule;

/**
 * The fontkit object: the browser build of `@pdf-lib/fontkit` exports it only as `default`, while the CommonJS build the
 * tests load exposes `create` on the namespace.
 */
export function fontkitOf(module: Fontkit | { readonly default: Fontkit }): Fontkit {
  return 'default' in module ? module.default : module;
}

const fontkit = fontkitOf(fontkitModule);

/** The font files of one weight: the basic Latin subset and the Latin Extended subset with the Polish letters. */
export interface FontPair {
  readonly latin: Uint8Array;
  readonly latinExt: Uint8Array;
}

export interface DiplomaFonts {
  readonly regular: FontPair;
  readonly bold: FontPair;
}

/** The already translated and formatted text of the diploma; the PDF only lays it out. */
export interface DiplomaText {
  readonly title: string;
  readonly nickname: string;
  readonly subtitle: string;
  readonly lines: readonly string[];
  readonly score: string;
  readonly achievements: string;
  readonly footer: string;
}

interface Pair {
  readonly latin: PDFFont;
  readonly ext: PDFFont;
  readonly latinChars: ReadonlySet<number>;
}

async function embed(doc: PDFDocument, fonts: FontPair): Promise<Pair> {
  const latin = await doc.embedFont(fonts.latin, { subset: true });
  const ext = await doc.embedFont(fonts.latinExt, { subset: true });
  return { latin, ext, latinChars: new Set(latin.getCharacterSet()) };
}

/** Splits text into runs that each one font of the pair can draw: Polish letters come from the Latin Extended subset. */
export function runs(
  text: string,
  latinChars: ReadonlySet<number>,
): { text: string; ext: boolean }[] {
  const result: { text: string; ext: boolean }[] = [];
  for (const char of text) {
    const ext = !latinChars.has(char.codePointAt(0) ?? 0);
    const last = result.at(-1);
    if (last?.ext === ext) {
      last.text += char;
    } else {
      result.push({ text: char, ext });
    }
  }

  return result;
}

function width(pair: Pair, text: string, size: number): number {
  return runs(text, pair.latinChars).reduce(
    (sum, run) => sum + (run.ext ? pair.ext : pair.latin).widthOfTextAtSize(run.text, size),
    0,
  );
}

function drawCentered(page: PDFPage, pair: Pair, text: string, y: number, size: number) {
  let x = (page.getWidth() - width(pair, text, size)) / 2;
  for (const run of runs(text, pair.latinChars)) {
    const font = run.ext ? pair.ext : pair.latin;
    page.drawText(run.text, { x, y, size, font, color: rgb(0.1, 0.1, 0.2) });
    x += font.widthOfTextAtSize(run.text, size);
  }
}

/** Wraps a long line to the page width at word boundaries. */
function wrap(pair: Pair, text: string, size: number, maxWidth: number): string[] {
  const lines: string[] = [];
  let line = '';
  for (const word of text.split(' ')) {
    const next = line === '' ? word : `${line} ${word}`;
    if (width(pair, next, size) > maxWidth && line !== '') {
      lines.push(line);
      line = word;
    } else {
      line = next;
    }
  }

  if (line !== '') {
    lines.push(line);
  }

  return lines;
}

/** An A4 landscape career diploma built in the browser from the ended career's server view (TD-44, TD-119). */
export async function buildDiploma(text: DiplomaText, fonts: DiplomaFonts): Promise<Uint8Array> {
  const doc = await PDFDocument.create();
  doc.registerFontkit(fontkit);
  doc.setTitle(`${text.title} — ${text.nickname}`);
  doc.setCreator('Polski Streamer Simulator');
  const regular = await embed(doc, fonts.regular);
  const bold = await embed(doc, fonts.bold);
  const page = doc.addPage([841.89, 595.28]);
  page.drawRectangle({
    x: 24,
    y: 24,
    width: 793.89,
    height: 547.28,
    borderWidth: 3,
    borderColor: rgb(0.55, 0.2, 0.6),
  });

  let y = 510;
  drawCentered(page, bold, text.title, y, 34);
  y -= 50;
  drawCentered(page, bold, text.nickname, y, 28);
  y -= 30;
  drawCentered(page, regular, text.subtitle, y, 14);
  y -= 40;
  for (const line of text.lines) {
    drawCentered(page, regular, line, y, 14);
    y -= 22;
  }

  y -= 10;
  drawCentered(page, bold, text.score, y, 22);
  y -= 34;
  for (const line of wrap(regular, text.achievements, 12, 700)) {
    drawCentered(page, regular, line, y, 12);
    y -= 18;
  }

  drawCentered(page, regular, text.footer, 48, 10);
  return doc.save();
}
