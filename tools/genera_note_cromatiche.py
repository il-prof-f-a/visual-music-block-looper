"""Generate the 48 printable pitch cards. Requires reportlab, pypdfium2, Pillow."""
from pathlib import Path
import csv
import io

from PIL import Image
import pypdfium2 as pdfium
from reportlab.pdfgen import canvas
from reportlab.lib.units import mm
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.graphics.barcode.qr import QrCodeWidget
from reportlab.graphics import renderPDF
from reportlab.graphics.shapes import Drawing

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets' / 'cartoncini-note'
PDF = ROOT / 'output' / 'pdf' / 'cartoncini-note-do2-si5.pdf'
NAMES = ['Do', 'Do#', 'Re', 'Re#', 'Mi', 'Fa', 'Fa#', 'Sol', 'Sol#', 'La', 'La#', 'Si']
DEGREES = [0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6]


def spectrum(wavelength):
    """Approximate spectral sRGB, 380..780 nm; not a colorimetric calibration."""
    w = wavelength
    if w < 440: r, g, b = (440-w)/60, 0, 1
    elif w < 490: r, g, b = 0, (w-440)/50, 1
    elif w < 510: r, g, b = 0, 1, (510-w)/20
    elif w < 580: r, g, b = (w-510)/70, 1, 0
    elif w < 645: r, g, b = 1, (645-w)/65, 0
    else: r, g, b = 1, 0, 0
    return tuple(max(0, min(1, c)) ** 0.8 * 0.72 for c in (r, g, b))


def pitch_data(midi):
    frequency = 440 * 2 ** ((midi-69)/12)
    # Map log(audio frequency) to optical frequency, anchored to piano A0..C8.
    t = (midi-21)/87
    optical_thz = (299792458 / (700e-9) + t *
                   (299792458 / (400e-9) - 299792458 / (700e-9))) / 1e12
    wavelength = 299792458 / (optical_thz * 1e12) * 1e9
    return frequency, wavelength, optical_thz, spectrum(wavelength)


def card(c, midi):
    frequency, wavelength, optical, rgb = pitch_data(midi)
    octave, pc = midi//12-1, midi % 12
    name = NAMES[pc].replace('#', '♯') + str(octave)
    c.saveState()
    c.scale(mm, mm)
    c.setFillColorRGB(*rgb)
    c.rect(0, 0, 60, 40, fill=1, stroke=0)
    # The continuous dark frame also preserves a measurable card body for vision.
    c.setStrokeColorRGB(0, 0, 0)
    c.setLineWidth(.25)
    c.rect(.125, .125, 59.75, 39.75, fill=0, stroke=1)
    qr = QrCodeWidget(f'VMBL1;N;{midi-36}', barLevel='M', barBorder=4)
    x0, y0, x1, y1 = qr.getBounds()
    d = Drawing(32, 32, transform=[32/(x1-x0), 0, 0, 32/(y1-y0), 0, 0])
    d.add(qr)
    c.setFillColorRGB(1, 1, 1)
    c.rect(2, 4, 32, 32, fill=1, stroke=0)
    renderPDF.draw(d, c, 2, 4)
    c.setFillColorRGB(1, 1, 1)
    c.roundRect(36, 8, 22, 23, .7, fill=1, stroke=0)
    c.setFillColorRGB(1, 1, 1)
    c.setFont('LabelBold', 12/mm)
    c.drawCentredString(47, 34, name)
    c.setFont('Label', 8/mm)
    c.drawCentredString(47, 3.6, f'{frequency:.2f}'.replace('.', ',') + ' Hz')
    # Five staff lines, 2 mm spacing. Diatonic step = 1 mm.
    bottom = 15.5
    c.setStrokeColorRGB(.12, .12, .12)
    c.setLineWidth(.13)
    for i in range(5):
        c.line(37, bottom+2*i, 57, bottom+2*i)
    bass = midi < 60
    code = 0x1d122 if bass else 0x1d11e
    # Segoe UI Symbol clefs use a common music origin; the bass F dots are at F3.
    c.setFillColorRGB(0, 0, 0)
    c.setFont('Music', 9.5 if bass else 15)
    c.drawString(37.2, bottom + (1.1 if bass else -1.9), chr(code))
    degree = octave*7 + DEGREES[pc]
    anchor = 2*7+4 if bass else 4*7+2  # bottom G2 or E4
    offset = degree-anchor
    y = bottom+offset
    x = 51.7
    c.setLineWidth(.17)
    if offset <= -2:
        for step in range(-2, offset-1, -2):
            c.line(x-2, bottom+step, x+2, bottom+step)
    if offset >= 10:
        for step in range(10, offset+1, 2):
            c.line(x-2, bottom+step, x+2, bottom+step)
    if '#' in NAMES[pc]:
        c.setFont('Music', 4.8)
        c.drawCentredString(x-3.6, y-1.5, '♯')
    # Hollow diamond, deliberately without stem: pitch only, width controls duration.
    p = c.beginPath()
    p.moveTo(x-1.6, y)
    p.lineTo(x, y+1.05)
    p.lineTo(x+1.6, y)
    p.lineTo(x, y-1.05)
    p.close()
    c.setFillColorRGB(1, 1, 1)
    c.setLineWidth(.33)
    c.drawPath(p, fill=1, stroke=1)
    c.restoreState()


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    PDF.parent.mkdir(parents=True, exist_ok=True)
    pdfmetrics.registerFont(TTFont('Label', 'C:/Windows/Fonts/segoeui.ttf'))
    pdfmetrics.registerFont(TTFont('LabelBold', 'C:/Windows/Fonts/segoeuib.ttf'))
    pdfmetrics.registerFont(TTFont('Music', 'C:/Windows/Fonts/seguisym.ttf'))
    c = canvas.Canvas(str(PDF), pagesize=A4)
    c.setTitle('Cartoncini cromatici Do2-Si5 | 60 x 40 mm')
    rows = []
    previews = []
    for octave in range(2, 6):
        c.setFillColorRGB(.12, .12, .12)
        c.setFont('LabelBold', 18)
        c.drawString(12*mm, 276*mm, f'Do{octave} - Si{octave}')
        c.setFont('Label', 9)
        c.drawString(12*mm, 269*mm, 'Scala cromatica / ' + ('Chiave di basso' if octave < 4 else 'Chiave di violino'))
        for pc in range(12):
            midi = (octave+1)*12+pc
            x, y = (12+(pc % 3)*63)*mm, (214-(pc//3)*47)*mm
            c.saveState()
            c.translate(x, y)
            card(c, midi)
            c.restoreState()
            buf = io.BytesIO()
            single = canvas.Canvas(buf, pagesize=(60*mm, 40*mm))
            card(single, midi)
            single.showPage()
            single.save()
            doc = pdfium.PdfDocument(buf.getvalue())
            img = doc[0].render(scale=300/72).to_pil().convert('RGB')
            name = f'nota-{midi:03d}-{NAMES[pc].lower().replace("#", "-diesis")}{octave}.png'
            img.save(OUT/name, dpi=(300, 300))
            previews.append(img)
            f, wave, optical, rgb = pitch_data(midi)
            rows.append([name, NAMES[pc]+str(octave), midi, midi-36, f'{f:.6f}',
                         'basso' if midi < 60 else 'violino', f'{wave:.3f}', f'{optical:.3f}',
                         'VMBL1;N;'+str(midi-36)])
        c.setStrokeColorRGB(.2, .2, .2)
        c.setLineWidth(.5)
        c.line(12*mm, 53*mm, 72*mm, 53*mm)
        c.line(12*mm, 52*mm, 12*mm, 54*mm)
        c.line(72*mm, 52*mm, 72*mm, 54*mm)
        c.setFont('Label', 8)
        c.drawString(12*mm, 47*mm, 'Controllo scala: questa linea deve misurare 60 mm.')
        c.drawString(12*mm, 39*mm, 'Stampa al 100%, senza adattamento. Cartoncini 60 x 40 mm. Taglia sul bordo nero.')
        c.drawString(12*mm, 32*mm, 'Temperamento equabile / La4 = 440 Hz / Do4 = MIDI 60 / 12 note per ottava')
        c.drawString(12*mm, 25*mm, 'Colore: spettro rosso-violetto ancorato al pianoforte La0-Do8. QR: protocollo VMBL1.')
        c.drawRightString(198*mm, 15*mm, f'{octave-1} / 4')
        c.showPage()
    c.save()
    with (OUT/'indice.csv').open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.writer(stream)
        writer.writerow(['file', 'nota', 'midi', 'idClip', 'frequenza_Hz', 'chiave', 'lunghezza_onda_nm', 'frequenza_ottica_THz', 'payload'])
        writer.writerows(rows)
    sheet = Image.new('RGB', (1200, 4*220), '#eeeeee')
    for row in range(4):
        for col, pc in enumerate([0, 1, 7, 11]):
            image = previews[row*12+pc].resize((283, 189), Image.Resampling.LANCZOS)
            sheet.paste(image, (10+col*300, 12+row*220))
    sheet.save(OUT/'anteprima.png')
    doc = pdfium.PdfDocument(str(PDF))
    for i in range(len(doc)):
        doc[i].render(scale=1.5).to_pil().save(OUT/f'pagina-{i+1}.png')
    print(f'Generated {len(rows)} cards and {len(doc)} A4 pages: {PDF}')


if __name__ == '__main__':
    main()
