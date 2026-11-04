# -*- coding: utf-8 -*-
from lib import *

def story():
    s = []
    # ---------------------------------------------------------------- 7
    s += H1("7. Procedure operative")
    s += H2("7.1 Che cosa fare, e quando")
    s += [P("Le cadenze sono <b>proposte</b>, da adattare all'organizzazione dello studio.")]
    s += TABLE([
        ["Quando", "Che cosa", "Dove"],
        ["Ogni giorno", "Guardare le schede <b>Richieste in scadenza</b> e <b>Incidenti aperti</b>. Registrare le richieste ricevute il giorno stesso. A fine giornata: <b>Esegui backup ora</b> e controllare che sia \"Riuscito\".", "§ 6.5, 6.6, 6.8"],
        ["Per ogni nuovo paziente", "Consegnare l'informativa e registrare la consegna (anche \"senza firma\").", "§ 6.3"],
        ["Ogni settimana", "Controllare che i file di backup siano presenti anche nella copia esterna; <b>Verifica un file…</b> su uno di essi.", "§ 6.8"],
        ["Ogni mese", "Guardare gli <b>elementi scaduti</b> nella scheda Conservazione e, se il titolare lo decide, eliminarli. Controllare le attività con riesame scaduto nel Registro.", "§ 6.4, 6.7"],
        ["Ogni sei mesi", "<b>Prova di ripristino</b> su un computer diverso, con la sola copia esterna e il file delle chiavi di recupero.", "§ 7.4"],
        ["Ogni anno", "Riesaminare il registro, le informative, le regole di conservazione, l'elenco dei fornitori e delle persone autorizzate; formare chi lavora nello studio.", "Cap. 8"],
        ["Quando cambia qualcosa", "Nuovo trattamento, nuovo fornitore, cambio di sede o di software: aggiornare il Registro (nuova revisione) e valutare se l'informativa va rivista.", "§ 6.4"],
    ], [0.20, 0.64, 0.16])

    s += H2("7.2 Una richiesta di un paziente, dall'inizio alla fine")
    s += STEPS([
        "Il paziente chiede, per email, copia dei propri dati. Lo stesso giorno: <b>Nuova richiesta…</b>, qualità \"Interessato\", canale \"Email\", diritto \"Accesso\". Annotare il riferimento assegnato.",
        "Verificare che si tratti davvero della persona (se c'è un dubbio ragionevole, chiedere informazioni supplementari: art. 12, par. 6) e annotarlo nella cronologia o a parte.",
        "Preparare il riscontro fuori dal programma (copia dei dati, informazioni richieste dall'art. 15).",
        "Se serve più tempo per complessità, <b>prima della scadenza</b> usare <b>Proroga…</b> con la motivazione e comunicarla al paziente entro un mese dalla richiesta.",
        "Inviato il riscontro: <b>Decisione…</b> per il diritto, esito \"Accolta\" (o altro con motivazione), indicando la data di invio.",
        "<b>Chiudi richiesta</b>. Il programma conserva cronologia, scadenza iniziale ed eventuale proroga.",
    ])

    s += H2("7.3 Una violazione dei dati, dalla scoperta alla chiusura")
    s += ATTENZIONE("Il tempo conta: le 72 ore decorrono da quando il titolare ne viene a conoscenza. Annotare subito, anche su carta, <b>data e ora</b> in cui si è capito che qualcosa è successo.")
    s += STEPS([
        "<b>Contenere</b>: scollegare il computer dalla rete, cambiare le password, bloccare l'account compromesso, recuperare il dispositivo se smarrito. Non cancellare tracce utili.",
        "<b>Registrare</b>: <b>Nuovo incidente…</b> con data e ora di rilevazione, descrizione e, se già accertata, la consapevolezza. L'orologio delle 72 ore compare in <b>Incidenti aperti</b>.",
        "<b>Valutare</b>: <b>Valutazione…</b>. Che dati sono coinvolti? Quante persone? Con quali conseguenze probabili? Il rischio per i diritti e le libertà è improbabile, presente, elevato? Nei casi dubbi, sentire il consulente.",
        "<b>Decidere</b> e registrare: notificare al Garante? comunicare agli interessati? Se si decide di non notificare, scrivere perché (art. 33, par. 5).",
        "<b>Notificare</b>, se dovuto, attraverso la procedura messa a disposizione dal Garante, entro le 72 ore (altrimenti con i motivi del ritardo). Poi <b>Registra azione…</b> di tipo \"Notifica al Garante\" con il <b>numero di protocollo o la ricevuta</b>.",
        "<b>Comunicare agli interessati</b>, se il rischio è elevato e non ricorre un'eccezione; registrare l'azione.",
        "<b>Rimediare e chiudere</b>: registrare le azioni correttive, rivedere le misure di sicurezza, poi <b>Chiudi incidente</b>.",
    ])

    s += H2("7.4 Ripristino dopo un guasto o su un nuovo computer")
    s += ATTENZIONE("Il ripristino va <b>provato prima</b> di averne bisogno (ogni sei mesi, § 7.1). Un backup mai ripristinato è una speranza, non una garanzia. "
                    "Finora è stato provato solo con un database di prova in ambiente di sviluppo, <b>non su un computer Windows pulito</b> (punto 16 del cap. 9).")
    s += [P("Il ripristino crea sempre un database <b>nuovo</b>: non sovrascrive mai uno esistente. Servono: il file di backup (<font name='UM'>.psbk</font>) con il suo file dell'elenco eliminazioni (<font name='UM'>.ledger.jsonl</font>), il file delle chiavi di recupero con la sua frase, PostgreSQL e il programma installati sul nuovo computer.")]
    s += STEPS([
        "Installare PostgreSQL e il programma (cap. 4) <b>senza creare lo schema</b>; indicare <font name='UM'>pgBinDir</font> in <font name='UM'>config.json</font>.",
        "Reinstallare le chiavi: <b>Importa chiavi…</b> (§ 5.3). Senza le chiavi il file non si apre.",
        "Copiare in una cartella il backup e il suo <font name='UM'>.ledger.jsonl</font>.",
        "Da un terminale, con la stringa dell'amministratore del database nella variabile d'ambiente (non sulla riga di comando):",
    ])
    s += CODE("""set PRIVACY_ADMIN_CONN=Host=127.0.0.1;Port=5432;Database=postgres;
Username=postgres;Password=...   (tutto su una riga)
Privacy.App.exe --restore "D:\\Ripristino\\privacy-....psbk" --database privacy""")
    s += [P("Il programma, in quest'ordine: verifica che il file si decifri; crea il database e i ruoli; ripristina i dati; <b>rielimina ciò che il backup aveva riportato</b> usando l'elenco delle eliminazioni; "
            "controlla la catena del registro di audit; registra la prova di ripristino. Alla fine mostra un riepilogo. L'esito è pieno solo se la catena è integra, non ci sono eliminazioni da verificare a mano e l'elenco delle eliminazioni è stato fornito.")]
    s += STEPS([
        "Impostare le password dei ruoli <font name='UM'>privacy_app</font> e <font name='UM'>privacy_migrator</font> (non sono nel backup) e aggiornare <font name='UM'>connection.txt</font> perché punti al nuovo database.",
        "Accedere e controllare i dati. <b>Non riprendere l'attività normale</b> finché il riepilogo non è pulito.",
    ])
    s += ATTENZIONE("Se l'elenco delle eliminazioni non è stato fornito, i dati cancellati dopo la data del backup possono essere di nuovo visibili. Recuperare il file più recente ed eseguire "
                    "<font name='UM'>Privacy.App.exe --reapply-disposals</font> <b>prima</b> di riprendere l'uso.")

    # ---------------------------------------------------------------- 8
    s += H1("8. Sicurezza e responsabilità")
    s += H2("8.1 Che cosa fa il programma per la sicurezza")
    s += TABLE([
        ["Misura", "Come è realizzata", "Esigenza (art. 32)"],
        ["Accesso controllato", "Password con derivazione PBKDF2-SHA256 (600.000 iterazioni, sale casuale); blocco dopo 5 tentativi per 15 minuti, che resiste al riavvio; messaggi uniformi.", "Riservatezza"],
        ["Separazione dei ruoli del database", "Il ruolo operativo non può modificare la struttura né alterare audit, versioni pubblicate ed eventi.", "Riservatezza, integrità"],
        ["Registro di audit a catena", "Ogni operazione importante è registrata e legata alla precedente da un codice di controllo; modifiche o cancellazioni successive si rilevano. La catena è verificata a ogni ripristino.", "Integrità"],
        ["Versioni immutabili", "Registro e informative pubblicati, consegne e prove non si modificano; si crea una nuova versione.", "Integrità"],
        ["Backup cifrati e verificati", "AES-256-GCM; file riletto e decifrato dal disco; copia esterna; elenco delle eliminazioni riapplicabile.", "Disponibilità, ripristino"],
        ["Moduli con impronta con chiave", "Codice di controllo HMAC-SHA256 del modulo consegnato, per dimostrare che non è stato alterato.", "Integrità"],
        ["Dati ridotti al minimo", "Nessun dato clinico; nei file non si conserva il nome originale.", "Minimizzazione (art. 5)"],
        ["Errori e registri senza dati", "Gli errori tecnici non mostrano dettagli; le password non compaiono mai in audit o messaggi.", "Riservatezza"],
    ], [0.24, 0.52, 0.24])
    s += H2("8.2 Limiti dichiarati")
    s += BUL([
        "L'amministratore del database e il ruolo proprietario possono aggirare le protezioni applicative: è un limite di qualunque sistema, mitigato dalla separazione dei ruoli e dal registro di audit. Chi ha quei poteri va individuato e vincolato (nomina, cap. 3).",
        "Le chiavi sono custodite da Windows (DPAPI) per l'utente che lavora: protette da chi non conosce la sua password, <b>non</b> da chi la conosce. Vedi § 9, punto 1.",
        "La password del database sta in un file dell'utente (§ 4.4). L'elenco esterno delle eliminazioni non è cifrato né firmato (contiene solo identificativi).",
        "Non esiste ancora una schermata per consultare il registro di audit; la verifica della catena avviene al ripristino.",
        "Il programma non è progettato per inviare dati all'esterno; l'<b>assenza</b> di connessioni in uscita e l'inaccessibilità del database dalla rete <b>non sono ancora state verificate con una prova automatica</b>.",
    ])
    s += H2("8.3 Che cosa resta al titolare")
    s += TABLE([
        ["Adempimento", "Riferimento", "Nel programma?"],
        ["Designare e istruire le persone autorizzate (segretaria, collaboratori)", "Art. 29 GDPR; art. 2-quaterdecies Codice", "No"],
        ["Nominare i responsabili del trattamento (informatico, hosting, commercialista, laboratori, …)", "Art. 28 GDPR", "No (previsto)"],
        ["Valutare se servono DPO e valutazione d'impatto", "Artt. 35, 37 GDPR", "No"],
        ["Verificare e rivedere le misure di sicurezza dello studio", "Art. 32 GDPR", "In parte (backup, accesso)"],
        ["Redigere e approvare l'informativa", "Artt. 12-14 GDPR", "Schema e pubblicazione"],
        ["Gestire consensi e revoche per finalità diverse dalla cura", "Art. 6, par. 1, lett. a); art. 7", "No (previsto)"],
        ["Tenere la documentazione clinica e stabilirne la conservazione", "Obblighi professionali e art. 5", "No"],
        ["Formare chi lavora nello studio", "Art. 29, art. 32, par. 4", "No"],
    ], [0.56, 0.28, 0.16])
    s += NORMA("Il titolare del trattamento e il responsabile del trattamento fanno sì che chiunque agisca sotto la loro autorità e abbia accesso a dati personali non tratti tali dati se non è istruito in tal senso.", "Reg. UE 2016/679, art. 32, par. 4 e art. 29")

    # ---------------------------------------------------------------- 9
    s += H1("9. Punti da esaminare")
    s += [P("Raccolta di tutti i punti segnalati nel manuale come incerti o rimessi a una scelta. Per ciascuno è indicato <b>chi dovrebbe decidere</b>. "
            "Le norme citate vanno controllate nel testo vigente al momento dell'uso. Questa tabella è il primo documento da portare al consulente.")]
    rows = [["N.", "Argomento", "Perché è da esaminare", "Chi decide", "Rif."]]
    items = [
        ("1", "Protezione di chiavi, database e credenziali", "Le chiavi sono custodite da Windows per l'utente; la password del database è in un file. Manca una scelta definitiva fra cifratura del disco, cifratura dei campi e gestione delle chiavi, con una procedura di recupero provata da una persona non tecnica.", "Medico con esperto di sicurezza", "§ 4.4, 5.3"),
        ("2", "Calcolo dei termini e proroga", "Applicazione del Reg. 1182/71 al termine dell'art. 12 GDPR; slittamento a giorno lavorativo; proroga massima di tre mesi complessivi oppure uno più due.", "Consulente", "§ 6.5, App. B"),
        ("3", "DPO e valutazione d'impatto", "Per il singolo professionista sanitario di norma non obbligatori (considerando 91; FAQ del Garante), ma dipende da organizzazione e dimensioni dello studio.", "Consulente", "§ 2.3"),
        ("4", "Consensi per finalità diverse dalla cura", "Marketing, ricerca, fascicolo sanitario ecc.: quali trattamenti dello studio li richiedono; la gestione non è nel programma.", "Consulente", "§ 2.2"),
        ("5", "Testo dell'informativa", "Il programma fornisce solo uno schema. Contenuto e completezza sono del titolare; eventuale norma specifica per medici di base e pediatri (art. 78 Codice).", "Medico, consulente", "§ 6.3"),
        ("6", "Durate di conservazione", "Documentazione clinica (non gestita dal programma), documenti fiscali (art. 2220 c.c. e normativa tributaria), richieste, incidenti, audit.", "Medico, consulente, commercialista", "§ 6.7"),
        ("7", "Rappresentanti legali e minori", "Come documentare i poteri di chi ritira per un minore o una persona sotto tutela; la schermata non esiste ancora.", "Consulente", "§ 6.3"),
        ("8", "Accesso alla documentazione sanitaria", "Termini specifici della L. 24/2017, art. 4, e rapporto con l'art. 12 GDPR per lo studio individuale.", "Consulente", "§ 6.5"),
        ("9", "Persone autorizzate e responsabili", "Come formalizzare l'assistenza informatica e gli altri fornitori; chi accede ai dati e a che titolo.", "Medico, consulente", "§ 3.3, 8.3"),
        ("10", "Verifica dell'identità del richiedente", "Art. 12, par. 6: non registrata nell'interfaccia; come documentarla.", "Medico", "§ 6.5"),
        ("11", "Consapevolezza e notifica delle violazioni", "Quale istante contare come \"conoscenza\"; modalità vigenti della procedura telematica del Garante; soglie di rischio.", "Medico, consulente", "§ 6.6"),
        ("12", "Completezza del registro (art. 30)", "Mancano nell'editor sistemi, misure per attività, fornitori, trasferimenti verso paesi terzi.", "Medico", "§ 6.4"),
        ("13", "Misure di garanzia per dati sanitari", "L'art. 2-septies del Codice prevede misure di garanzia per i dati relativi alla salute da adottare con provvedimento del Garante: stato e contenuto vigenti da verificare.", "Consulente", "§ 2.1"),
        ("14", "Obiettivi di backup", "Perdita massima di 24 ore e ripristino in un giorno lavorativo sono proposte, non obblighi.", "Medico", "§ 6.8"),
        ("15", "Aggiornamento del programma", "Procedura per applicare nuove versioni dello schema, con copia di sicurezza prima, da scrivere e provare.", "Sviluppo, medico", "§ 4.3"),
        ("16", "Installazione e ripristino su Windows pulito", "Il programma di installazione è solo in bozza: provato in \"prova a secco\", non con l'installazione reale di PostgreSQL; il ripristino è provato in ambiente di sviluppo.", "Sviluppo", "§ 4, 7.4"),
        ("17", "Tema dell'interfaccia", "Basato su una funzione sperimentale di .NET 10; provato su Windows 11, non su Windows 10, scale diverse dal 100% e contrasto elevato.", "Sviluppo", "§ 6.1"),
        ("18", "Firma digitale dell'installatore", "Senza certificato di firma Windows mostra l'avviso \"editore sconosciuto\". Decisione rimandata.", "Medico", "§ 4.0"),
        ("19", "Ruolo del medico in strutture, associazioni e piattaforme", "Titolare autonomo, contitolare, responsabile o persona autorizzata: dipende da contratti e organizzazione.", "Consulente", "§ 0.1"),
        ("20", "Trasferimenti fuori dall'Unione per servizi in cloud", "Validità delle decisioni di adeguatezza e delle garanzie contrattuali per ogni servizio con dati sanitari.", "Consulente", "§ 0.3.7"),
        ("21", "Telemedicina, FSE, videosorveglianza, sito web", "Regole specifiche e possibile DPIA, non coperte dal programma.", "Consulente", "§ 0.3.11-12"),
    ]
    rows += [list(i) for i in items]
    s += TABLE(rows, [0.05, 0.22, 0.43, 0.20, 0.10])

    # ---------------------------------------------------------------- 10
    s += H1("10. Stato delle funzioni e condizioni di rilascio", newpage=False)
    s += H2("10.1 Che cosa è disponibile oggi")
    s += TABLE([
        ["Funzione", "Disponibile", "Non ancora disponibile"],
        ["F01 Registro", "Attività, revisioni, finalità e basi giuridiche, categorie, pubblicazione con controlli, esportazione alla data.", "Sistemi, misure per attività, fornitori, trasferimenti; conservazione per finalità nell'esportazione."],
        ["F02 Informative e consegne", "Informative con variabili, pubblicazione, consegna e storico, verifica dell'integrità del modulo.", "Stampa e PDF; acquisizione della scansione della ricevuta firmata; rappresentanti legali."],
        ["F03 Richieste", "Registrazione, scadenza, proroga, decisione, chiusura, anche da non pazienti.", "Verifica dell'identità; collegamento alla scheda del paziente; allegati."],
        ["F04 Violazioni", "Registrazione, orologio delle 72 ore, valutazione, azioni, protocollo di notifica, chiusura.", "Allegati e prove; rilettura delle valutazioni nel dettaglio."],
        ["F05 Conservazione", "Regole con approvazione, elenco degli scaduti con motivo, eliminazione guidata, registro riapplicabile.", "Documentazione clinica (fuori dal programma)."],
        ["F06 Backup e ripristino", "Backup cifrato verificato con copia esterna; verifica file; chiavi di recupero; ripristino da riga di comando; riapplicazione eliminazioni.", "Pianificazione automatica; prova su Windows pulito."],
        ["F07 Rimozione interessato", "Rimozione con anonimizzazione delle prove di consegna.", "—"],
        ["Accesso", "Accesso con password, blocco persistente, audit.", "Cambio password in interfaccia; scadenza della sessione; più utenti e ruoli."],
        ["Installazione", "Prima configurazione guidata; script del database; programma di installazione con PostgreSQL incluso (bozza).", "Prova dell'installatore su Windows pulito; firma digitale; aggiornamenti automatici; modifica dei dati dello studio."],
    ], [0.20, 0.46, 0.34])
    s += H2("10.2 Condizioni prima di usare dati reali")
    s += [P("Condizioni di progetto basate sul rischio (non un certificato di conformità). Se una non è soddisfatta, si continua a provare con dati inventati.")]
    s += STEPS([
        "Configurazione effettiva dello studio e dei destinatari/fornitori.",
        "Informative approvate dal titolare (e, se coinvolto, dal consulente).",
        "Durate e regole di conservazione decise e registrate.",
        "Regola dei termini confermata dal consulente.",
        "Decisione sulla protezione di chiavi e database presa e rischio residuo registrato (punto 1 del cap. 9).",
        "Prova dei permessi del database e della catena di audit; ripristino riuscito su una macchina pulita, con le chiavi di recupero consegnate e verificate.",
        "Procedura per le violazioni scritta e provata con un caso simulato.",
        "Responsabilità di manutenzione e aggiornamenti assegnate, compresa la nomina di chi può accedere ai dati.",
    ])

    # ---------------------------------------------------------------- Appendici
    s += H1("Appendice A - Glossario")
    s += TABLE([
        ["Termine", "Significato"],
        ["<b>Titolare del trattamento</b>", "Chi decide finalità e mezzi del trattamento: qui il medico o lo studio (art. 4, n. 7)."],
        ["<b>Interessato</b>", "La persona a cui i dati si riferiscono: il paziente, ma anche un collaboratore o un fornitore."],
        ["<b>Dati relativi alla salute</b>", "Dati sulla salute fisica o mentale, anche relativi alla prestazione di servizi sanitari (art. 4, n. 15). Categoria particolare (art. 9)."],
        ["<b>Responsabile del trattamento</b>", "Chi tratta dati per conto del titolare (art. 28): per esempio il fornitore informatico."],
        ["<b>Persona autorizzata</b>", "Chi tratta dati sotto l'autorità del titolare (art. 29; art. 2-quaterdecies del Codice)."],
        ["<b>DPO / RPD</b>", "Responsabile della protezione dei dati (artt. 37-39), nominato nei casi previsti."],
        ["<b>DPIA</b>", "Valutazione d'impatto sulla protezione dei dati (art. 35)."],
        ["<b>Violazione dei dati</b>", "Violazione di sicurezza che comporta, anche accidentalmente, distruzione, perdita, modifica, divulgazione o accesso non autorizzati (art. 4, n. 12)."],
        ["<b>Base giuridica</b>", "La ragione per cui un trattamento è lecito (art. 6; per i dati sulla salute anche art. 9)."],
        ["<b>Informativa</b>", "Il documento con cui si informa l'interessato su come sono trattati i suoi dati (artt. 13-14)."],
        ["<b>Garante</b>", "Autorità garante per la protezione dei dati personali."],
        ["<b>EDPB</b>", "Comitato europeo per la protezione dei dati, che emana linee guida."],
        ["<b>Revisione</b>", "Versione numerata di un'attività del registro o di un documento. Una volta pubblicata non si modifica."],
        ["<b>Registro di audit</b>", "Elenco delle operazioni, legate l'una all'altra da un codice di controllo, in modo che ogni manomissione si noti."],
        ["<b>HMAC</b>", "Codice di controllo calcolato con una chiave segreta: permette di verificare che un testo non sia cambiato."],
        ["<b>Backup cifrato</b>", "Copia dei dati resa illeggibile senza la chiave (AES-256-GCM)."],
        ["<b>Elenco delle eliminazioni</b>", "File esterno con tipo e identificativo di ciò che è stato eliminato, per rieliminarlo dopo un ripristino."],
    ], [0.28, 0.72])

    s += H1("Appendice B - Esempi di calcolo dei termini")
    s += [P("Regola <font name='UM'>it-1182-v1</font> (da confermare, § 6.5): il giorno di ricezione non conta; scadenza nel giorno corrispondente del mese successivo; se manca, ultimo giorno del mese; "
            "se cade di sabato, domenica o festività nazionale, primo giorno lavorativo successivo. Festività considerate: 1 gennaio, 6 gennaio, lunedì di Pasqua, 25 aprile, 1 maggio, 2 giugno, 15 agosto, 1 novembre, 8 dicembre, 25 e 26 dicembre (più eventuali festività locali inserite dallo studio).")]
    s += TABLE([
        ["Ricevuta il", "Scadenza", "Motivo"],
        ["lunedì 16/03/2026", "giovedì 16/04/2026", "Caso ordinario."],
        ["sabato 31/01/2026", "lunedì 02/03/2026", "Il 28/02/2026 è sabato: slitta al primo giorno lavorativo."],
        ["lunedì 31/01/2028 (anno bisestile)", "martedì 29/02/2028", "Il giorno corrispondente non esiste: ultimo giorno del mese."],
        ["lunedì 30/11/2026", "mercoledì 30/12/2026", "Mese con meno giorni."],
        ["giovedì 31/12/2026", "lunedì 01/02/2027", "Il 31/01/2027 è domenica."],
        ["giovedì 15/10/2026", "lunedì 16/11/2026", "Il 15/11/2026 è domenica."],
        ["martedì 25/03/2025", "lunedì 28/04/2025", "Il 25/04 è festivo, il 26 e 27 sono sabato e domenica."],
        ["sabato 31/01/2026 con proroga massima", "giovedì 30/04/2026", "Tre mesi complessivi dalla ricezione, con la stessa regola (punto 2 del cap. 9)."],
    ], [0.34, 0.26, 0.40])
    s += CONSIGLIO("Finché la regola non è confermata dal consulente, rispondere <b>prima</b> della scadenza mostrata: nessuna delle interpretazioni plausibili è più favorevole al ritardo.")

    s += H1("Appendice C - Lista di controllo per l'avvio")
    s += TABLE([
        ["", "Voce", "Fatto il / da"],
        ["[&nbsp;&nbsp;&nbsp;]", "Computer dedicato, aggiornato, disco cifrato, account personale, blocco schermo automatico", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "PostgreSQL installato, in ascolto solo su localhost, firewall senza regola per la porta 5432", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Database e ruoli creati, script 000-004 eseguiti senza errori, password dei ruoli annotate", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "<font name='UM'>connection.txt</font> e <font name='UM'>config.json</font> configurati (copia esterna su supporto diverso)", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Prima configurazione dello studio completata; accesso riuscito", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Chiavi di recupero esportate, file e frase conservati fuori dal PC e separatamente", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Primo backup riuscito e verificato, presente anche nella copia esterna", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Prova di ripristino su un altro computer eseguita e registrata", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Registro: attività principali pubblicate", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Informativa completata, approvata e pubblicata", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Regole di conservazione decise e approvate", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Persone autorizzate designate; fornitori nominati responsabili", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Procedura per le violazioni letta da tutti e provata con un caso simulato", ""],
        ["[&nbsp;&nbsp;&nbsp;]", "Punti del cap. 9 portati al consulente e risolti o accettati con rischio registrato", ""],
    ], [0.07, 0.68, 0.25])

    s += H1("Appendice D - Riferimenti normativi e documenti")
    s += H2("Norme citate")
    s += BUL([
        "<b>Regolamento (UE) 2016/679</b>: artt. 4, 5, 6, 7, 9, 12, 13, 14, 15, 16, 17, 18, 20, 21, 24, 28, 29, 30, 32, 33, 34, 35, 37 e considerando 91.",
        "<b>D.Lgs. 30 giugno 2003, n. 196</b> (Codice in materia di protezione dei dati personali), come modificato dal <b>D.Lgs. 10 agosto 2018, n. 101</b>: artt. 2-terdecies, 2-quaterdecies, 2-septies; Parte II, Titolo V (artt. 75 e seguenti), in particolare art. 78.",
        "<b>Regolamento (CEE, Euratom) n. 1182/71</b> del Consiglio, sulle norme applicabili ai periodi di tempo, alle date e ai termini.",
        "<b>Legge 8 marzo 2017, n. 24</b>, art. 4 (trasparenza dei dati sanitari).",
        "<b>Codice civile</b>, art. 2220 (conservazione delle scritture contabili).",
        "<b>Provvedimento del Garante del 7 marzo 2019</b>, sul trattamento dei dati relativi alla salute in ambito sanitario; <b>FAQ del Garante</b> sul responsabile della protezione dei dati in ambito sanitario; <b>linee guida dell'EDPB</b> sulla notifica delle violazioni dei dati personali.",
    ])
    s += ESAMINARE("L'elenco è quello dei riferimenti usati per costruire il programma e il manuale. Il testo, i numeri e la vigenza dei provvedimenti vanno <b>controllati alla fonte ufficiale</b> (Gazzetta Ufficiale, EUR-Lex, sito del Garante, sito dell'EDPB) prima di fondarci decisioni.")
    s += H2("Documenti tecnici del progetto")
    s += TABLE([
        ["Documento", "Contenuto"],
        ["01 - Analisi normativa", "Obblighi, loro classificazione (obbligo di legge, scelta di progetto, decisione del titolare), decisioni aperte."],
        ["02 - Specifica funzionale e dati", "Flussi F01-F07, struttura dei dati, regole del database."],
        ["03 - Piano di test e rilascio", "Prove eseguite e da eseguire; condizioni di rilascio con dati reali."],
        ["04 - Procedura di backup e ripristino", "Dettaglio tecnico di backup, chiavi e ripristino."],
    ], [0.34, 0.66])
    s += H2("Registro delle modifiche del manuale")
    s += TABLE([
        ["Versione", "Data", "Modifica"],
        ["0.9", "novembre 2026", "Prima stesura, bozza per revisione. Riferita al programma in pre-rilascio."],
    ], [0.15, 0.25, 0.60])
    return s
