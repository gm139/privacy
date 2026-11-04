# Generazione del manuale

Il manuale in PDF (`../Manuale-Privacy-Studio.pdf`) si genera da questi sorgenti con Python e reportlab:

```
python -m venv venv && venv/bin/pip install reportlab fonttools
venv/bin/python build.py ../Manuale-Privacy-Studio.pdf
```

- `lib.py`: impaginazione, copertina (illustrazione vettoriale di un medico, non una fotografia) e riquadri.
- `part_a.py` ... `part_d.py`: il testo, per capitoli.
- I font Ubuntu di sistema sono collegamenti a un font variabile: `lib.py` ne ricava istanze statiche in `.fonts/` (ignorata da git).

## Schermate

Le immagini in `img/` sono generate dal programma stesso (build Debug) con dati inventati, su un database dimostrativo vuoto:

```
set PRIVACY_CONN=Host=127.0.0.1;Port=55432;Database=privacy_demo;Username=privacy_app;Password=...
Privacy.App.exe --screenshots "C:\cartella\di\uscita" --theme light   (oppure dark)
```

Dopo un cambiamento dell'interfaccia basta rigenerarle e ricostruire il PDF. Richiede `pillow`.

In Windows serve il .NET 10 Desktop Runtime (anche per eseguire la build Debug da un SDK installato in una cartella utente: impostare `DOTNET_ROOT`).
Le immagini `dark-*.png` sono quelle del tema scuro; le altre, del tema chiaro.
