# -*- coding: utf-8 -*-
from lib import *
import part_a, part_b, part_c, part_d

def story():
    s = []
    s += part_a.story()
    s += part_b.story()
    s += part_c.story()
    s += part_d.story()
    return s
