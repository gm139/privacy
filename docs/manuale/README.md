# Generazione del manuale

Il manuale in PDF (`../Manuale-Privacy-Studio.pdf`) si genera da questi sorgenti con Python e reportlab:

```
python -m venv venv && venv/bin/pip install reportlab fonttools
venv/bin/python build.py ../Manuale-Privacy-Studio.pdf
```

- `lib.py`: impaginazione, copertina (illustrazione vettoriale di un medico, non una fotografia) e riquadri.
- `part_a.py` ... `part_d.py`: il testo, per capitoli.
- I font Ubuntu di sistema sono collegamenti a un font variabile: `lib.py` ne ricava istanze statiche in `.fonts/` (ignorata da git).
