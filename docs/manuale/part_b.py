# -*- coding: utf-8 -*-
from lib import *

def story():
    s = []
    # ---------------------------------------------------------------- 3
    s += H1("3. Prima di installare")
    s += H2("3.1 Requisiti")
    s += TABLE([
        ["Elemento", "Requisito", "Note"],
        ["Sistema operativo", "Windows 10 o 11, 64 bit, con gli aggiornamenti installati", "Un computer dedicato allo studio, non usato per altro."],
        ["Memoria e spazio", "8 GB di memoria; almeno 5 GB liberi per il programma e il database", "I dati del programma sono piccoli (anagrafiche, registri, prove di consegna); lo spazio per i backup va previsto a parte."],
        ["Database", "PostgreSQL 17 (provato con la 17)", "Installato sullo stesso computer, in ascolto solo su quel computer (§ 4.2)."],
        ["Ambiente di esecuzione", "Incluso nel programma", "Il programma di installazione porta con sé .NET: non va installato a parte."],
        ["Supporto per la copia esterna", "Disco esterno o altra macchina", "<b>Indispensabile</b>: un backup sullo stesso disco non protegge da guasto, furto o incendio."],
        ["Supporto per le chiavi di recupero", "Chiavetta o altro, custodito fuori dal PC", "Vedi § 5.3."],
    ], [0.22, 0.38, 0.40])
    s += H2("3.2 Sicurezza del computer")
    s += [P("Il programma protegge i propri dati (accesso con password, registro di audit, backup cifrati), ma non può compensare un computer non sicuro. "
            "L'art. 32 del Regolamento chiede misure tecniche e organizzative adeguate al rischio; per un computer che contiene dati sulla salute, almeno:")]
    s += BUL([
        "<b>Cifratura del disco</b> (BitLocker o equivalente): se il computer viene rubato, i dati non sono leggibili.",
        "<b>Aggiornamenti</b> automatici di Windows e dei programmi; antivirus attivo.",
        "<b>Account Windows personale</b> con password; nessun account condiviso; blocco schermo automatico.",
        "<b>Nessuna esposizione in rete</b> del database (§ 4.2) e firewall attivo.",
        "<b>Backup</b> regolari su un supporto separato (cap. 5 e 6.8).",
        "Locali e dispositivi dello studio: chi può vedere lo schermo, dove sono i supporti dei backup.",
    ])
    s += NORMA("Il titolare e il responsabile mettono in atto misure adeguate che comprendono, se del caso: la pseudonimizzazione e la cifratura dei dati personali; la capacità di assicurare "
               "riservatezza, integrità, disponibilità e resilienza dei sistemi; la capacità di ripristinare tempestivamente la disponibilità dei dati in caso di incidente; "
               "una procedura per provare e valutare regolarmente l'efficacia delle misure.", "Reg. UE 2016/679, art. 32, par. 1")
    s += H2("3.3 Che cosa preparare prima di iniziare")
    s += TABLE([
        ["Che cosa", "Perché serve", "Chi lo decide o lo fornisce"],
        ["Denominazione, titolare, indirizzo, contatto per le richieste privacy (email o PEC), telefono", "Compaiono nelle informative.", "Il medico"],
        ["Eventuale DPO e relativo contatto", "Va indicato nell'informativa se nominato.", "Il medico, con il consulente"],
        ["Elenco di chi lavora nello studio e dei fornitori che vedono dati (informatico, commercialista, laboratori, software)", "Per le designazioni e le nomine; per i destinatari da indicare nell'informativa.", "Il medico"],
        ["Durate di conservazione per i diversi tipi di documentazione", "Il programma non ne propone: vanno inserite dal titolare (§ 6.7).", "Il medico, con il consulente"],
        ["Testo dell'informativa", "Il programma offre solo uno schema da completare (§ 6.3).", "Il medico, con il consulente"],
        ["Una persona di fiducia per l'installazione e l'assistenza", "L'installazione tocca database e Windows.", "Il medico"],
    ], [0.40, 0.32, 0.28])
    s += ESAMINARE("<b>Assistenza informatica.</b> Chi installa e mantiene il programma, se può accedere ai dati (anche solo in teoria), svolge un trattamento per conto dello studio: "
                   "serve un atto di nomina a responsabile del trattamento (art. 28) o, se è una persona che lavora sotto l'autorità del titolare, una designazione (art. 29 e art. 2-quaterdecies). "
                   "Quale delle due ricorra e come formalizzarla va deciso con il consulente.")

    # ---------------------------------------------------------------- 4
    s += H1("4. Installazione")
    s += H2("4.0 Installazione guidata (consigliata)")
    s += [P("Il programma di installazione <b>PrivacyStudio-Setup-0.9.4.exe</b> fa in un solo passaggio ciò che i punti 4.1-4.5 descrivono a mano. Va eseguito <b>dall'utente Windows che userà il programma</b> (con i permessi di amministratore, che Windows chiede), "
            "perché i file di connessione e le chiavi sono personali di quell'utente.")]
    s += STEPS([
        "Avviare il programma di installazione e confermare la richiesta di Windows. Scegliere la cartella del programma (di norma quella proposta).",
        "<b>Password del database</b>: scegliere la password dell'amministratore (<font name='UM'>postgres</font>), di almeno 10 caratteri, scrivendola due volte. La casella <b>Mostra le password</b> permette di controllare quanto si è scritto. Annotarla in un luogo sicuro: serve per il ripristino dei backup e per la manutenzione. Se PostgreSQL 17 è già presente, il programma chiede la password esistente (non la salva).",
        "<b>Backup</b>: indicare la cartella dei backup sul computer e la <b>copia esterna</b> (disco esterno o altra macchina; il pulsante Sfoglia aiuta a sceglierla). La copia esterna è consigliata ma <b>si può rimandare</b> spuntando «Non indicare ora la copia esterna»: il programma chiede conferma e ricorda il rischio. Avvisa anche se la copia esterna è sullo stesso disco di Windows.",
        "Attendere: l'installazione di PostgreSQL richiede qualche minuto. Il programma installa PostgreSQL 17 (se manca) tenendolo in ascolto solo su questo computer, crea il database e i due ruoli con <b>password casuali generate da lui</b>, applica gli script dello schema, scrive i file di configurazione e crea i collegamenti.",
        "Al termine avviare Privacy Studio: compare la <b>Prima configurazione</b> (§ 5.1).",
    ])
    s += BUL([
        "Le password dei due ruoli del database non vanno mai digitate dall'utente: la password del ruolo operativo è nel file <font name='UM'>connection.txt</font> dell'utente; quella del ruolo di installazione è in un file riservato agli amministratori, nella cartella dei dati comuni di Windows.",
        "Se un tentativo precedente si è interrotto, il programma può trovare i residui di PostgreSQL (cartella, servizio o utente <font name='UM'>postgres</font>). Se non c'è <b>nessun database</b> da conservare, chiede il permesso e li rimuove da solo; se trova un database si ferma senza toccare nulla.",
        "Se il database dello studio esiste già (aggiornamento del programma), l'installazione <b>non tocca i dati</b>. L'aggiornamento dello schema non è ancora automatico (punto 15 del cap. 9).",
        "La <b>disinstallazione</b> rimuove il programma ma <b>non</b> il database, i backup e le chiavi, che contengono dati dello studio.",
        "Per l'assistenza tecnica: l'installazione può essere non interattiva con i parametri <font name='UM'>/VERYSILENT /PGPASSWORD=… /EXTERNALDIR=…</font>; con <font name='UM'>/DRYRUN=1</font> mostra cosa farebbe senza modificare nulla.",
    ])
    s += ATTENZIONE("Il programma di installazione è una <b>bozza non ancora provata su un computer Windows pulito</b>. È stato compilato e verificato soltanto in \"prova a secco\" (sequenza dei passaggi, copia dei file, messaggi), ma l'installazione reale di PostgreSQL e la creazione del database con le password generate "
                    "<b>non sono state eseguite</b>. Fino a quella prova, usare la procedura manuale qui sotto solo con l'aiuto dell'assistenza e senza dati reali. "
                    "Il programma non è <b>firmato digitalmente</b>: Windows mostrerà l'avviso \"editore sconosciuto\".")
    s += H2("Installazione manuale (riferimento tecnico)")
    s += [P("I punti seguenti descrivono i passaggi che il programma di installazione automatizza. Servono per capire cosa succede, per l'assistenza e come alternativa.")]
    s += H2("4.1 Installare i componenti (manuale)")
    s += STEPS([
        "Installare <b>PostgreSQL 17</b> (programma di installazione di EDB). Durante l'installazione si sceglie la password dell'utente amministratore del database (<font name='UM'>postgres</font>): annotarla in un luogo sicuro. Lasciare la porta predefinita (5432).",
        "Il programma pubblicato in modalità autonoma (<font name='UM'>--self-contained true</font>) include .NET: non serve installarlo a parte.",
        "Copiare la cartella del programma (<font name='UM'>Privacy.App</font> e le sue librerie) in una cartella dello studio, ad esempio <font name='UM'>C:\\Programmi\\PrivacyStudio</font>.",
    ])
    s += ESAMINARE("La cartella del programma si ottiene oggi compilando il codice (<font name='UM'>dotnet publish src\\Privacy.App -c Release -r win-x64 --self-contained true</font>). "
                   "Questo passaggio e l'avvio della versione Release su un computer pulito non sono ancora stati provati. Un installatore firmato è un obiettivo da realizzare prima del rilascio.")
    s += H2("4.2 Tenere il database chiuso verso la rete")
    s += [P("Il database deve accettare connessioni <b>solo dal computer stesso</b>. Nell'installazione standard di PostgreSQL è già così, ma va controllato:")]
    s += BUL([
        "nel file <font name='UM'>postgresql.conf</font> la riga <font name='UM'>listen_addresses</font> deve valere <font name='UM'>'localhost'</font>;",
        "nel file <font name='UM'>pg_hba.conf</font> le connessioni devono essere ammesse solo da <font name='UM'>127.0.0.1</font> e <font name='UM'>::1</font> con metodo <font name='UM'>scram-sha-256</font>;",
        "il firewall di Windows non deve avere una regola che apra la porta 5432 verso la rete.",
    ])
    s += H2("4.3 Creare il database e i due ruoli")
    s += [P("Lo schema usa due ruoli distinti: <font name='UM'>privacy_migrator</font>, proprietario dello schema, usato solo per installare e aggiornare; "
            "<font name='UM'>privacy_app</font>, usato dal programma, che non può modificare la struttura né alterare il registro di audit e le versioni pubblicate. "
            "Dalla cartella <font name='UM'>db</font> del programma, in un terminale con gli strumenti di PostgreSQL (<font name='UM'>psql</font>) disponibili:")]
    s += CODE("""createdb -U postgres privacy
psql -U postgres -d privacy -v ON_ERROR_STOP=1 -f 000_bootstrap_roles.sql
psql -U postgres -c "ALTER ROLE privacy_migrator PASSWORD '<password lunga 1>'"
psql -U postgres -c "ALTER ROLE privacy_app PASSWORD '<password lunga 2>'"
psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -f 001_schema_v1.sql
psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -f 002_catalogs.sql
psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -f 003_rules_and_grants.sql
psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -f 004_disposal_ledger.sql""")
    s += [P("Le due password dei ruoli sono diverse dalla password del medico nel programma e vanno scelte lunghe e casuali; vanno conservate insieme alle altre credenziali dell'installazione. "
            "Se uno script dà errore, <b>fermarsi</b> e non proseguire: l'opzione <font name='UM'>ON_ERROR_STOP</font> serve a questo.")]
    s += ESAMINARE("Non è ancora definita la procedura di <b>aggiornamento</b> dello schema del database quando uscirà una nuova versione del programma (nuovi script numerati dopo il 004, con una copia di sicurezza prima di applicarli). "
                   "Va scritta e provata prima del rilascio.")
    s += H2("4.4 Dire al programma come raggiungere il database")
    s += [P("Il programma legge i parametri di connessione da un file di testo personale dell'utente Windows, <font name='UM'>%LOCALAPPDATA%\\PrivacyStudio\\connection.txt</font>, con una sola riga:")]
    s += CODE("Host=127.0.0.1;Port=5432;Database=privacy;Username=privacy_app;Password=<password lunga 2>")
    s += ATTENZIONE("Questo file contiene la password del database. Deve stare nella cartella dell'utente (non in una cartella condivisa), e non va copiato né inviato. "
                    "In alternativa si può usare la variabile d'ambiente <font name='UM'>PRIVACY_CONN</font>. La protezione di questa credenziale rientra nella decisione sulla gestione delle chiavi (§ 9, punto 1).")
    s += H2("4.5 Cartelle dei backup e delle eliminazioni")
    s += [P("Il file <font name='UM'>%LOCALAPPDATA%\\PrivacyStudio\\config.json</font> indica dove salvare backup e registro esterno delle eliminazioni, e dove sono gli strumenti di PostgreSQL:")]
    s += CODE("""{ "ledgerPath": "D:\\\\Privacy\\\\disposals.jsonl",
  "backupDir": "D:\\\\Backup\\\\Privacy",
  "externalBackupDir": "E:\\\\CopiaEsterna",
  "pgBinDir": "C:\\\\Program Files\\\\PostgreSQL\\\\17\\\\bin" }""")
    s += BUL([
        "<b>externalBackupDir</b> deve trovarsi su un supporto diverso dal computer (disco esterno, altra macchina).",
        "<b>ledgerPath</b>: elenco delle eliminazioni già fatte (solo identificativi, nessun dato personale); conviene su un disco diverso da quello del database.",
        "<b>pgBinDir</b>: cartella con <font name='UM'>pg_dump</font> e <font name='UM'>pg_restore</font>. Senza, il pulsante di backup resta disattivato e la scheda lo segnala.",
    ])
    s += H2("4.6 Verifica finale dell'installazione")
    s += STEPS([
        "Avviare <font name='UM'>Privacy.App.exe</font>: deve comparire la finestra <b>Prima configurazione</b> (§ 5.1). Se compare un errore di connessione, controllare <font name='UM'>connection.txt</font> e che il servizio PostgreSQL sia attivo.",
        "Completare la configurazione e accedere: deve aprirsi la finestra principale con le schede.",
        "Aprire la scheda <b>Backup</b>: se <font name='UM'>pgBinDir</font> è corretto, il pulsante <b>Esegui backup ora</b> è attivo.",
        "Eseguire un primo backup e controllare che compaia come <b>Riuscito</b> con la data di verifica, e che i file siano nella cartella principale e nella copia esterna.",
    ])

    # ---------------------------------------------------------------- 5
    s += H1("5. Primo avvio e configurazione")
    s += H2("5.1 Prima configurazione dello studio")
    s += [P("Al primo avvio, se il database non contiene ancora uno studio, si apre da sola la finestra <b>Prima configurazione</b>, in <b>due passaggi</b>: nel primo si inseriscono i dati dello studio, nel secondo il nome utente e la password.")]
    s += TABLE([
        ["Campo", "A che cosa serve"],
        ["Denominazione dello studio", "Compare nelle informative."],
        ["Titolare del trattamento", "Nome e cognome del medico. È il soggetto che decide finalità e mezzi del trattamento (art. 4, n. 7)."],
        ["Codice fiscale o partita IVA (facoltativo)", "Identificativo del titolare nelle informative, se lo si vuole indicare. Il programma controlla la cifra o il carattere di controllo e segnala un errore di battitura prima di proseguire."],
        ["Indirizzo dello studio", "Compare nelle informative."],
        ["Contatto per le richieste privacy", "Email o PEC a cui i pazienti scrivono per esercitare i diritti. Va mantenuto attivo e controllato."],
        ["Telefono (facoltativo), contatto del DPO (facoltativo)", "Il contatto del DPO, se nominato, deve essere indicato nell'informativa (art. 13, par. 1, lett. b)."],
        ["Passaggio 2: nome utente, nome mostrato, password (due volte)", "Le credenziali del medico. La password deve avere almeno 12 caratteri, non essere troppo ripetitiva né contenere il nome utente. La casella <b>Mostra la password</b> permette di vedere quanto scritto. Non esiste un modo per recuperarla se dimenticata."],
    ], [0.36, 0.64])
    s += FIG("01-prima-configurazione", "Prima configurazione, passaggio 1: i dati dello studio (dati inventati).", maxh=11.0 * cm)
    s += FIG("01b-prima-configurazione-accesso", "Passaggio 2: nome utente e password, con la casella per vedere la password.", maxh=11.0 * cm)
    s += NORMA("L'informativa deve contenere l'identità e i dati di contatto del titolare e, se del caso, del responsabile della protezione dei dati.", "Reg. UE 2016/679, art. 13, par. 1, lett. a) e b)")
    s += ATTENZIONE("Lo studio si configura <b>una sola volta</b>. La modifica successiva dei dati dello studio (per esempio un cambio di indirizzo) non è ancora disponibile nell'interfaccia: "
                    "va fatta con l'aiuto dell'assistenza. Le informative già pubblicate conservano i dati com'erano al momento della pubblicazione, per costruzione.")
    s += H2("5.2 Accesso quotidiano")
    s += BUL([
        "Inserire nome utente e password. Il nome utente non distingue le maiuscole.",
        "Dopo <b>5 tentativi sbagliati</b> l'accesso è bloccato per <b>15 minuti</b>, anche se poi si inserisce la password giusta. Il blocco non si azzera riavviando il programma.",
        "I messaggi di errore sono volutamente uguali per \"utente inesistente\" e \"password errata\".",
        "Ogni accesso, riuscito o no, è registrato nel registro di audit (senza mai registrare le password).",
    ])
    s += FIG("02-accesso", "La finestra di accesso.")
    s += ESAMINARE("Il <b>cambio della password</b> esiste come funzione del programma ma <b>non ha ancora una schermata</b>. Mancano anche la scadenza della sessione per inattività e il blocco dello schermo del programma: "
                   "nel frattempo bloccare il computer (tasto Windows + L) ogni volta che ci si allontana.")
    s += H2("5.3 Le chiavi di recupero: da fare subito")
    s += [P("Il programma usa due chiavi segrete: una per cifrare i backup e una per firmare i moduli consegnati ai pazienti. Sono generate automaticamente e custodite da Windows, "
            "legate all'utente Windows con cui si lavora. <b>Se il computer o il profilo Windows si perdono, senza una copia delle chiavi i backup non si possono più aprire.</b> "
            "Per questo, subito dopo il primo accesso:")]
    s += STEPS([
        "Aprire la scheda <b>Backup</b> e premere <b>Esporta chiavi di recupero…</b>.",
        "Scegliere una <b>frase segreta</b> di almeno 14 caratteri, digitarla due volte e salvare il file (estensione <font name='UM'>.pskr</font>).",
        "Conservare il file <b>fuori dal computer</b> (chiavetta in cassaforte) e la frase segreta <b>in un luogo diverso</b>.",
        "Prima di fidarsi, provare l'importazione su un altro computer (<b>Importa chiavi…</b>).",
    ])
    s += FIG("19-chiavi-di-recupero", "Esportazione delle chiavi di recupero (scheda Backup).")
    s += ATTENZIONE("Importare chiavi su un computer che ne ha già <b>le sostituisce</b>: i backup e i moduli cifrati con le chiavi precedenti non saranno più apribili. "
                    "Il programma chiede di digitare SOSTITUISCI per confermare.")
    s += ESAMINARE("Questa è una soluzione <b>provvisoria</b>. La decisione di fondo su come proteggere le chiavi e il database (cifratura del disco, cifratura dei singoli campi, gestione delle chiavi, "
                   "procedura di recupero testata da una persona non tecnica) è ancora aperta ed è la prima condizione per l'uso con dati reali (§ 9, punto 1).")
    return s
