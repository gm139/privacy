# -*- coding: utf-8 -*-
"""Libreria di impaginazione del manuale di Privacy Studio (reportlab)."""
import math
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm, mm
from reportlab.lib import colors
from reportlab.lib.enums import TA_JUSTIFY, TA_LEFT, TA_CENTER
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak, Table, TableStyle,
                                KeepTogether, NextPageTemplate, Flowable, CondPageBreak)
from reportlab.platypus.tableofcontents import TableOfContents

import os
_SRC = "/usr/share/fonts/truetype/ubuntu/"
FD = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".fonts") + os.sep

def _make_fonts():
    """I file Ubuntu-*.ttf di sistema sono collegamenti al font variabile: se ne ricavano istanze statiche (richiede fonttools)."""
    from fontTools.ttLib import TTFont as FT
    from fontTools.varLib import instancer
    os.makedirs(FD, exist_ok=True)
    for src, tag, w in [("Ubuntu[wdth,wght].ttf", "R", 400), ("Ubuntu[wdth,wght].ttf", "B", 700), ("Ubuntu[wdth,wght].ttf", "L", 300), ("Ubuntu[wdth,wght].ttf", "M", 500),
                        ("Ubuntu-Italic[wdth,wght].ttf", "RI", 400), ("Ubuntu-Italic[wdth,wght].ttf", "BI", 700),
                        ("UbuntuMono[wght].ttf", "MR", 400), ("UbuntuMono[wght].ttf", "MB", 700)]:
        out = FD + "U-%s.ttf" % tag
        if os.path.exists(out): continue
        f = FT(_SRC + src)
        axes = {"wght": w}
        if "wdth" in [a.axisTag for a in f["fvar"].axes]: axes["wdth"] = 100
        inst = instancer.instantiateVariableFont(f, axes)
        for rec in inst["name"].names:   # nomi unici: reportlab identifica il font dal nome, uguale in tutte le istanze
            if rec.nameID in (1, 4): rec.string = "Ubuntu Studio " + tag
            elif rec.nameID == 6: rec.string = "UbuntuStudio-" + tag
            elif rec.nameID in (2, 16, 17): rec.string = "Regular"
        inst.save(out)

_make_fonts()
pdfmetrics.registerFont(TTFont("U", FD + "U-R.ttf"))
pdfmetrics.registerFont(TTFont("U-B", FD + "U-B.ttf"))
pdfmetrics.registerFont(TTFont("U-I", FD + "U-RI.ttf"))
pdfmetrics.registerFont(TTFont("U-BI", FD + "U-BI.ttf"))
pdfmetrics.registerFont(TTFont("U-L", FD + "U-L.ttf"))
pdfmetrics.registerFont(TTFont("U-M", FD + "U-M.ttf"))
pdfmetrics.registerFont(TTFont("UM", FD + "U-MR.ttf"))
pdfmetrics.registerFont(TTFont("UM-B", FD + "U-MB.ttf"))
pdfmetrics.registerFontFamily("U", normal="U", bold="U-B", italic="U-I", boldItalic="U-BI")
pdfmetrics.registerFontFamily("UM", normal="UM", bold="UM-B", italic="UM", boldItalic="UM-B")

NAVY = colors.HexColor("#0B2A4A")
TEAL = colors.HexColor("#0E7C86")
TEAL_L = colors.HexColor("#E3F2F3")
INK = colors.HexColor("#1D2733")
GREY = colors.HexColor("#5B6775")
LINE = colors.HexColor("#C9D3DC")
AMBER = colors.HexColor("#B7791F")
AMBER_BG = colors.HexColor("#FFF6E0")
RED = colors.HexColor("#B3261E")
RED_BG = colors.HexColor("#FCECEA")
BLUE = colors.HexColor("#1F4E8C")
BLUE_BG = colors.HexColor("#EAF1FA")
GREEN = colors.HexColor("#2E7D4F")
GREEN_BG = colors.HexColor("#E9F5EE")

W, H = A4
LM = RM = 2.2 * cm
TM = 2.5 * cm
BM = 2.3 * cm

S = {
    "body": ParagraphStyle("body", fontName="U", fontSize=9.6, leading=14.2, textColor=INK, alignment=TA_JUSTIFY, spaceAfter=6, hyphenationLang=None),
    "bodyL": ParagraphStyle("bodyL", fontName="U", fontSize=9.6, leading=14.2, textColor=INK, alignment=TA_LEFT, spaceAfter=6),
    "h1": ParagraphStyle("h1", fontName="U-B", fontSize=21, leading=26, textColor=NAVY, spaceBefore=0, spaceAfter=10),
    "h2": ParagraphStyle("h2", fontName="U-B", fontSize=13.5, leading=18, textColor=TEAL, spaceBefore=14, spaceAfter=5, keepWithNext=1),
    "h3": ParagraphStyle("h3", fontName="U-B", fontSize=10.5, leading=14, textColor=NAVY, spaceBefore=9, spaceAfter=3, keepWithNext=1),
    "bullet": ParagraphStyle("bullet", fontName="U", bulletFontName="U", fontSize=9.6, leading=14, textColor=INK, leftIndent=14, bulletIndent=3, spaceAfter=2.5, alignment=TA_LEFT),
    "step": ParagraphStyle("step", fontName="U", bulletFontName="U-B", fontSize=9.6, leading=14, textColor=INK, leftIndent=20, bulletIndent=0, spaceAfter=3.5, alignment=TA_LEFT),
    "box": ParagraphStyle("box", fontName="U", fontSize=9.1, leading=13.2, textColor=INK, alignment=TA_LEFT),
    "boxlabel": ParagraphStyle("boxlabel", fontName="U-B", fontSize=7.6, leading=10, textColor=GREY, spaceAfter=2),
    "cell": ParagraphStyle("cell", fontName="U", fontSize=8.6, leading=11.8, textColor=INK, alignment=TA_LEFT),
    "cellb": ParagraphStyle("cellb", fontName="U-B", fontSize=8.6, leading=11.8, textColor=colors.white, alignment=TA_LEFT),
    "code": ParagraphStyle("code", fontName="UM", fontSize=8.4, leading=11.4, textColor=colors.HexColor("#102A43"), leftIndent=8, rightIndent=8, spaceBefore=2, spaceAfter=2),
    "toc1": ParagraphStyle("toc1", fontName="U-B", fontSize=10.2, leading=15, textColor=NAVY, leftIndent=0, spaceBefore=5),
    "toc2": ParagraphStyle("toc2", fontName="U", fontSize=9.2, leading=13, textColor=INK, leftIndent=16),
    "small": ParagraphStyle("small", fontName="U", fontSize=8, leading=11, textColor=GREY, alignment=TA_LEFT),
    "title_toc": ParagraphStyle("title_toc", fontName="U-B", fontSize=21, leading=26, textColor=NAVY, spaceAfter=10),
    "caption": ParagraphStyle("caption", fontName="U-I", fontSize=8.2, leading=11, textColor=GREY, alignment=TA_CENTER),
    "title2": ParagraphStyle("title2", fontName="U-B", fontSize=15, leading=20, textColor=NAVY, spaceAfter=6),
}


class Bar(Flowable):
    """Barra colorata sotto i titoli dei capitoli."""
    def __init__(self, w=3.2 * cm, h=2.2, color=TEAL):
        super().__init__(); self.w, self.h, self.color = w, h, color
    def wrap(self, aw, ah): return self.w, self.h + 6
    def draw(self):
        self.canv.setFillColor(self.color); self.canv.rect(0, 2, self.w, self.h, stroke=0, fill=1)


class Doc(BaseDocTemplate):
    def __init__(self, fn, **kw):
        super().__init__(fn, pagesize=A4, leftMargin=LM, rightMargin=RM, topMargin=TM, bottomMargin=BM,
                         title="Privacy Studio - Manuale d'uso e di conformità", author="Privacy Studio",
                         subject="Manuale per lo studio medico: installazione, uso e riferimenti normativi", **kw)
        fr = Frame(LM, BM, W - LM - RM, H - TM - BM, id="f", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
        self.addPageTemplates([PageTemplate(id="cover", frames=[Frame(0, 0, W, H, id="c", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)], onPage=draw_cover),
                               PageTemplate(id="main", frames=[fr], onPage=draw_page)])
        self._chapter = ""
        self._starts = {}

    def afterFlowable(self, f):
        if isinstance(f, Paragraph):
            st = f.style.name
            if st in ("h1", "h2"):
                text = f.getPlainText()
                import zlib
                key = "h-%08x" % zlib.crc32(("%d|%s" % (self.page, text)).encode("utf-8"))
                self.canv.bookmarkPage(key)
                self.canv.addOutlineEntry(text, key, 0 if (st == "h1" or getattr(self, "flat", False)) else 1, False)
                self.notify("TOCEntry", (0 if (st == "h1" or getattr(self, "flat", False)) else 1, text, self.page, key))
                if st == "h1": self._starts[self.page] = text


def draw_page(c, doc):
    c.saveState()
    c.setStrokeColor(LINE); c.setLineWidth(0.6)
    c.line(LM, H - 1.75 * cm, W - RM, H - 1.75 * cm)
    c.setFont("U-M", 8); c.setFillColor(NAVY)
    c.drawString(LM, H - 1.5 * cm, "Privacy Studio")
    c.setFont("U", 8); c.setFillColor(GREY)
    starts = [p for p in doc._starts if p <= doc.page]
    c.drawRightString(W - RM, H - 1.5 * cm, doc._starts[max(starts)][:70] if starts else "")
    c.line(LM, 1.75 * cm, W - RM, 1.75 * cm)
    c.setFont("U", 7.6)
    c.drawString(LM, 1.2 * cm, getattr(doc, "footer", "Manuale d'uso e di conformità - versione 0.9 (bozza per revisione) - novembre 2026"))
    c.setFont("U-B", 9); c.setFillColor(NAVY)
    c.drawRightString(W - RM, 1.2 * cm, str(doc.page))
    c.restoreState()


# ---------------------------------------------------------------- copertina
def _skin(c, col): c.setFillColor(colors.HexColor(col))

def draw_doctor(c, cx, cy, k=1.0):
    """Illustrazione vettoriale di un medico (non una fotografia)."""
    c.saveState(); c.translate(cx, cy); c.scale(k, k)
    def poly(pts, fill, stroke=None, lw=1):
        p = c.beginPath(); p.moveTo(*pts[0])
        for q in pts[1:]: p.lineTo(*q)
        p.close(); c.setFillColor(colors.HexColor(fill))
        if stroke: c.setStrokeColor(colors.HexColor(stroke)); c.setLineWidth(lw)
        c.drawPath(p, fill=1, stroke=1 if stroke else 0)
    # camice
    poly([(-128, -170), (-120, -78), (-70, -46), (-24, -40), (24, -40), (70, -46), (120, -78), (128, -170)], "#F7FAFC", "#C9D6E2", 1.2)
    # camicia / scollo
    poly([(-30, -41), (30, -41), (0, -104)], "#7FC4CB")
    poly([(-30, -41), (0, -104), (-14, -104), (-44, -50)], "#EAF2F8", "#C9D6E2", 0.8)
    poly([(30, -41), (0, -104), (14, -104), (44, -50)], "#EAF2F8", "#C9D6E2", 0.8)
    # collo
    c.setFillColor(colors.HexColor("#E2AE8C")); c.roundRect(-17, -48, 34, 46, 8, stroke=0, fill=1)
    c.setFillColor(colors.HexColor("#D49A78")); c.rect(-17, -48, 34, 12, stroke=0, fill=1)
    # orecchie
    for sx in (-1, 1):
        c.setFillColor(colors.HexColor("#E8B896")); c.ellipse(sx * 50 - 7, 14, sx * 50 + 7, 44, stroke=0, fill=1)
    # testa
    c.setFillColor(colors.HexColor("#F1C6A6")); c.ellipse(-49, -26, 49, 94, stroke=0, fill=1)
    # capelli
    c.setFillColor(colors.HexColor("#3B2A22"))
    p = c.beginPath(); p.moveTo(-52, 52)
    p.curveTo(-60, 118, 60, 118, 52, 52)
    p.curveTo(48, 70, 24, 80, 0, 74)
    p.curveTo(-22, 82, -46, 72, -52, 52); p.close(); c.drawPath(p, fill=1, stroke=0)
    # sopracciglia, occhi, naso, sorriso
    c.setStrokeColor(colors.HexColor("#3B2A22")); c.setLineWidth(2.4); c.setLineCap(1)
    c.line(-30, 52, -10, 55); c.line(10, 55, 30, 52)
    c.setFillColor(colors.HexColor("#22303C"))
    for sx in (-1, 1): c.ellipse(sx * 19 - 5, 36, sx * 19 + 5, 46, stroke=0, fill=1)
    c.setFillColor(colors.white)
    for sx in (-1, 1): c.circle(sx * 19 + 1.8, 43, 1.6, stroke=0, fill=1)
    c.setStrokeColor(colors.HexColor("#C98E6C")); c.setLineWidth(1.6)
    p = c.beginPath(); p.moveTo(0, 38); p.curveTo(-4, 24, -4, 22, 3, 21); c.drawPath(p, fill=0, stroke=1)
    c.setStrokeColor(colors.HexColor("#A5554A")); c.setLineWidth(2.6)
    p = c.beginPath(); p.moveTo(-17, 6); p.curveTo(-8, -6, 8, -6, 17, 6); c.drawPath(p, fill=0, stroke=1)
    # guance
    c.setFillColor(colors.Color(0.93, 0.55, 0.5, alpha=0.25))
    for sx in (-1, 1): c.circle(sx * 30, 14, 8, stroke=0, fill=1)
    # fonendoscopio
    c.setStrokeColor(colors.HexColor("#33414E")); c.setLineWidth(4.4); c.setLineJoin(1)
    p = c.beginPath(); p.moveTo(-31, -43); p.curveTo(-40, -110, -14, -140, 12, -128); c.drawPath(p, fill=0, stroke=1)
    p = c.beginPath(); p.moveTo(31, -43); p.curveTo(40, -90, 36, -112, 30, -126); c.drawPath(p, fill=0, stroke=1)
    c.setFillColor(colors.HexColor("#B8C4CE")); c.setStrokeColor(colors.HexColor("#33414E")); c.setLineWidth(2.2)
    c.circle(20, -134, 13, stroke=1, fill=1)
    c.setFillColor(colors.HexColor("#8796A3")); c.circle(20, -134, 6, stroke=0, fill=1)
    # taschino + penna + croce
    c.setStrokeColor(colors.HexColor("#C9D6E2")); c.setLineWidth(1.1); c.setFillColor(colors.HexColor("#F1F6FA"))
    c.roundRect(-96, -150, 46, 40, 4, stroke=1, fill=1)
    c.setFillColor(colors.HexColor("#0E7C86")); c.roundRect(-84, -122, 5, 24, 2, stroke=0, fill=1)
    c.setFillColor(colors.HexColor("#1F4E8C")); c.roundRect(-74, -118, 5, 20, 2, stroke=0, fill=1)
    # distintivo con croce
    c.setFillColor(colors.HexColor("#0E7C86")); c.roundRect(52, -118, 40, 26, 4, stroke=0, fill=1)
    c.setFillColor(colors.white); c.rect(68, -114, 8, 18, stroke=0, fill=1); c.rect(63, -109, 18, 8, stroke=0, fill=1)
    c.restoreState()


def draw_shield(c, cx, cy, k=1.0, fill="#0E7C86"):
    c.saveState(); c.translate(cx, cy); c.scale(k, k)
    p = c.beginPath(); p.moveTo(0, 46); p.curveTo(18, 40, 30, 38, 38, 38); p.curveTo(38, -8, 24, -34, 0, -48)
    p.curveTo(-24, -34, -38, -8, -38, 38); p.curveTo(-30, 38, -18, 40, 0, 46); p.close()
    c.setFillColor(colors.HexColor(fill)); c.setStrokeColor(colors.white); c.setLineWidth(2.5); c.drawPath(p, fill=1, stroke=1)
    c.setFillColor(colors.white); c.roundRect(-13, -16, 26, 20, 3, stroke=0, fill=1)
    c.setStrokeColor(colors.white); c.setLineWidth(3.6)
    q = c.beginPath(); q.moveTo(-8, 3); q.curveTo(-8, 22, 8, 22, 8, 3); c.drawPath(q, fill=0, stroke=1)
    c.setFillColor(colors.HexColor(fill)); c.circle(0, -6, 3, stroke=0, fill=1)
    c.restoreState()


def draw_cover(c, doc):
    c.saveState()
    # sfondo sfumato
    n = 90
    for i in range(n):
        t = i / (n - 1)
        r = 0x0B + (0x0E - 0x0B) * t; g = 0x2A + (0x6F - 0x2A) * t; b = 0x4A + (0x7C - 0x4A) * t
        c.setFillColor(colors.Color(r / 255, g / 255, b / 255))
        c.rect(0, H - (i + 1) * H / n - 1, W, H / n + 2, stroke=0, fill=1)
    # motivo a punti
    c.setFillColor(colors.Color(1, 1, 1, alpha=0.07))
    for ix in range(0, 26):
        for iy in range(0, 18):
            c.circle(40 + ix * 22, 300 + iy * 22, 1.3, stroke=0, fill=1)
    # cerchi decorativi
    c.setFillColor(colors.Color(1, 1, 1, alpha=0.09)); c.circle(W / 2, 478, 205, stroke=0, fill=1)
    c.setFillColor(colors.Color(1, 1, 1, alpha=0.10)); c.circle(W / 2, 478, 160, stroke=0, fill=1)
    c.setStrokeColor(colors.Color(1, 1, 1, alpha=0.25)); c.setLineWidth(1); c.circle(W / 2, 478, 222, stroke=1, fill=0)
    # medico
    draw_doctor(c, W / 2, 472, 1.38)
    # scudo
    draw_shield(c, W / 2 + 175, 640, 1.0)
    # titolo
    c.setFillColor(colors.HexColor("#8FD3D9")); c.setFont("U-M", 11)
    c.drawString(LM + 4, H - 2.6 * cm, "GDPR  |  STUDIO MEDICO")
    c.setFillColor(colors.white); c.setFont("U-B", 44); c.drawString(LM, H - 4.6 * cm, "Privacy Studio")
    c.setFont("U-L", 17); c.drawString(LM + 2, H - 5.55 * cm, "Manuale d'uso e di conformità")
    c.setStrokeColor(colors.HexColor("#8FD3D9")); c.setLineWidth(2.4); c.line(LM + 2, H - 6.0 * cm, LM + 2 + 3.2 * cm, H - 6.0 * cm)
    # pannello inferiore
    c.setFillColor(colors.white); c.roundRect(LM - 4, 2.1 * cm, W - 2 * LM + 8, 5.2 * cm, 10, stroke=0, fill=1)
    c.setFillColor(NAVY); c.setFont("U-B", 12.5)
    c.drawString(LM + 14, 6.1 * cm, "Dall'installazione all'uso quotidiano")
    c.setFont("U", 9.6); c.setFillColor(INK)
    lines = ["Registro dei trattamenti, informative e consegne, richieste degli interessati,",
             "violazioni dei dati, conservazione, backup e ripristino, con i riferimenti al",
             "Regolamento (UE) 2016/679 e al Codice in materia di protezione dei dati personali."]
    for i, l in enumerate(lines): c.drawString(LM + 14, 5.35 * cm - i * 0.5 * cm, l)
    c.setStrokeColor(LINE); c.setLineWidth(0.8); c.line(LM + 14, 3.55 * cm, W - RM - 10, 3.55 * cm)
    c.setFont("U-B", 8.4); c.setFillColor(AMBER)
    c.drawString(LM + 14, 3.0 * cm, "VERSIONE 0.9 - BOZZA PER REVISIONE")
    c.setFont("U", 8.4); c.setFillColor(GREY)
    c.drawString(LM + 14, 2.55 * cm, "Novembre 2026  -  Documento per il titolare del trattamento. Non è una consulenza legale.")
    c.restoreState()


# ---------------------------------------------------------------- elementi di testo
def P(t, st="body"): return Paragraph(t, S[st])

def H1(t, newpage=True):
    head = [PageBreak()] if newpage else [Spacer(1, 22), CondPageBreak(7 * cm)]
    return head + [Paragraph(t, S["h1"]), Bar(), Spacer(1, 4)]

def H2(t): return [Paragraph(t, S["h2"])]
def H3(t): return [Paragraph(t, S["h3"])]

def BUL(items):
    return [Paragraph(i, S["bullet"], bulletText="•") for i in items]

def STEPS(items):
    return [Paragraph(i, S["step"], bulletText="%d." % (n + 1)) for n, i in enumerate(items)]

def CODE(text):
    rows = [[Paragraph(l.replace(" ", "&nbsp;") or "&nbsp;", S["code"])] for l in text.strip("\n").split("\n")]
    t = Table(rows, colWidths=[W - LM - RM])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#EEF2F6")), ("BOX", (0, 0), (-1, -1), 0.6, LINE),
                           ("TOPPADDING", (0, 0), (-1, -1), 0), ("BOTTOMPADDING", (0, 0), (-1, -1), 0)]))
    return [t, Spacer(1, 6)]

def _box(label, text, accent, bg, extra=None):
    inner = [Paragraph(label, ParagraphStyle("l", parent=S["boxlabel"], textColor=accent))]
    for x in (text if isinstance(text, list) else [text]): inner.append(Paragraph(x, S["box"]))
    t = Table([[inner]], colWidths=[W - LM - RM])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), bg), ("LINEBEFORE", (0, 0), (0, -1), 3.2, accent),
                           ("LEFTPADDING", (0, 0), (-1, -1), 10), ("RIGHTPADDING", (0, 0), (-1, -1), 9),
                           ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 7)]))
    return [KeepTogether([t, Spacer(1, 8)])]

def NORMA(t, ref=None): return _box("RIFERIMENTO NORMATIVO" + (" - " + ref if ref else ""), t, BLUE, BLUE_BG)
def ESAMINARE(t): return _box("DA ESAMINARE", t, AMBER, AMBER_BG)
def ATTENZIONE(t): return _box("ATTENZIONE", t, RED, RED_BG)
def CONSIGLIO(t): return _box("CONSIGLIO", t, GREEN, GREEN_BG)
def NOTA(t): return _box("NOTA", t, TEAL, TEAL_L)

def TABLE(rows, widths, header=True, zebra=True, fs=None):
    total = W - LM - RM
    cw = [total * w for w in widths]
    data = []
    for i, r in enumerate(rows):
        row = []
        for cell in r:
            if isinstance(cell, str): row.append(Paragraph(cell, S["cellb"] if (header and i == 0) else S["cell"]))
            else: row.append(cell)
        data.append(row)
    t = Table(data, colWidths=cw, repeatRows=1 if header else 0)
    st = [("VALIGN", (0, 0), (-1, -1), "TOP"), ("LINEBELOW", (0, 0), (-1, -1), 0.4, LINE),
          ("LEFTPADDING", (0, 0), (-1, -1), 6), ("RIGHTPADDING", (0, 0), (-1, -1), 6),
          ("TOPPADDING", (0, 0), (-1, -1), 4.5), ("BOTTOMPADDING", (0, 0), (-1, -1), 5)]
    if header: st += [("BACKGROUND", (0, 0), (-1, 0), NAVY)]
    if zebra:
        for i in range(1, len(rows)):
            if i % 2 == 0: st.append(("BACKGROUND", (0, i), (-1, i), colors.HexColor("#F4F7FA")))
    t.setStyle(TableStyle(st))
    return [t, Spacer(1, 8)]

_fig_n = [0]
_IMG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "img")
_IMGC = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".img")

def FIG(name, caption, maxh=12.8 * cm, dpi=192):
    """Schermata del programma (immagine a 2x): dimensione naturale se sta nella pagina, altrimenti ridotta; con didascalia numerata."""
    from PIL import Image as PI
    from reportlab.platypus import Image
    src = os.path.join(_IMG, name + ".png")
    os.makedirs(_IMGC, exist_ok=True)
    dst = os.path.join(_IMGC, name + ".png")
    im = PI.open(src).convert("RGB")
    pw, ph = im.size
    if pw > 1500:
        im = im.resize((1500, int(ph * 1500 / pw)), PI.LANCZOS); pw, ph = im.size
    if not os.path.exists(dst): im.save(dst, optimize=True)
    avail = W - LM - RM - 4
    nat_w = pw / dpi * 2.54 * cm * (2134 / 1500.0 if pw == 1500 else 1.0)   # larghezza naturale (le immagini ridotte tornano alla scala originale)
    w = min(avail, nat_w); h = w * ph / pw
    if h > maxh: w = w * maxh / h; h = maxh
    _fig_n[0] += 1
    img = Image(dst, width=w, height=h)
    box = Table([[img]], colWidths=[w + 4])
    box.setStyle(TableStyle([("BOX", (0, 0), (-1, -1), 0.8, LINE), ("LEFTPADDING", (0, 0), (-1, -1), 1), ("RIGHTPADDING", (0, 0), (-1, -1), 1),
                             ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1)]))
    cap = Paragraph("<b>Figura %d</b> - %s" % (_fig_n[0], caption), S["caption"])
    return [KeepTogether([Spacer(1, 4), box, Spacer(1, 3), cap, Spacer(1, 8)])]

def build_toc():
    toc = TableOfContents()
    toc.levelStyles = [S["toc1"], S["toc2"]]
    toc.dotsMinLevel = 0
    return toc
