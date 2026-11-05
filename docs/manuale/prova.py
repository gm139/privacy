# -*- coding: utf-8 -*-
"""Lista di prova dell'installazione su un Windows pulito (PDF breve, senza copertina)."""
import sys
from lib import *

C_OK = "[&nbsp;&nbsp;] OK<br/>[&nbsp;&nbsp;] KO"

def rows(items):
    out = [["N.", "Che cosa fare", "Risultato atteso", "Esito / note"]]
    for n, a, r in items: out.append([n, a, r, C_OK])
    return TABLE(out, [0.06, 0.40, 0.36, 0.18])

def story():
    s = [Paragraph("Lista di prova dell'installazione", S["title_toc"]), Bar(), Spacer(1, 6)]
    s += [Paragraph("Privacy Studio 0.9.0 - programma di installazione (bozza)", S["title2"])]
    s += [P("Questa lista serve a provare <b>per la prima volta</b> l'installazione reale su un computer Windows pulito. Finora l'installatore è stato verificato solo in \"prova a secco\": "
            "l'installazione di PostgreSQL, la creazione del database e dei ruoli, e i permessi sui file <b>non sono mai stati eseguiti</b>. Ogni riga ha un risultato atteso: "
            "se non coincide, annotare cosa è successo e raccogliere i file indicati nella sezione E. Un KO non è un problema della prova: è ciò che la prova deve trovare.")]
    s += ATTENZIONE("Provare <b>solo</b> in una macchina virtuale (o computer di prova) con un'istantanea (snapshot) fatta prima di cominciare, e con dati inventati. "
                    "Non usare password reali: scegliere password di prova. Non provare sul computer di lavoro dello studio.")

    s += H2("A. Preparazione")
    s += rows([
        ("A1", "Creare una macchina virtuale Windows 10 o 11 a 64 bit, con almeno 4 GB di memoria, 40 GB di disco e <b>un secondo disco virtuale</b> (sarà la \"copia esterna\" D:). Aggiornare Windows.", "Windows pulito, utente amministratore, secondo disco visibile come D:."),
        ("A2", "Fare un'<b>istantanea</b> della macchina, chiamata \"pulita\".", "Si può tornare a questo stato dopo ogni prova."),
        ("A3", "In PowerShell: <font name='UM'>Get-Service postgres*</font> e <font name='UM'>Test-Path \"C:\\Program Files\\PostgreSQL\"</font>.", "Nessun servizio; <font name='UM'>False</font>."),
        ("A4", "<font name='UM'>netstat -ano | findstr :5432</font>", "Nessun risultato: la porta 5432 è libera."),
        ("A5", "Copiare nella macchina il file <font name='UM'>PrivacyStudio-Setup-0.9.0.exe</font> (circa 410 MB) in <font name='UM'>C:\\Temp</font>. Confrontare l'impronta: <font name='UM'>Get-FileHash C:\\Temp\\PrivacyStudio-Setup-0.9.0.exe</font>.", "L'impronta SHA-256 è <font name='UM'>0748E1D11576128FFAACDC5714CE78DFB0AD73CA8334B1DCEBC32063BE327B98</font> (se l'installatore è stato ricostruito, usare quella nuova)."),
    ])

    s += H2("B. Installazione normale")
    s += [P("Avviare l'installatore con il registro attivo, da un prompt dei comandi: <font name='UM'>C:\\Temp\\PrivacyStudio-Setup-0.9.0.exe /LOG=\"C:\\Temp\\setup.log\"</font>. "
            "Annotare l'ora di inizio e di fine.")]
    s += rows([
        ("B1", "Confermare la richiesta di Windows (consenso amministratore).", "Windows mostra l'avviso \"editore sconosciuto\" (previsto: il programma non è firmato). La procedura guidata è in italiano."),
        ("B2", "Nella pagina della password del database: provare prima una password <b>corta</b>, poi due password <b>diverse</b>, poi una con un carattere non ammesso (per esempio <font name='UM'>%</font>).", "In ogni caso un messaggio chiaro e non si va avanti."),
        ("B3", "Inserire una password valida di prova (es. <font name='UM'>ProvaPassword-123</font>) due volte. Annotarla.", "Si va avanti."),
        ("B4", "Pagina dei backup: lasciare vuota la copia esterna e premere avanti.", "Avviso che il backup resterebbe sullo stesso computer; si può rifiutare e tornare indietro."),
        ("B5", "Indicare come copia esterna <font name='UM'>D:\\CopiaEsterna</font>. Proseguire e attendere.", "Nessun errore. L'installazione di PostgreSQL dura di solito diversi minuti: annotare quanto."),
        ("B6", "Al termine, controllare la cartella del programma (di norma <font name='UM'>C:\\Program Files\\PrivacyStudio</font>).", "Presenti l'eseguibile, la cartella <font name='UM'>db</font> con 5 file .sql, la cartella <font name='UM'>docs</font> con il manuale e <font name='UM'>installazione.log</font>."),
        ("B7", "Aprire <font name='UM'>installazione.log</font>.", "Elenca i passaggi in ordine, termina con \"Fatto.\" e <b>non contiene password</b>."),
        ("B8", "<font name='UM'>Get-Service postgresql-x64-17</font>", "Stato <font name='UM'>Running</font>, avvio <font name='UM'>Automatic</font>."),
        ("B9", "<font name='UM'>netstat -an | findstr :5432</font>", "Solo <font name='UM'>127.0.0.1:5432</font> (ed eventualmente <font name='UM'>[::1]</font>) in ascolto; <b>mai</b> <font name='UM'>0.0.0.0:5432</font>."),
        ("B10", "<font name='UM'>Get-NetFirewallRule | Where DisplayName -like \"*postgres*\"</font>", "Nessuna regola in ingresso per la porta 5432 (annotare se ce n'è una)."),
        ("B11", "<font name='UM'>&amp; \"C:\\Program Files\\PostgreSQL\\17\\bin\\psql.exe\" -h 127.0.0.1 -U postgres -d privacy -c \"\\du\"</font> (chiede la password di B3)", "Compaiono i ruoli <font name='UM'>privacy_app</font> e <font name='UM'>privacy_migrator</font>."),
        ("B12", "Stesso comando con <font name='UM'>-c \"select count(*) from ps.cat_right\"</font>", "Risultato 6."),
        ("B13", "<font name='UM'>icacls \"$env:LOCALAPPDATA\\PrivacyStudio\\connection.txt\"</font> e <font name='UM'>icacls C:\\ProgramData\\PrivacyStudio\\migrator.secret</font>", "Il primo: solo l'utente, Administrators e SYSTEM. Il secondo: solo Administrators e SYSTEM."),
        ("B14", "Aprire <font name='UM'>%LOCALAPPDATA%\\PrivacyStudio\\config.json</font>.", "Percorsi dei backup (con <font name='UM'>D:\\CopiaEsterna</font>) e <font name='UM'>pgBinDir</font> corretti."),
        ("B15", "Avviare Privacy Studio dal collegamento nel menu Start.", "Si apre la finestra <b>Prima configurazione</b> (nessun errore di connessione)."),
        ("B16", "Compilare la configurazione con dati inventati e accedere.", "Si apre la finestra principale con le schede."),
        ("B17", "Scheda <b>Backup</b>: il pulsante \"Esegui backup ora\" è attivo?", "Sì (significa che <font name='UM'>pgBinDir</font> è stato riconosciuto)."),
        ("B18", "<b>Esporta chiavi di recupero…</b> con una frase di prova; salvare il file su D:.", "File <font name='UM'>.pskr</font> creato."),
        ("B19", "<b>Esegui backup ora</b>.", "Esito \"Riuscito\" con data di verifica; i file <font name='UM'>.psbk</font> sono sia in <font name='UM'>Documenti\\Backup Privacy Studio</font> sia in <font name='UM'>D:\\CopiaEsterna</font>."),
        ("B20", "Registrare una richiesta e un paziente di prova. <b>Riavviare la macchina</b> e riaprire il programma.", "Il servizio del database parte da solo; si accede e i dati inseriti ci sono ancora."),
    ])

    s += H2("C. Casi particolari")
    s += [P("Tornare all'istantanea \"pulita\" tra un caso e l'altro, salvo dove indicato.")]
    s += rows([
        ("C1", "<b>Seconda esecuzione</b> dell'installatore sopra l'installazione di B (stessa password).", "Non tocca i dati. Nel registro: \"Database e configurazione già presenti: i dati NON vengono toccati\". Il programma resta utilizzabile con i dati di prima."),
        ("C2", "<b>Disinstallare</b> da Impostazioni di Windows.", "Programma rimosso; messaggio che database, backup e chiavi restano. Il servizio PostgreSQL, il database e i file in <font name='UM'>D:\\CopiaEsterna</font> ci sono ancora."),
        ("C3", "<b>Reinstallare</b> dopo C2.", "L'installatore riconosce PostgreSQL già presente e chiede la password di <font name='UM'>postgres</font>; non crea un secondo database; i dati di prima sono intatti."),
        ("C4", "<b>PostgreSQL 17 già installato</b> (istantanea separata, con una propria password) e poi l'installatore.", "L'installatore lo rileva e chiede la password; <b>non reinstalla</b> PostgreSQL. Provare anche con una password <b>sbagliata</b> e annotare il messaggio."),
        ("C5", "Interrompere l'installazione chiudendo la finestra durante l'attesa (solo se possibile).", "Annotare lo stato lasciato (servizio, database, file). Non è un caso previsto: serve a conoscere il rischio."),
        ("C6", "Eseguire l'installatore da un utente <b>standard</b>, dando le credenziali di un altro amministratore alla richiesta di Windows.", "Limite noto: i file di connessione finiscono nel profilo dell'amministratore, non in quello dell'utente standard. Annotare cosa succede all'avvio."),
        ("C7", "Spegnere la macchina bruscamente durante l'uso e riaccendere.", "Il database riparte; il programma si apre; nessun errore di integrità."),
    ])

    s += H2("D. Ripristino su un'altra macchina (facoltativo, ma importante)")
    s += [P("Su una <b>seconda</b> macchina virtuale pulita, con l'installazione di B completata <b>senza</b> usare l'istantanea della prima. Servono: il file <font name='UM'>.psbk</font> e il suo <font name='UM'>.ledger.jsonl</font>, il file delle chiavi di recupero con la sua frase.")]
    s += rows([
        ("D1", "Sulla seconda macchina: <b>Backup › Importa chiavi…</b> con il file e la frase di B18.", "Chiavi reinstallate."),
        ("D2", "<b>Backup › Verifica un file…</b> sul file <font name='UM'>.psbk</font> della prima macchina.", "\"Il file si decifra ed è una copia valida\"."),
        ("D3", "Da PowerShell impostare <font name='UM'>PRIVACY_ADMIN_CONN</font> (Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=…) ed eseguire <font name='UM'>Privacy.App.exe --restore \"percorso.psbk\" --database privacy_ripristino</font>.", "Riepilogo senza errori: catena di audit integra, nessuna eliminazione da verificare a mano, prova di ripristino registrata."),
        ("D4", "Impostare le password dei ruoli del nuovo database (non sono nel backup) e controllare i dati.", "I dati della prima macchina sono presenti."),
    ])

    s += H2("E. Che cosa raccogliere e restituire")
    s += BUL([
        "<b>C:\\Temp\\setup.log</b> (registro dell'installatore) e <b>installazione.log</b> (nella cartella del programma).",
        "I registri del programma di PostgreSQL, di solito in <font name='UM'>%TEMP%</font>: <font name='UM'>install-postgresql.log</font> e i file <font name='UM'>bitrock_installer*.log</font>.",
        "L'<b>immagine</b> di ogni messaggio di errore e l'<b>output</b> dei comandi con esito KO.",
        "Versione di Windows (<font name='UM'>winver</font>), quanto è durata l'installazione, e se Windows Defender ha segnalato qualcosa.",
        "<b>Non inviare password</b> reali né file di chiavi: per la prova usare solo password e dati inventati.",
    ])

    s += H2("F. Quando la prova è superata")
    s += TABLE([
        ["Criterio", "Riferimento"],
        ["L'installazione completa termina senza errori su Windows pulito", "B1-B8"],
        ["Il database è raggiungibile solo dal computer stesso", "B9, B10"],
        ["I file con password sono protetti come previsto", "B13"],
        ["Il programma si avvia, si configura e fa un backup verificato con copia esterna", "B15-B19"],
        ["Dopo il riavvio tutto riparte da solo e i dati ci sono", "B20, C7"],
        ["Seconda esecuzione, disinstallazione e reinstallazione non perdono dati", "C1-C3"],
        ["Il ripristino su un'altra macchina restituisce i dati con riepilogo pulito", "D1-D4"],
    ], [0.78, 0.22])
    s += ESAMINARE("Se una riga di B è KO, correggere l'installatore e <b>ripartire dall'istantanea pulita</b>: una prova su una macchina già parzialmente installata non è attendibile. "
                   "Questa lista non prova la sicurezza del programma né la conformità giuridica: prova che l'installazione fa ciò che dice.")
    return s

if __name__ == "__main__":
    doc = Doc(sys.argv[1] if len(sys.argv) > 1 else "Lista-di-prova.pdf")
    doc.flat = True
    doc.footer = "Lista di prova dell'installazione - Privacy Studio 0.9.0 - novembre 2026"
    doc.title = "Privacy Studio - Lista di prova dell'installazione"
    # senza copertina: si parte direttamente dal modello «main»
    doc.pageTemplates = [t for t in doc.pageTemplates if t.id == "main"]
    doc.build(story())
