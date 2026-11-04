# -*- coding: utf-8 -*-
from lib import *

def story():
    s = []
    s += H1("6. Uso del programma")
    s += H2("6.1 La finestra principale")
    s += [P("Dopo l'accesso si apre la finestra principale, divisa in schede. In alto, a destra, quattro pulsanti sempre disponibili: "
            "<b>Nuova richiesta…</b>, <b>Nuovo incidente…</b>, <b>Apri selezionata</b> (apre il dettaglio della riga scelta nella scheda Richieste o Incidenti) e <b>Aggiorna</b>.")]
    s += TABLE([
        ["Scheda", "Che cosa mostra", "Capitolo"],
        ["Richieste in scadenza", "Richieste degli interessati ancora aperte con scadenza entro 30 giorni o già scaduta, la più urgente per prima.", "6.5"],
        ["Incidenti aperti", "Violazioni non chiuse, con la scadenza delle 72 ore in ora italiana e il tempo residuo.", "6.6"],
        ["Conservazione", "Regole di conservazione, elementi scaduti, regole da rivedere.", "6.7"],
        ["Backup", "Esecuzioni di backup, stato rispetto all'obiettivo di 24 ore, chiavi di recupero.", "6.8"],
        ["Registro", "Le attività di trattamento e le loro revisioni.", "6.4"],
        ["Pazienti", "Anagrafica degli interessati, consegna dell'informativa, storico.", "6.2, 6.3"],
        ["Informative", "Le informative pubblicate.", "6.3"],
    ], [0.22, 0.64, 0.14])
    s += FIG("03-richieste-in-scadenza", "La finestra principale con la scheda «Richieste in scadenza». In alto i quattro pulsanti sempre disponibili.")
    s += H3("Regole comuni a tutte le schermate")
    s += BUL([
        "<b>Nessuna scelta preselezionata</b>: nelle liste a tendina bisogna scegliere esplicitamente; se si dimentica, il programma dice quale valore manca.",
        "<b>Scorciatoie da tastiera</b>: nei pulsanti e nelle etichette la lettera sottolineata, premuta insieme ad Alt, attiva il comando o porta al campo.",
        "<b>Messaggi di errore</b> in italiano; le regole del database (per esempio \"chiusura impossibile: un diritto senza decisione\") sono mostrate così come sono. Gli errori tecnici non mostrano dettagli, che potrebbero contenere dati.",
        "<b>Operazioni ripetibili</b>: se un salvataggio viene ripetuto per errore (doppio clic, ritentativo dopo un'interruzione), non crea duplicati.",
        "<b>Le date e le ore</b> sono sempre in ora italiana.",
    ])

    # ---------------------------------------------------------------- 6.2
    s += H2("6.2 Pazienti (anagrafica degli interessati)")
    s += [P("La scheda <b>Pazienti</b> contiene l'anagrafica minima necessaria alle consegne delle informative: nome, cognome e, se si vuole, data di nascita, codice fiscale, email e telefono. "
            "A ogni persona il programma assegna un codice interno progressivo (<font name='UM'>P-000001</font>, …). <b>Nessun dato clinico</b> viene inserito qui.")]
    s += STEPS([
        "<b>Cercare</b>: scrivere nel campo <b>Cerca</b> parte del nome, del cognome o del codice; l'elenco si aggiorna.",
        "<b>Nuovo interessato…</b>: compilare nome e cognome (obbligatori) e, se utile, gli altri campi.",
        "<b>Rettifica…</b>: seleziona la riga e modifica l'anagrafica corrente.",
        "<b>Consegne…</b> (o doppio clic): apre lo storico delle consegne della persona (§ 6.3).",
        "<b>Rimuovi…</b>: vedi sotto.",
    ])
    s += FIG("08-pazienti", "La scheda «Pazienti»: ricerca, nuovo interessato, rettifica, consegna dell'informativa, storico, rimozione.")
    s += NORMA("L'interessato ha il diritto di ottenere la rettifica dei dati inesatti e, tenuto conto delle finalità, l'integrazione di quelli incompleti.", "Reg. UE 2016/679, art. 16")
    s += [P("La <b>rettifica</b> cambia solo l'anagrafica di oggi: i moduli già consegnati restano com'erano stati emessi (il programma li ricostruisce dai dati salvati al momento della consegna).")]
    s += H3("La rimozione di un interessato")
    s += [P("<b>Rimuovi…</b> è un'operazione <b>irreversibile</b>, da fare solo dopo che il titolare ha valutato la richiesta di cancellazione e gli obblighi di conservazione. Il programma chiede una motivazione e di digitare RIMUOVI. Dopo la rimozione:")]
    s += BUL([
        "nome e cognome diventano \"Interessato rimosso\"; data di nascita, codice fiscale, email e telefono sono azzerati; il <b>codice interno resta</b>;",
        "le consegne passate restano come prova minima (esistenza, data, versione dell'informativa), ma con il nome azzerato e le note cancellate;",
        "i file collegati (per esempio la scansione di una ricevuta) vengono svuotati;",
        "l'operazione è registrata nel registro di audit e nel registro delle eliminazioni;",
        "la persona non è più modificabile, e il modulo della sua consegna non si può più ricostruire identico (l'impronta non coincide più): è voluto.",
    ])
    s += NORMA("Il diritto alla cancellazione non si applica nella misura in cui il trattamento sia necessario, tra l'altro, per l'adempimento di un obbligo legale, per motivi di interesse pubblico nel settore "
               "della sanità pubblica (art. 9, par. 2, lett. h e i, e par. 3), o per l'accertamento, l'esercizio o la difesa di un diritto in sede giudiziaria.", "Reg. UE 2016/679, art. 17, par. 3, lett. b), c), e)")
    s += ESAMINARE("La rimozione cancella <b>ciò che sta in Privacy Studio</b>. La documentazione clinica del paziente è altrove e segue obblighi di conservazione propri: "
                   "prima di rimuovere un paziente dal programma, il titolare deve valutare (con il consulente) se una richiesta di cancellazione possa essere accolta in tutto o in parte, "
                   "tenuto conto proprio dell'art. 17, par. 3, e di eventuali obblighi di conservazione della cartella clinica.")

    # ---------------------------------------------------------------- 6.3
    s += H2("6.3 Informative e consegne")
    s += NORMA("Il titolare adotta misure appropriate per fornire all'interessato, in forma concisa, trasparente, intelligibile e facilmente accessibile, con linguaggio semplice e chiaro, "
               "le informazioni previste dagli artt. 13 e 14. Se i dati sono raccolti presso l'interessato, l'informativa si dà <b>al momento</b> in cui i dati sono ottenuti.", "Reg. UE 2016/679, artt. 12, par. 1 e 13, par. 1")
    s += H3("Che cosa deve contenere l'informativa (art. 13)")
    s += BUL([
        "identità e contatti del titolare; contatti del DPO, se nominato;",
        "finalità del trattamento e base giuridica;",
        "destinatari o categorie di destinatari; eventuali trasferimenti verso paesi terzi;",
        "periodo di conservazione, o i criteri per determinarlo;",
        "i diritti dell'interessato (accesso, rettifica, cancellazione, limitazione, opposizione, portabilità) e il diritto di proporre reclamo al Garante;",
        "se il conferimento dei dati è obbligatorio o facoltativo e le conseguenze del mancato conferimento; l'esistenza di processi decisionali automatizzati, se ci sono.",
    ])
    s += FIG("09-informative", "La scheda «Informative» con le informative pubblicate.")
    s += H3("Creare e pubblicare un'informativa")
    s += STEPS([
        "Prima serve nel <b>Registro</b> almeno un'attività di trattamento <b>pubblicata</b> (§ 6.4): l'informativa deve riferirsi a ciò che lo studio ha dichiarato di fare.",
        "Scheda <b>Informative › Nuova informativa…</b>. Si compilano il titolo, il testo, la data di entrata in vigore e si selezionano le attività del registro a cui si riferisce (almeno una).",
        "Il <b>testo di partenza</b> è solo uno schema: contiene variabili e promemoria <font name='UM'>[Completare: …]</font> da sostituire con il contenuto che il titolare ha deciso.",
        "Alla conferma il programma <b>sostituisce le variabili</b> con i dati correnti dello studio (denominazione, titolare, indirizzo, contatti, versione, data), calcola l'impronta del testo, approva e pubblica. Da quel momento il testo non cambia più.",
    ])
    s += FIG("15-nuova-informativa", "«Nuova informativa»: testo con variabili, data di entrata in vigore e attività del registro collegate.", maxh=13.5 * cm)
    s += H3("Le variabili ammesse")
    s += [P("Si scrivono tra doppie graffe e sono un elenco chiuso: <font name='UM'>{{practice_name}}</font> (denominazione), <font name='UM'>{{controller_name}}</font> (titolare), "
            "<font name='UM'>{{controller_identifier}}</font>, <font name='UM'>{{address}}</font>, <font name='UM'>{{privacy_contact}}</font>, <font name='UM'>{{phone}}</font>, "
            "<font name='UM'>{{dpo_contact}}</font>, <font name='UM'>{{document_version}}</font> (numero di versione), <font name='UM'>{{effective_date}}</font> (data di efficacia). "
            "Una variabile non ammessa, una variabile senza valore (per esempio il DPO se non è stato indicato) o un promemoria <font name='UM'>[Completare…]</font> ancora presente <b>bloccano la pubblicazione</b>, e non resta alcuna bozza incompleta.")]
    s += ESAMINARE([
        "<b>Contenuto dell'informativa.</b> Il programma fornisce lo schema, non il testo giuridicamente adeguato: il contenuto (finalità, basi giuridiche, tempi di conservazione, destinatari, diritti) è del titolare e, se lo ritiene, del consulente. "
        "Il testo di partenza <b>non è un'informativa valida</b> finché non viene completato e approvato.",
        "Per il medico di medicina generale e il pediatra di libera scelta il Codice contiene una norma specifica sulle informazioni da dare ai pazienti (art. 78): <b>se questo è il caso dello studio, va verificata con il consulente</b>.",
        "Nessun aspetto grafico: il programma produce il <b>testo</b> del modulo. La stampa su carta intestata e il PDF non sono ancora disponibili."])
    s += H3("Registrare la consegna")
    s += STEPS([
        "Scheda <b>Pazienti</b>, selezionare la persona, <b>Consegna informativa…</b>.",
        "Scegliere l'<b>informativa</b> (versione pubblicata), la <b>modalità di consegna</b> (di persona, email, posta, altro) e l'<b>esito</b>.",
        "Indicare la <b>data di consegna</b>. Per un tentativo fallito il campo va lasciato vuoto.",
        "Se la riceve una persona diversa dal paziente (per esempio un genitore), indicarne <b>nome e qualità</b> nei campi del firmatario.",
        "Alla conferma il programma mostra il <b>codice di consegna</b> (formato <font name='UM'>PS-</font> seguito da 12 caratteri): è quello che compare sul modulo e serve a collegare una scansione alla consegna giusta.",
    ])
    s += FIG("16-consegna-informativa", "«Consegna informativa»: informativa, modalità, esito e data. Nessuna scelta è preselezionata (qui compilata a titolo di esempio).")
    s += TABLE([
        ["Esito", "Significato"],
        ["<b>Consegnata senza firma</b>", "Il paziente ha ricevuto l'informativa ma non ha firmato. È un caso normale e lecito: non si registra alcuna firma o alcun consenso."],
        ["<b>Ricevuta firmata</b>", "Il paziente ha firmato una ricevuta (che può essere acquisita come scansione)."],
        ["<b>Firma rifiutata (consegna avvenuta)</b>", "La consegna c'è stata, ma il paziente non ha voluto firmare."],
        ["<b>Tentativo fallito</b>", "La consegna non è avvenuta (per esempio email respinta): senza data di consegna."],
    ], [0.30, 0.70])
    s += NORMA("La consegna dell'informativa è un obbligo del titolare e non richiede il consenso dell'interessato né una sua firma: ciò che il titolare deve poter dimostrare è di aver <b>fornito</b> l'informazione. "
               "Per questo il programma distingue la consegna dalla firma e non inventa sottoscrizioni.", "Reg. UE 2016/679, artt. 5, par. 2, 12 e 13")
    s += H3("Lo storico e la verifica dell'integrità")
    s += [P("Da <b>Pazienti › Consegne…</b> si vedono le consegne di una persona. Selezionandone una e premendo <b>Mostra modulo</b> il programma <b>ricostruisce il testo</b> a partire dai dati salvati al momento della consegna "
            "e ne ricalcola l'impronta (un codice di controllo con chiave segreta). Se coincide con quella salvata, il modulo è identico a quello emesso, anche se nel frattempo l'indirizzo dello studio è cambiato. "
            "Il testo si può copiare negli appunti.")]
    s += FIG("17-storico-consegne", "Storico delle consegne di un paziente, con il modulo ricostruito e la verifica dell'integrità.", maxh=13.0 * cm)
    s += ESAMINARE("<b>Rappresentanti legali.</b> La struttura dati prevede la rappresentanza di minori o incapaci con verifica dei poteri e delle date di validità, ma la <b>schermata per gestirla non esiste ancora</b>: oggi chi riceve per un minore si indica a testo libero come firmatario. "
                   "Come documentare i poteri di chi ritira per conto di un minore o di una persona sotto tutela va deciso con il consulente.")

    # ---------------------------------------------------------------- 6.4
    s += H2("6.4 Registro dei trattamenti")
    s += NORMA("Ogni titolare tiene un registro delle attività di trattamento svolte sotto la propria responsabilità, in forma scritta (anche elettronica), con: nome e contatti del titolare; finalità; "
               "descrizione delle categorie di interessati e di dati; categorie di destinatari; trasferimenti verso paesi terzi; ove possibile, termini ultimi per la cancellazione; descrizione generale delle misure di sicurezza. "
               "Il registro è messo a disposizione del Garante su richiesta.", "Reg. UE 2016/679, art. 30, par. 1 e 4")
    s += NORMA("L'esenzione per imprese e organizzazioni con meno di 250 dipendenti <b>non si applica</b> se il trattamento non è occasionale o include categorie particolari di dati (art. 9): "
               "per uno studio medico il registro è quindi dovuto.", "Reg. UE 2016/679, art. 30, par. 5")
    s += [P("Il registro è composto da <b>attività</b> (per esempio \"Cura dei pazienti\", \"Gestione amministrativa e fatturazione\"). Ogni attività ha <b>revisioni</b> numerate: "
            "una revisione pubblicata non si modifica; per cambiare qualcosa si crea una nuova revisione. Così il registro \"alla data T\" si può sempre ricostruire.")]
    s += FIG("07-registro", "La scheda «Registro»: l'attività A2 mostra «Pubblicata - riesame scaduto», A3 è in bozza.")
    s += H3("Creare un'attività")
    s += STEPS([
        "Scheda <b>Registro › Nuova attività…</b>: codice (es. A1) e nome. L'attività nasce in bozza e si apre l'editor.",
        "Nell'editor: <b>descrizione</b> dell'attività e <b>data di riesame</b> prevista (entro quando il titolare riguarderà la scheda).",
        "<b>Categorie</b>: spuntare le categorie di dati (anagrafici, contatto, dati sulla salute, …), di interessati (pazienti, minori, collaboratori, fornitori, …) e di destinatari. Nessuna è preselezionata.",
        "<b>Finalità</b>: <b>Aggiungi finalità…</b> con nome, descrizione, valutazione giuridica del titolare, valutazione sul consenso (necessario / non necessario) e la prima <b>base giuridica</b>: art. 6 e, se si trattano dati sulla salute, la condizione dell'art. 9; riferimento normativo (facoltativo) e a quale parte della finalità si applica. "
        "<b>Aggiungi presupposto…</b> aggiunge altre basi alla finalità selezionata; <b>Rimuovi finalità</b> la toglie (solo in bozza).",
        "<b>Salva dati e categorie</b>, poi <b>Invia in verifica</b> e infine <b>Pubblica…</b> indicando la data di entrata in vigore.",
    ])
    s += FIG("13-registro-editor", "L'editor di una revisione in bozza: descrizione, riesame, categorie e finalità.", maxh=14.0 * cm)
    s += FIG("14-nuova-finalita", "«Nuova finalità»: valutazione giuridica, base dell'art. 6 e condizione dell'art. 9 per i dati sulla salute.", maxh=12.0 * cm)
    s += H3("Che cosa controlla il programma alla pubblicazione")
    s += BUL([
        "descrizione dell'attività, data di riesame e approvazione presenti;",
        "almeno una finalità, e per ogni finalità: descrizione, valutazione giuridica, almeno un presupposto, consenso già valutato (non \"da valutare\");",
        "almeno una categoria di dati e una di interessati;",
        "nessuna categoria di dati \"da valutare\";",
        "se ci sono <b>dati sulla salute</b> (art. 9), almeno una finalità con la condizione dell'art. 9; se ci sono dati su condanne penali o reati (art. 10), un riferimento a una disposizione che lo consenta.",
    ])
    s += [P("Se manca qualcosa la pubblicazione è <b>rifiutata</b> e il messaggio elenca tutto ciò che manca. Con la pubblicazione la revisione diventa immutabile e la precedente diventa \"superata\" nello stesso istante.")]
    s += H3("Nuova revisione ed esportazione")
    s += BUL([
        "<b>Nuova revisione…</b> (da una revisione pubblicata) copia finalità, basi e categorie; si modifica la copia, indicando il motivo, e la si pubblica quando è pronta. La revisione precedente resta in vigore fino ad allora.",
        "<b>Esporta alla data…</b> produce un testo leggibile del registro com'era a quella data (copiabile o salvabile come file), da mostrare al Garante se lo chiede. Modifiche successive non cambiano l'esportazione di una data passata.",
        "Lo stato \"<b>Pubblicata - riesame scaduto</b>\" segnala le attività la cui data di riesame è passata.",
    ])
    s += FIG("20-esportazione-registro", "Esportazione del registro alla data scelta: testo leggibile, copiabile e salvabile.", maxh=13.5 * cm)
    s += ESAMINARE([
        "<b>Completezza rispetto all'art. 30.</b> L'editor e l'esportazione non comprendono ancora i <b>sistemi</b> usati, le <b>misure di sicurezza</b> per attività, i <b>fornitori</b> (responsabili) e i <b>trasferimenti verso paesi terzi</b>; i termini di cancellazione sono nel modulo Conservazione ma non nell'esportazione. "
        "Finché mancano, queste parti vanno documentate a parte (la nota in fondo all'esportazione lo ricorda).",
        "La <b>scelta delle basi giuridiche</b> e delle condizioni dell'art. 9 è del titolare: il programma le registra e controlla che ci siano, non che siano giuste."])

    # ---------------------------------------------------------------- 6.5
    s += H2("6.5 Richieste degli interessati")
    s += NORMA("Il titolare agevola l'esercizio dei diritti e fornisce le informazioni sull'azione intrapresa <b>senza ingiustificato ritardo e, comunque, al più tardi entro un mese</b> dal ricevimento della richiesta. "
               "Il termine può essere <b>prorogato di due mesi</b>, se necessario, tenuto conto della complessità e del numero delle richieste; l'interessato è informato della proroga e dei motivi del ritardo "
               "entro un mese dal ricevimento. Se il titolare non ottempera, informa l'interessato senza ritardo, e al più tardi entro un mese, dei motivi e della possibilità di proporre reclamo al Garante.",
               "Reg. UE 2016/679, art. 12, par. 3 e 4")
    s += TABLE([
        ["Diritto", "Articolo", "In sintesi"],
        ["Accesso", "15", "Sapere se i dati sono trattati, ottenerne copia e le informazioni collegate."],
        ["Rettifica", "16", "Correggere o integrare dati inesatti o incompleti."],
        ["Cancellazione", "17", "Cancellazione senza ingiustificato ritardo nei casi previsti, con le eccezioni del par. 3."],
        ["Limitazione", "18", "Contrassegnare i dati in attesa di verifiche o in altri casi previsti."],
        ["Portabilità", "20", "Ricevere i dati in formato strutturato, quando il trattamento si basa su consenso o contratto ed è automatizzato."],
        ["Opposizione", "21", "Opporsi a trattamenti basati su interesse pubblico o legittimo interesse."],
    ], [0.20, 0.12, 0.68])
    s += [P("La richiesta può arrivare da <b>qualunque canale</b> (di persona, email, PEC, posta, telefono) e anche da chi <b>non è un paziente</b> (un collaboratore, un fornitore, un erede): il programma non richiede che esista una scheda del paziente.")]
    s += H3("Registrare una richiesta")
    s += STEPS([
        "<b>Nuova richiesta…</b>: nome del richiedente, <b>qualità</b> (interessato, rappresentante o delegato, erede o avente interesse, altro), canale, data e ora di ricezione (di norma adesso), testo e <b>diritti richiesti</b> (almeno uno).",
        "Alla conferma il programma assegna il riferimento (<font name='UM'>R-2026-0001</font>, …) e <b>calcola la scadenza</b> (§ seguente), che compare nella scheda <b>Richieste in scadenza</b>.",
        "<b>Apri selezionata</b> (o doppio clic) mostra il dettaglio: i diritti richiesti con il loro stato, la cronologia e le azioni.",
    ])
    s += FIG("10-nuova-richiesta", "«Nuova richiesta»: richiedente, qualità, canale, data di ricezione, testo e diritti (almeno uno).", maxh=12.5 * cm)
    s += H3("Come si calcola la scadenza")
    s += [P("Regola applicata (identificata nel programma come <font name='UM'>it-1182-v1</font>), ispirata al Regolamento (CEE, Euratom) n. 1182/71: il giorno di ricezione non conta; la scadenza è il <b>giorno corrispondente del mese successivo</b>; "
            "se quel giorno non esiste (per esempio 31 gennaio) è l'ultimo giorno del mese; se cade di sabato, domenica o in una festività nazionale (compreso il lunedì di Pasqua) slitta al <b>primo giorno lavorativo successivo</b>. "
            "La data si intende nel fuso orario italiano. Alcuni esempi sono nell'Appendice B.")]
    s += ESAMINARE([
        "<b>La regola di calcolo va confermata dal consulente.</b> Non è pacifico come il Regolamento 1182/71 si applichi ai termini dell'art. 12 né se lo slittamento a giorno lavorativo spetti anche al termine di <i>risposta</i> (e non solo ai termini di <i>azione</i>). "
        "Finché non è confermata, il programma mostra la scadenza calcolata con questa regola: <b>per cautela, rispondere sempre prima</b>.",
        "<b>La proroga</b>: il programma accetta una proroga fino a <b>tre mesi complessivi dalla ricezione</b> (un mese più due). Se invece i due mesi di proroga si sommassero alla scadenza già calcolata (per esempio 28 febbraio più due mesi) il limite cambierebbe. Anche questo è da confermare.",
        "Il programma <b>non registra la verifica dell'identità</b> del richiedente (art. 12, par. 6: il titolare può chiedere informazioni supplementari in caso di dubbio ragionevole); va annotata nella cronologia o a parte. La struttura dati è prevista, la schermata no.",
        "<b>Costi e richieste manifestamente infondate o eccessive</b> (art. 12, par. 5): le informazioni sono gratuite, salvo il caso di richieste manifestamente infondate o eccessive. La valutazione è del titolare."])
    s += FIG("11-dettaglio-richiesta", "Dettaglio di una richiesta: diritti con il loro stato, cronologia e azioni (Proroga, Decisione, Chiudi richiesta).")
    s += H3("Prorogare, decidere, chiudere")
    s += BUL([
        "<b>Proroga…</b>: nuova scadenza (in avanti, entro il limite), <b>motivazione</b> obbligatoria e, se già comunicata, la data di comunicazione all'interessato. La scadenza iniziale resta registrata: non si cancella.",
        "<b>Decisione…</b>: per ogni diritto si sceglie l'<b>esito</b> (accolta, accolta in parte, respinta, non applicabile). La <b>motivazione</b> è obbligatoria se non è accolta del tutto. Si indica la data di invio del riscontro quando è avvenuto; se è ancora da inviare si lascia vuota.",
        "<b>Chiudi richiesta</b>: possibile solo se <b>ogni</b> diritto richiesto ha una decisione <b>con riscontro inviato</b>. Altrimenti il programma rifiuta e dice quanti diritti mancano.",
    ])
    s += NORMA("<b>Persone decedute.</b> I diritti riguardanti dati di persone decedute possono essere esercitati da chi ha un interesse proprio, o agisce a tutela dell'interessato come mandatario, o per ragioni familiari meritevoli di protezione. "
               "Nel programma si registra scegliendo come qualità <b>Erede o avente interesse</b>.", "D.Lgs. 196/2003, art. 2-terdecies")
    s += ESAMINARE([
        "<b>Accesso alla documentazione sanitaria.</b> Per le strutture sanitarie la legge 8 marzo 2017, n. 24 (art. 4) prevede termini specifici (ricordati come sette giorni) per fornire al paziente la documentazione sanitaria. "
        "Se e come questa disposizione si applichi a uno studio professionale individuale, e quale termine prevalga sul termine ordinario dell'art. 12 GDPR, <b>va verificato con il consulente</b>.",
        "Il testo della richiesta e il nome del richiedente sono dati personali conservati nel programma: la loro durata di conservazione va decisa (§ 6.7)."])

    # ---------------------------------------------------------------- 6.6
    s += H2("6.6 Violazioni dei dati (incidenti)")
    s += NORMA("In caso di violazione di dati personali il titolare notifica la violazione all'autorità di controllo <b>senza ingiustificato ritardo e, ove possibile, entro 72 ore</b> dal momento in cui ne è venuto a conoscenza, "
               "a meno che sia improbabile che la violazione presenti un rischio per i diritti e le libertà delle persone fisiche. Se la notifica non è fatta entro 72 ore, è corredata dei motivi del ritardo. "
               "Il titolare <b>documenta qualsiasi violazione</b>, comprese le circostanze, le conseguenze e i provvedimenti adottati (anche se non notificata).", "Reg. UE 2016/679, art. 33, par. 1 e 5")
    s += NORMA("Quando la violazione è suscettibile di presentare un <b>rischio elevato</b> per i diritti e le libertà delle persone, il titolare la comunica senza ingiustificato ritardo anche agli interessati, "
               "salvo le eccezioni del par. 3 (dati resi incomprensibili, misure successive che escludono il rischio, sforzo sproporzionato).", "Reg. UE 2016/679, art. 34")
    s += [P("Una violazione non è solo un furto di dati: è anche la <b>perdita</b> o l'<b>indisponibilità</b> dei dati (per esempio un guasto o un attacco che impedisce di accedere alle cartelle) e l'accesso o la modifica non autorizzati. "
            "Il programma permette di registrare anche un incidente di sola indisponibilità come violazione.")]
    s += FIG("04-incidenti-aperti", "La scheda «Incidenti aperti»: scadenza delle 72 ore in ora italiana e tempo residuo.")
    s += H3("Che cosa fa il programma")
    s += STEPS([
        "<b>Nuovo incidente…</b>: quando è stato <b>rilevato</b>, quando il titolare ne è <b>venuto a conoscenza</b> (se già accertato; altrimenti il campo resta vuoto), descrizione e classificazione provvisoria (da valutare, incidente tecnico senza dati personali, violazione di dati personali).",
        "Dal momento della consapevolezza partono le <b>72 ore</b>, calcolate su <b>istanti reali</b> (non in giorni lavorativi, e correttamente anche attraverso il cambio dell'ora). La scheda <b>Incidenti aperti</b> mostra la scadenza in ora italiana e il tempo residuo.",
        "Nel dettaglio: <b>Consapevolezza…</b> per impostare o correggere l'istante (ogni modifica resta nel registro di audit); <b>Valutazione…</b> per registrare il livello di rischio (improbabile, rischio, elevato), motivazione, conseguenze probabili, numero approssimativo di interessati, categorie di dati coinvolte, e le due decisioni (notificare al Garante? comunicare agli interessati?) con l'eventuale motivo di un ritardo; <b>Registra azione…</b> per ciò che si fa (contenimento, notifica, integrazione, comunicazione agli interessati, note).",
        "L'azione di tipo <b>Notifica al Garante</b> richiede il <b>numero di protocollo o la ricevuta</b>, da inserire <b>dopo</b> l'invio effettivo: il programma non registra una notifica che non è avvenuta.",
        "<b>Chiudi incidente</b> quando tutto è documentato.",
    ])
    s += FIG("12-dettaglio-incidente", "Dettaglio di un incidente: scadenza di notifica, azioni registrate e comandi (Consapevolezza, Valutazione, Registra azione, Chiudi).")
    s += ATTENZIONE("Il programma <b>non invia</b> la notifica al Garante e <b>non decide</b> se un evento sia una violazione né se vada notificata. Il medico (con il consulente, nei casi dubbi) valuta; il programma ricorda l'orologio e conserva la documentazione. "
                    "Anche le violazioni <b>non</b> notificate vanno registrate: l'art. 33, par. 5 lo richiede.")
    s += ESAMINARE([
        "<b>Quando decorrono le 72 ore.</b> Il Regolamento parla del momento in cui il titolare \"ne è venuto a conoscenza\"; le linee guida dell'EDPB lo collegano al momento in cui il titolare ha un ragionevole grado di certezza che si sia verificata una violazione. Quale istante inserire come \"consapevolezza\" in un caso concreto è una valutazione da fare con attenzione.",
        "<b>Come si notifica al Garante.</b> Esiste una procedura telematica messa a disposizione dal Garante: le modalità vigenti e i campi richiesti vanno verificati sul sito dell'autorità al momento dell'uso. Il programma conserva solo il protocollo.",
        "<b>Classificazione e soglie di rischio</b> (quando il rischio è \"improbabile\", \"presente\", \"elevato\"): scelta del titolare, anche con riferimento alle linee guida dell'EDPB."])

    # ---------------------------------------------------------------- 6.7
    s += H2("6.7 Conservazione e eliminazione")
    s += NORMA("I dati personali sono conservati in una forma che consenta l'identificazione degli interessati per un arco di tempo <b>non superiore al conseguimento delle finalità</b> per le quali sono trattati. "
               "Il titolare deve poter indicare i termini di cancellazione e informare gli interessati del periodo di conservazione o dei criteri per determinarlo.", "Reg. UE 2016/679, art. 5, par. 1, lett. e); art. 13, par. 2, lett. a); art. 30, par. 1, lett. f)")
    s += H3("Le regole di conservazione")
    s += [P("Nella scheda <b>Conservazione</b> si definiscono le regole, una per tipo di documentazione. <b>Il programma non propone alcuna durata</b>: le sceglie il titolare. "
            "<b>Nuova regola…</b> chiede il tipo di documentazione (richieste degli interessati, incidenti, informative e prove di consegna, …), l'evento da cui decorre la durata (es. chiusura della richiesta), "
            "la natura della durata e il riesame della regola:")]
    s += BUL([
        "<b>Determinata</b>: si indicano mesi e/o giorni. Alla data di scadenza gli elementi compaiono tra gli <b>elementi scaduti</b>.",
        "<b>Illimitata, motivata</b> o <b>a criterio</b>: serve un criterio o una motivazione scritta; non scade da sola e compare tra le <b>regole da rivedere</b> quando è passata la data di riesame. Una durata \"vuota\" non vale mai come \"illimitata\".",
    ])
    s += [P("La regola nasce in <b>bozza</b> e non ha effetto finché il titolare non la <b>approva</b> (<b>Approva selezionata</b>). Una nuova versione di una regola sostituisce la precedente, che diventa \"superata\".")]
    s += FIG("05-conservazione", "La scheda «Conservazione»: regole, elementi scaduti con il motivo e regole da rivedere. Le durate mostrate sono esempi inventati, non indicazioni.")
    s += H3("Eliminare gli elementi scaduti")
    s += STEPS([
        "Nella lista <b>Elementi scaduti</b> (richieste chiuse, incidenti chiusi, consegne di informative la cui durata approvata è trascorsa), selezionare uno o più elementi (tasto Ctrl per più righe).",
        "<b>Elimina selezionati…</b>: il programma elenca gli elementi, chiede la <b>motivazione</b> (l'autorizzazione del titolare) e di digitare ELIMINA.",
        "I dati vengono cancellati dal database (con i file collegati svuotati). Restano solo il <b>registro dell'eliminazione</b> (descrizione generica e identificativo dell'elemento, senza dati personali) e la riga di audit.",
        "L'identificativo viene scritto anche in un <b>file esterno</b> (<font name='UM'>ledgerPath</font>), copiato con ogni backup: serve, dopo un ripristino da un backup più vecchio, a rieliminare ciò che era già stato cancellato (§ 7.4).",
    ])
    s += FIG("18-eliminazione-guidata", "Eliminazione guidata: elenco degli elementi, motivazione e conferma scritta.", maxh=9.0 * cm)
    s += ATTENZIONE("L'eliminazione è <b>irreversibile</b>. Richieste e incidenti si possono eliminare solo se chiusi. Il programma non cancella mai nulla da solo: ogni eliminazione richiede l'azione del titolare.")
    s += ESAMINARE([
        "<b>Durate di conservazione.</b> Per la <b>documentazione clinica</b> non esiste, per lo studio del singolo medico, un termine unico e semplice: la prassi per le cartelle cliniche delle strutture sanitarie si richiama a indicazioni ministeriali che portano a una conservazione molto lunga o illimitata, "
        "mentre per lo studio professionale la durata va stabilita tenendo conto di obblighi di legge, esigenze di difesa in giudizio, indicazioni dell'Ordine e delle regole regionali. <b>Da definire con il consulente.</b> La documentazione clinica non è comunque gestita da questo programma.",
        "<b>Documenti contabili e fiscali</b>: scritture contabili (art. 2220 del codice civile, di regola dieci anni) e documenti fiscali seguono la normativa tributaria; il termine esatto per ciascun documento è da confermare con il commercialista.",
        "<b>Richieste e incidenti</b>: per quanto tempo conservare le richieste chiuse e la documentazione delle violazioni (che serve anche a dimostrare la conformità) è una scelta del titolare, da motivare.",
        "Il <b>registro di audit</b> e il <b>registro delle eliminazioni</b> non vengono eliminati dal programma; la loro durata di conservazione va decisa a parte."])

    # ---------------------------------------------------------------- 6.8
    s += H2("6.8 Backup e chiavi")
    s += NORMA("Le misure di sicurezza comprendono la capacità di ripristinare tempestivamente la disponibilità e l'accesso ai dati personali in caso di incidente fisico o tecnico, e una procedura per provare regolarmente l'efficacia delle misure.", "Reg. UE 2016/679, art. 32, par. 1, lett. c) e d)")
    s += FIG("06-backup", "La scheda «Backup»: stato rispetto all'obiettivo di 24 ore, esecuzioni e comandi per le chiavi.")
    s += BUL([
        "<b>Esegui backup ora</b>: il programma estrae una copia dei dati, la <b>cifra</b>, la scrive nella cartella dei backup e nella copia esterna, poi la <b>rilegge e la decifra dal disco</b> per accertarsi che sia valida. Solo allora l'esecuzione risulta <b>Riuscita</b> con la data di verifica. Accanto alla copia viene messo il file con l'elenco delle eliminazioni.",
        "In cima alla scheda una riga indica lo stato rispetto all'obiettivo di <b>24 ore</b> (<i>ATTENZIONE: l'ultimo backup verificato risale a… ore fa</i>).",
        "<b>Verifica un file…</b>: controlla a campione che un file di backup si decifri e sia una copia valida.",
        "<b>Esporta / Importa chiavi…</b>: vedi § 5.3.",
    ])
    s += ESAMINARE([
        "<b>Frequenza e tempi di ripristino.</b> L'obiettivo di 24 ore di perdita massima di dati e di un giorno lavorativo per il ripristino è una <b>proposta</b> da confermare con il medico: non sono termini fissati dal Regolamento.",
        "Il <b>backup è manuale</b>: non c'è ancora la pianificazione automatica. Va fatto a fine giornata e controllato.",
        "Il file di backup è trattato in memoria durante la cifratura: adatto alle dimensioni di uno studio, non a volumi molto grandi."])
    return s
