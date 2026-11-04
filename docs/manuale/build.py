# -*- coding: utf-8 -*-
import sys
from lib import *
import content

def main(out):
    doc = Doc(out)
    story = [NextPageTemplate("main"), PageBreak()]
    story += [Paragraph("Indice", S["title_toc"]), Bar(), Spacer(1, 8)]
    toc = build_toc(); story.append(toc)
    story += content.story()
    doc.multiBuild(story)

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Manuale-Privacy-Studio.pdf")
