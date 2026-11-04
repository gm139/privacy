# -*- coding: utf-8 -*-
from lib import *

def story():
    s = []
    s += H1("0. Gli adempimenti del medico libero professionista")
    s += [P("Questo capitolo è indipendente dal programma: descrive gli adempimenti che il Regolamento (UE) 2016/679 (GDPR) e il Codice privacy pongono al medico che esercita in proprio, in ordine di priorità e con i riferimenti normativi. "
            "Privacy Studio ne copre una parte (§ 0.6); il resto resta al titolare. Non esiste un «bollino» di conformità: la legge chiede di adottare <b>misure adeguate</b> al rischio e di essere in grado di <b>dimostrarlo</b>.")]
    s += NORMA("Il titolare mette in atto misure tecniche e organizzative adeguate per garantire, ed essere in grado di dimostrare, che il trattamento è effettuato conformemente al Regolamento, tenuto conto della natura, dell'ambito, del contesto e delle finalità del trattamento e dei rischi per i diritti e le libertà delle persone. "
               "Le misure sono riesaminate e aggiornate se necessario.", "Reg. UE 2016/679, art. 24, par. 1; art. 5, par. 2 (responsabilizzazione)")
    s += ESAMINARE("Questo capitolo è una <b>sintesi divulgativa</b>, non un parere legale. Le norme sono citate come note alla data del manuale; i provvedimenti del Garante e le linee guida europee cambiano. "
                   "I punti in cui la norma lascia margini di scelta, o di cui chi scrive non ha certezza, sono segnalati in questi riquadri e raccolti nel capitolo 9.")

    # ------------------------------------------------------------ 0.1
    s += H2("0.1 Il punto di partenza: chi è il medico per il GDPR")
    s += [P("Il medico che decide <b>quali dati dei pazienti raccogliere, perché e come</b> è titolare del trattamento. Il suo ruolo concreto dipende da come lavora:")]
    s += TABLE([
        ["Situazione", "Ruolo di norma", "Da tenere presente"],
        ["Studio proprio, esercizio individuale", "<b>Titolare autonomo</b> dei dati dei propri pazienti (art. 4, n. 7).", "Tutti gli adempimenti di questo capitolo ricadono sul medico."],
        ["Medico di medicina generale o pediatra di libera scelta convenzionato", "Titolare per i propri assistiti.", "Il Codice ha una norma specifica sull'informativa (art. 78). I dati inseriti nei sistemi regionali o nazionali (prescrizioni, anagrafe assistiti) seguono le regole di quei sistemi: <b>ruolo da verificare</b>."],
        ["Studio associato, medicina di gruppo, segreteria o archivio in comune", "Ciascun medico titolare per i propri pazienti, con un <b>responsabile</b> comune (la segreteria) oppure <b>contitolarità</b> (art. 26).", "Serve un accordo scritto che dica chi fa che cosa e chi risponde agli interessati."],
        ["Medico che esercita in una struttura (ambulatorio, poliambulatorio, clinica)", "Di norma <b>titolare è la struttura</b> per i dati trattati con i suoi mezzi; il medico può essere persona autorizzata o titolare autonomo per alcuni dati.", "Dipende dal contratto e da chi decide gli strumenti: <b>da esaminare caso per caso</b>."],
        ["Medico competente (medicina del lavoro)", "Titolare autonomo delle cartelle sanitarie e di rischio dei lavoratori, secondo la disciplina specifica (D.Lgs. 81/2008).", "Regole proprie su conservazione e comunicazioni al datore di lavoro: <b>da esaminare</b>."],
        ["Telemedicina o piattaforme online", "La piattaforma può essere <b>responsabile</b> (art. 28) o <b>contitolare</b> (art. 26).", "Verificare contratto, luogo dei dati e trasferimenti fuori dall'Unione (§ 0.3.7)."],
    ], [0.27, 0.34, 0.39])
    s += ESAMINARE("La qualificazione del ruolo (titolare autonomo, contitolare, responsabile, persona autorizzata) non è automatica e cambia gli adempimenti. In presenza di una struttura, di un'associazione o di una piattaforma, va stabilita <b>con il consulente</b>, leggendo i contratti.")
    s += H3("Che dati sono in gioco")
    s += NORMA("«Dati relativi alla salute» sono i dati personali attinenti alla salute fisica o mentale di una persona fisica, compresa la prestazione di servizi di assistenza sanitaria, che rivelano informazioni relative al suo stato di salute. "
               "Sono una <b>categoria particolare</b> di dati: il loro trattamento è vietato salvo le eccezioni dell'art. 9, par. 2.", "Reg. UE 2016/679, art. 4, n. 15 e art. 9, par. 1")
    s += [P("Rientrano non solo diagnosi, terapie e referti, ma anche il semplice fatto che una persona sia paziente dello studio, gli appuntamenti, le prescrizioni e i certificati. "
            "Ci sono poi i dati «ordinari»: anagrafica, recapiti, fatturazione, e quelli di collaboratori e fornitori.")]
    s += H3("Segreto professionale e consensi: due cose da non confondere")
    s += BUL([
        "<b>Segreto professionale</b>: il medico è già tenuto alla riservatezza (art. 622 del codice penale; Codice di deontologia medica, artt. 10 e 11 nel testo del 2014, da verificare nella versione vigente). La disciplina privacy si <b>aggiunge</b> al segreto, non lo sostituisce; l'art. 9, par. 3 GDPR lo richiama espressamente.",
        "<b>Consenso informato alle cure</b> (legge 22 dicembre 2017, n. 219): riguarda il trattamento sanitario, non il trattamento dei dati. Per curare il paziente <b>non serve</b> un consenso privacy (§ 2.2); serve invece, di norma, il consenso alle cure. Sono due documenti e due basi diverse.",
    ])

    # ------------------------------------------------------------ 0.2
    s += H2("0.2 Gli adempimenti in una pagina")
    s += TABLE([
        ["N.", "Adempimento", "Norma", "Quando", "Nel manuale"],
        ["1", "Mappare i trattamenti e i dati", "Artt. 5, 30", "Sempre: è la base di tutto", "§ 0.3.1, 6.4"],
        ["2", "Individuare finalità e basi giuridiche", "Artt. 6, 9", "Sempre", "§ 0.3.2, 2.2"],
        ["3", "Informativa ai pazienti", "Artt. 12-14; art. 78 Codice", "Sempre", "§ 0.3.3, 6.3"],
        ["4", "Registro dei trattamenti", "Art. 30", "Sempre (par. 5: l'esenzione non vale)", "§ 6.4"],
        ["5", "Misure di sicurezza", "Artt. 25, 32", "Sempre", "§ 0.3.5, cap. 3 e 8"],
        ["6", "Designare le persone autorizzate", "Artt. 29, 32 par. 4; art. 2-quaterdecies", "Se ci sono collaboratori, sostituti, tirocinanti", "§ 0.3.6"],
        ["7", "Nominare i responsabili; gestire contitolari e trasferimenti", "Artt. 26, 28; capo V", "Se fornitori trattano dati per conto dello studio", "§ 0.3.7"],
        ["8", "Procedura per i diritti degli interessati", "Artt. 12, 15-22; art. 2-terdecies", "Sempre (la procedura); le richieste, quando arrivano", "§ 0.3.8, 6.5"],
        ["9", "Procedura e registro delle violazioni", "Artt. 33, 34", "Sempre (la procedura e il registro)", "§ 0.3.9, 6.6"],
        ["10", "Conservazione e cancellazione", "Art. 5 par. 1 lett. e); artt. 17, 30", "Sempre", "§ 0.3.10, 6.7"],
        ["11", "Valutare DPIA e DPO, e documentare la valutazione", "Artt. 35, 37", "Da valutare sempre; obbligo solo in certi casi", "§ 0.3.11"],
        ["12", "Situazioni particolari (consensi, telemedicina, fascicolo sanitario, sito web, videosorveglianza, minori)", "Vari", "Se ricorrono", "§ 0.3.12"],
        ["13", "Formazione e verifica periodica", "Artt. 24, 32 par. 4", "Sempre", "§ 0.5"],
    ], [0.05, 0.31, 0.22, 0.25, 0.17])

    # ------------------------------------------------------------ 0.3
    s += H2("0.3 Gli adempimenti, uno per uno")

    s += H3("0.3.1 Sapere che cosa si tratta: la mappa dei trattamenti")
    s += BUL([
        "Elencare <b>quali dati</b> si raccolgono (anagrafici, contatto, salute, fatturazione), <b>da chi</b> (paziente, familiari, altri medici, laboratori, ASL), <b>dove</b> stanno (gestionale, cartelle cartacee, computer, telefono, posta, cloud, supporti esterni) e <b>chi può vederli</b>.",
        "Contare anche i canali «informali»: messaggi su WhatsApp, foto di referti sul telefono, email con allegati, agende di carta, fogli di calcolo.",
        "Per ogni trattamento chiedersi: serve davvero questo dato? (<b>minimizzazione</b>) È esatto e aggiornato? Per quanto tempo lo tengo?",
    ])
    s += NORMA("I dati personali sono adeguati, pertinenti e limitati a quanto necessario rispetto alle finalità (minimizzazione); esatti e, se necessario, aggiornati; conservati per un arco di tempo non superiore a quello necessario (limitazione della conservazione); "
               "trattati in modo da garantirne un'adeguata sicurezza (integrità e riservatezza).", "Reg. UE 2016/679, art. 5, par. 1, lett. c), d), e), f)")
    s += [P("La mappa è il materiale da cui nascono il registro (§ 6.4), l'informativa e la valutazione dei rischi. Senza, tutto il resto è costruito sulla sabbia.")]

    s += H3("0.3.2 Perché si può trattare quel dato: finalità e basi giuridiche")
    s += [P("Ogni trattamento ha una <b>finalità</b> dichiarata e una <b>base giuridica</b> (art. 6); per i dati sulla salute serve in più una <b>condizione dell'art. 9, par. 2</b>. Per un medico le combinazioni tipiche sono:")]
    s += TABLE([
        ["Attività", "Base art. 6", "Condizione art. 9 (dati sulla salute)"],
        ["Visita, diagnosi, terapia, gestione dell'agenda e dei documenti clinici", "Lett. b): esecuzione del contratto con il paziente (o misure precontrattuali)", "Par. 2, lett. h) con par. 3: finalità di cura, da parte di un professionista tenuto al segreto"],
        ["Fatturazione, adempimenti fiscali e contabili", "Lett. c): obbligo legale", "Di norma non servono dati sanitari analitici; se ci sono, lett. h) o lett. f)"],
        ["Comunicazioni obbligatorie (certificati, segnalazioni di legge, sistemi prescrittivi)", "Lett. c): obbligo legale", "Lett. h) o i) (sanità pubblica), secondo la norma che impone la comunicazione"],
        ["Difesa in un contenzioso o in una richiesta di risarcimento", "Lett. f) (legittimo interesse) o lett. c)", "Par. 2, lett. f): accertare, esercitare o difendere un diritto in sede giudiziaria"],
        ["Comunicazioni promozionali, newsletter, ricerca, alcuni servizi opzionali", "Lett. a): <b>consenso</b>", "Par. 2, lett. a): consenso esplicito (per ricerca possono esistere altre regole)"],
    ], [0.38, 0.30, 0.32])
    s += NORMA("Il trattamento di dati relativi alla salute è consentito quando è necessario per finalità di medicina preventiva, diagnosi, assistenza o terapia sanitaria, sulla base del diritto dell'Unione o degli Stati membri o di un contratto con un professionista della sanità, "
               "se i dati sono trattati da o sotto la responsabilità di un professionista soggetto al segreto professionale. Il Garante ha chiarito (provvedimento del 7 marzo 2019) che per le finalità di cura <b>non occorre il consenso</b> dell'interessato.",
               "Reg. UE 2016/679, art. 9, par. 2, lett. h) e par. 3; art. 6, par. 1")
    s += ESAMINARE("L'abbinamento tra ogni attività dello studio e la base giuridica è una <b>valutazione del titolare</b> e va scritta (nel registro, § 6.4). Il <b>consenso</b> è la base giusta solo per ciò che non è necessario alla cura; non va chiesto «per sicurezza» dove una base diversa si applica, perché un consenso non libero o non revocabile è inefficace e genera confusione. "
                   "Quali trattamenti dello studio richiedano davvero un consenso va verificato con il consulente.")

    s += H3("0.3.3 L'informativa ai pazienti")
    s += BUL([
        "Va data <b>prima o al momento</b> della raccolta dei dati (art. 13, par. 1), in forma concisa, trasparente, intelligibile e accessibile, con linguaggio semplice e chiaro, <b>anche per minori</b> (art. 12, par. 1).",
        "Contenuto: identità e contatti del titolare (e del DPO, se c'è); finalità e base giuridica; destinatari o categorie di destinatari; eventuali trasferimenti fuori dall'UE; <b>periodo di conservazione</b> o criteri; diritti dell'interessato e diritto di reclamo al Garante; se il conferimento è obbligatorio; eventuali processi decisionali automatizzati (art. 13, par. 1 e 2).",
        "Se i dati arrivano da altri (un altro medico, un familiare, un laboratorio) l'informativa va data anche all'interessato, <b>entro un termine ragionevole e al più tardi entro un mese</b> (art. 14, par. 3), salvo le eccezioni dell'art. 14, par. 5.",
        "Va resa <b>disponibile in studio</b> (sala d'attesa, consegna) e, se esiste, <b>sul sito</b>. Conviene darla anche al paziente nuovo, e riconsegnarla se cambia in modo rilevante.",
        "Il medico deve poter <b>dimostrare</b> di averla data: Privacy Studio registra le consegne (§ 6.3). La firma del paziente non è richiesta dalla legge per l'informativa.",
    ])
    s += NORMA("Per il medico di medicina generale e il pediatra di libera scelta il Codice contiene una disposizione specifica sulle informazioni da dare ai pazienti, che permette di fornire le informazioni in forma semplificata per più trattamenti.", "D.Lgs. 196/2003, art. 78")
    s += ESAMINARE("Il contenuto esatto dell'informativa, l'eventuale forma a più livelli (breve in sala d'attesa e completa a richiesta o sul sito) e l'applicabilità dell'art. 78 vanno decisi con il consulente. Il programma fornisce uno <b>schema</b>, non un testo valido.")

    s += H3("0.3.4 Il registro dei trattamenti")
    s += [P("È obbligatorio per lo studio medico: l'esenzione per organizzazioni con meno di 250 dipendenti <b>non si applica</b> perché i trattamenti non sono occasionali e includono categorie particolari di dati (art. 30, par. 5). "
            "Contenuto e funzionamento sono nel § 6.4. Va tenuto in forma scritta (anche elettronica) e mostrato al Garante se lo chiede.")]

    s += H3("0.3.5 La sicurezza dei dati")
    s += [P("Il vecchio elenco di «misure minime» (allegato B del Codice) non esiste più: dal 2018 la sicurezza si valuta <b>caso per caso</b>, in base al rischio (art. 32). Per uno studio medico, significa almeno:")]
    s += TABLE([
        ["Area", "Misure da mettere in atto o verificare"],
        ["Accesso ai locali e alle cartelle cartacee", "Armadi chiusi a chiave; chi può entrare nello studio e vedere agende e schermi; riservatezza in sala d'attesa e al telefono."],
        ["Computer e programmi", "Account personali con password robuste; blocco schermo automatico; aggiornamenti automatici; antivirus; <b>cifratura del disco</b>; nessun uso promiscuo del computer; software regolarmente licenziato."],
        ["Rete e posta", "Firewall; wi-fi dello studio protetto e separato da quello degli ospiti; <b>niente referti inviati per email non protetta</b> (usare PEC o allegati cifrati, o altri canali sicuri concordati)."],
        ["Telefoni e messaggistica", "Evitare di conservare dati sanitari nelle chat e nelle foto del telefono personale; se inevitabile, protezione del dispositivo e regole scritte; mai l'invio a persone non verificate."],
        ["Copie di sicurezza", "<b>Backup</b> regolari, cifrati, con una copia fuori dallo studio, e prova periodica di ripristino (art. 32, par. 1, lett. c) e d))."],
        ["Supporti e dismissione", "Chiavette e dischi rimovibili cifrati o evitati; distruzione sicura di carta e supporti non più necessari; cancellazione sicura dei dati prima di dismettere o riparare un computer."],
        ["Persone", "Istruzioni scritte e formazione a chi lavora nello studio (§ 0.3.6); vincolo di riservatezza."],
        ["Verifica", "Riesame periodico delle misure e prova che funzionano (art. 32, par. 1, lett. d))."],
    ], [0.27, 0.73])
    s += NORMA("Tenuto conto dello stato dell'arte, dei costi, della natura e delle finalità, il titolare mette in atto misure adeguate al rischio, che comprendono, se del caso: pseudonimizzazione e cifratura; riservatezza, integrità, disponibilità e resilienza dei sistemi; "
               "capacità di ripristinare tempestivamente i dati; una procedura per provare e valutare regolarmente l'efficacia delle misure. Si tiene conto in particolare dei rischi di distruzione, perdita, modifica, divulgazione o accesso non autorizzati.", "Reg. UE 2016/679, art. 32, par. 1 e 2; art. 25 (protezione dei dati fin dalla progettazione e per impostazione predefinita)")
    s += ESAMINARE("L'art. 2-septies del Codice prevede <b>misure di garanzia</b> per il trattamento di dati relativi alla salute, da individuare con provvedimento del Garante: stato e contenuto vigenti vanno verificati. Quanto sia «adeguata» una singola misura (per esempio la cifratura dei campi oltre a quella del disco) è una valutazione tecnica da documentare.")

    s += H3("0.3.6 Chi lavora nello studio: le persone autorizzate")
    s += BUL([
        "Chiunque tratti dati sotto l'autorità del medico (segretaria, collaboratori, sostituti, tirocinanti, personale delle pulizie se può accedere agli archivi) deve essere <b>istruito</b> e, nella prassi consigliata, <b>designato per iscritto</b> con l'indicazione dei dati e dei compiti.",
        "Le istruzioni coprono: accesso solo a ciò che serve, password personali, divieto di copiare dati su dispositivi privati, comportamento al telefono e in sala d'attesa, che cosa fare in caso di incidente (§ 7.3), obbligo di riservatezza anche dopo la fine del rapporto.",
        "La <b>formazione</b> va fatta e documentata (data, partecipanti, argomenti), e ripetuta periodicamente e quando cambia qualcosa.",
    ])
    s += NORMA("Chiunque agisca sotto l'autorità del titolare e abbia accesso a dati personali non può trattare tali dati se non è istruito in tal senso dal titolare. Il titolare può prevedere che compiti e funzioni siano attribuiti a persone fisiche espressamente designate, che operano sotto la sua autorità.",
               "Reg. UE 2016/679, art. 29 e art. 32, par. 4; D.Lgs. 196/2003, art. 2-quaterdecies")

    s += H3("0.3.7 Fornitori, altri professionisti, trasferimenti")
    s += [P("Chi tratta dati <b>per conto</b> del medico è responsabile del trattamento e serve un contratto o altro atto giuridico scritto con il contenuto dell'art. 28, par. 3 (istruzioni documentate, riservatezza, misure di sicurezza, sub-fornitori, assistenza per diritti e violazioni, cancellazione o restituzione a fine rapporto, audit). Casi tipici:")]
    s += TABLE([
        ["Soggetto", "Ruolo di norma", "Da fare"],
        ["Fornitore del gestionale, del cloud, dell'hosting, della posta", "Responsabile (art. 28)", "Contratto con le clausole dell'art. 28; verificare dove stanno i dati e i sub-fornitori."],
        ["Assistenza informatica che può accedere ai dati", "Responsabile (o persona autorizzata se lavora sotto l'autorità del medico)", "Nomina scritta; accessi limitati e tracciati."],
        ["Laboratori, centri diagnostici, specialisti a cui si invia il paziente", "Di norma titolari autonomi", "Nessuna nomina; ma il flusso dei dati va indicato nell'informativa."],
        ["Commercialista, consulente del lavoro", "Dipende dal servizio: titolare autonomo per gli obblighi di legge propri, responsabile per servizi affidati", "Verificare caso per caso e documentare."],
        ["Altro medico in associazione o sostituto", "Contitolare, titolare autonomo o persona autorizzata", "Accordo scritto (art. 26) o designazione."],
    ], [0.34, 0.31, 0.35])
    s += NORMA("Il trattamento da parte di un responsabile è disciplinato da un contratto o altro atto giuridico che vincola il responsabile al titolare e stipula materia, durata, natura e finalità, tipo di dati, categorie di interessati, obblighi e diritti del titolare. "
               "Il titolare ricorre unicamente a responsabili che presentino garanzie sufficienti.", "Reg. UE 2016/679, art. 28, par. 1 e 3; art. 26 (contitolari)")
    s += NORMA("Il trasferimento di dati personali verso un paese terzo (fuori dallo Spazio economico europeo) è ammesso solo con una decisione di adeguatezza della Commissione o con garanzie adeguate (per esempio clausole contrattuali tipo) o in casi specifici.", "Reg. UE 2016/679, artt. 44-49 (capo V)")
    s += ESAMINARE("Molti servizi in cloud, di posta e di videochiamata hanno sedi o sub-fornitori fuori dall'Unione (in particolare negli Stati Uniti). La validità del trasferimento dipende dalle decisioni di adeguatezza (tra cui quella del 2023 per le imprese statunitensi certificate) e dalle garanzie contrattuali, e può cambiare. "
                   "<b>Da verificare con il consulente</b> per ogni servizio che contiene dati sanitari.")

    s += H3("0.3.8 I diritti dei pazienti: la procedura")
    s += BUL([
        "Preparare una <b>procedura</b> (anche di una pagina) che dica a chi arrivano le richieste (email o PEC dello studio), chi le gestisce, come si verifica l'identità del richiedente, come si registrano, come si risponde.",
        "<b>Termine</b>: senza ingiustificato ritardo e al più tardi entro un mese dal ricevimento; prorogabile di due mesi per complessità o numero di richieste, con comunicazione dei motivi entro il primo mese (art. 12, par. 3). Il programma calcola la scadenza (§ 6.5); la regola di calcolo va confermata (cap. 9, punto 2).",
        "<b>Accesso</b> (art. 15): conferma del trattamento, copia dei dati, informazioni su finalità, destinatari, conservazione; la copia della documentazione clinica ha regole e tempi propri da verificare.",
        "<b>Rettifica</b> (art. 16), <b>cancellazione</b> (art. 17: spesso non applicabile ai dati clinici, per obblighi di legge e difesa in giudizio, par. 3), <b>limitazione</b> (art. 18), <b>portabilità</b> (art. 20), <b>opposizione</b> (art. 21).",
        "Le informazioni sono gratuite; si può rifiutare o chiedere un contributo solo per richieste manifestamente infondate o eccessive, con motivazione (art. 12, par. 5). In caso di dubbio ragionevole sull'identità si chiedono informazioni supplementari (art. 12, par. 6).",
        "Per i <b>defunti</b> i diritti possono essere esercitati da chi ha un interesse proprio o agisce a tutela dell'interessato o per ragioni familiari meritevoli di protezione (art. 2-terdecies Codice).",
    ])
    s += ESAMINARE("Il rapporto tra il termine ordinario dell'art. 12 e le disposizioni specifiche sull'accesso alla documentazione sanitaria (per esempio l'art. 4 della legge 24/2017) e, più in generale, <b>che cosa si consegna e in che forma</b> a un paziente che chiede la propria cartella, va chiarito con il consulente.")

    s += H3("0.3.9 Le violazioni dei dati")
    s += [P("Una <b>violazione</b> è ogni evento di sicurezza che porta a distruzione, perdita, modifica, divulgazione o accesso non autorizzati ai dati (art. 4, n. 12), accidentale o illecito. Negli studi medici le più frequenti sono:")]
    s += BUL([
        "smarrimento o furto di computer, telefono, chiavetta, borsa del medico;",
        "email o messaggio con un referto inviato alla persona sbagliata, o in chiaro a un indirizzo sbagliato;",
        "<b>ransomware</b> o guasto che rende i dati indisponibili (anche la sola indisponibilità è una violazione);",
        "accesso non autorizzato a un account, a un programma o alla posta;",
        "documenti cartacei lasciati incustoditi o smaltiti senza distruzione.",
    ])
    s += BUL([
        "Avere una <b>procedura scritta</b> (chi decide, chi avvisa, a chi) e <b>registrare ogni violazione</b>, anche se non notificata (art. 33, par. 5).",
        "<b>Notificare al Garante senza ingiustificato ritardo e, ove possibile, entro 72 ore</b> da quando se ne è venuti a conoscenza, a meno che sia improbabile un rischio per i diritti e le libertà degli interessati (art. 33, par. 1). Oltre le 72 ore vanno indicati i motivi del ritardo.",
        "<b>Comunicare agli interessati</b>, senza ingiustificato ritardo, se il rischio è elevato (art. 34), salvo le eccezioni del par. 3.",
    ])
    s += [P("Procedura operativa nel § 7.3; registrazione nel programma al § 6.6.")]

    s += H3("0.3.10 Conservazione e cancellazione")
    s += BUL([
        "Per ogni tipo di dato <b>stabilire e scrivere</b> per quanto tempo si conserva e perché (art. 5, par. 1, lett. e); art. 30, par. 1, lett. f)); il periodo o i criteri vanno indicati anche nell'informativa (art. 13, par. 2, lett. a)).",
        "A fine periodo i dati vanno <b>cancellati o resi anonimi</b>, con modalità sicure, e la cancellazione va documentata.",
        "Documentazione amministrativa e contabile: scritture contabili generalmente per <b>dieci anni</b> (art. 2220 c.c.); altri documenti fiscali secondo la normativa tributaria.",
    ])
    s += ESAMINARE("<b>La durata di conservazione della documentazione clinica dello studio individuale non è fissata da una regola unica e semplice</b>: dipende da obblighi di legge, esigenze di difesa in giudizio, indicazioni dell'Ordine e regole regionali; per le cartelle cliniche delle strutture sanitarie la prassi è molto lunga o illimitata. "
                   "Va deciso e motivato per iscritto con il consulente. Il programma non tiene la cartella clinica e non propone durate.")

    s += H3("0.3.11 Valutazione d'impatto e responsabile della protezione dei dati")
    s += BUL([
        "<b>DPIA</b> (art. 35): obbligatoria quando un trattamento può presentare un rischio elevato, in particolare per trattamenti <b>su larga scala</b> di categorie particolari di dati. Il considerando 91 precisa che il trattamento dei dati dei pazienti da parte di un <b>singolo professionista sanitario</b> non va considerato su larga scala.",
        "<b>DPO</b> (art. 37): obbligatorio, tra l'altro, se l'attività principale consiste in trattamenti su larga scala di categorie particolari di dati. Il Garante ha indicato nelle sue FAQ che il singolo professionista sanitario in libera professione a titolo individuale <b>non</b> è, di regola, tenuto a designarlo; le linee guida europee sul DPO (WP 243) ragionano allo stesso modo.",
        "<b>Anche se non obbligatori</b>, conviene <b>scrivere la valutazione</b> (una pagina): perché si ritiene che DPIA e DPO non servano, in base a quali elementi dello studio (un solo medico, numero di pazienti, assenza di tecnologie innovative).",
    ])
    s += ESAMINARE("La conclusione cambia con le dimensioni e l'organizzazione dello studio: più medici, grande numero di pazienti, <b>telemedicina o strumenti innovativi</b> (applicazioni, intelligenza artificiale, dispositivi connessi), videosorveglianza. "
                   "Il Garante ha pubblicato un elenco di trattamenti per cui la DPIA è richiesta (provvedimento dell'11 ottobre 2018): <b>da consultare con il consulente</b> prima di concludere.")

    s += H3("0.3.12 Situazioni particolari")
    s += TABLE([
        ["Situazione", "Che cosa considerare"],
        ["<b>Minori</b> e persone sotto tutela", "L'informativa va dalla parte di chi esercita la responsabilità genitoriale o la tutela, con linguaggio adatto al minore; documentare i poteri di chi accompagna il paziente. Il consenso del minore per i servizi online ha limiti d'età propri (art. 8 GDPR; art. 2-quinquies Codice), diversi dal consenso alle cure."],
        ["<b>Telemedicina</b>, videovisite, app", "Piattaforma come responsabile o contitolare; sicurezza del collegamento; informativa specifica; verifica dei trasferimenti fuori UE; possibile DPIA. Le regole sanitarie sulla telemedicina sono distinte da quelle privacy."],
        ["<b>Fascicolo sanitario elettronico</b> e sistemi regionali", "Disciplina specifica, con regole proprie sul consenso all'alimentazione e alla consultazione; seguire le istruzioni della Regione e le regole sul FSE (art. 12 D.L. 179/2012 e decreti attuativi): <b>da verificare</b>."],
        ["<b>Sito web</b> dello studio", "Informativa sul sito; cookie e strumenti di tracciamento secondo le linee guida del Garante (giugno 2021); moduli di contatto che raccolgono dati; non pubblicare dati o immagini di pazienti senza una base valida."],
        ["<b>Comunicazioni promozionali</b>, recensioni, social", "Il marketing diretto richiede consenso (art. 130 Codice per le comunicazioni elettroniche); mai usare dati sanitari a fini promozionali senza base valida; attenzione a foto e storie dei pazienti."],
        ["<b>Videosorveglianza</b>", "Se presente: cartelli informativi, base giuridica, limiti su riprese di aree dove si trattano dati sanitari, tempi di conservazione delle immagini; le linee guida europee in materia (3/2019) sono il riferimento."],
        ["<b>Dipendenti e collaboratori</b>", "Ulteriori informative (lavoratori), registro, accessi; disciplina del lavoro sui controlli a distanza."],
    ], [0.27, 0.73])

    # ------------------------------------------------------------ 0.4
    s += H2("0.4 I documenti che lo studio dovrebbe avere")
    s += [P("Al termine del percorso, in uno stesso luogo (cartella o archivio), lo studio dovrebbe poter mostrare:")]
    s += TABLE([
        ["Documento", "Contenuto", "In Privacy Studio"],
        ["Registro dei trattamenti", "Attività, finalità, basi giuridiche, categorie, destinatari, conservazione, misure", "Sì (parziale, § 6.4)"],
        ["Informativa ai pazienti", "Testo approvato, versione e data; prova della consegna", "Sì (§ 6.3)"],
        ["Designazioni delle persone autorizzate", "Nomi, compiti, istruzioni, formazione ricevuta", "No"],
        ["Elenco dei fornitori e contratti con i responsabili", "Chi tratta dati per conto dello studio e con quale atto", "No (previsto)"],
        ["Procedura per i diritti e registro delle richieste", "Chi fa che cosa; richieste, esiti, tempi", "Sì (§ 6.5)"],
        ["Procedura per le violazioni e registro delle violazioni", "Istruzioni, ruoli, registro anche delle non notificate", "Sì (§ 6.6)"],
        ["Valutazione dei rischi e delle misure di sicurezza", "Misure in atto, rischi residui, piano di miglioramento", "In parte (backup, accesso)"],
        ["Regole di conservazione e registro delle eliminazioni", "Durate, motivazioni, eliminazioni eseguite", "Sì (§ 6.7)"],
        ["Valutazione su DPO e DPIA", "Perché si ritengono non necessari, o i documenti se necessari", "No"],
        ["Documentazione della formazione", "Date, partecipanti, argomenti", "No"],
    ], [0.34, 0.44, 0.22])

    # ------------------------------------------------------------ 0.5
    s += H2("0.5 Da dove cominciare e come mantenersi in regola")
    s += H3("Un ordine ragionevole")
    s += STEPS([
        "<b>Prima settimana</b>: mappare trattamenti e dati; decidere ruoli e fornitori; mettere in sicurezza il minimo urgente (disco cifrato, aggiornamenti, backup su supporto separato).",
        "<b>Primo mese</b>: scrivere basi giuridiche e registro; redigere l'informativa e iniziare a consegnarla; designare le persone autorizzate; preparare le due procedure (diritti e violazioni).",
        "<b>Entro tre mesi</b>: contratti con i responsabili; decisioni su conservazione, DPO e DPIA; formazione; prova di ripristino del backup.",
    ])
    s += H3("Mantenimento")
    s += BUL([
        "<b>Ogni anno</b>: riesame del registro, dell'informativa, delle durate di conservazione, delle misure di sicurezza e dell'elenco dei fornitori; ripetere la formazione.",
        "<b>Quando cambia qualcosa</b> (nuovo software, nuovo fornitore, nuova sede, nuovo collaboratore, nuovo servizio): aggiornare registro e informativa e rivalutare i rischi.",
        "<b>Dopo ogni violazione o richiesta difficile</b>: chiedersi che cosa migliorare.",
    ])
    s += ATTENZIONE("Gli errori più comuni negli studi medici: referti inviati per email non protetta o alla persona sbagliata; dati sanitari nelle chat del telefono personale; computer senza cifratura né backup verificato; "
                    "nessuna informativa consegnata o nessuna prova di averla data; collaboratori senza istruzioni scritte; fornitori informatici con accesso ai dati e nessuna nomina; nessuna procedura per un incidente.")

    # ------------------------------------------------------------ 0.6
    s += H2("0.6 Dove Privacy Studio aiuta, e dove no")
    s += TABLE([
        ["Adempimento", "Privacy Studio", "Resta al titolare"],
        ["Mappa dei trattamenti e registro", "Registro versionato, esportabile alla data (§ 6.4)", "Decidere finalità e basi; completare sistemi, misure, fornitori, trasferimenti"],
        ["Informativa", "Schema, pubblicazione immutabile, registro delle consegne, verifica dell'integrità (§ 6.3)", "Contenuto giuridico; stampa e consegna; rappresentanti legali"],
        ["Diritti degli interessati", "Registrazione, scadenze, proroga, decisione, chiusura con prova (§ 6.5)", "Valutare e preparare il riscontro; identificazione del richiedente"],
        ["Violazioni", "Orologio delle 72 ore, valutazione, registro, protocollo di notifica (§ 6.6)", "Decidere se è violazione e se notificare; fare la notifica"],
        ["Conservazione", "Regole, elementi scaduti, eliminazione guidata e documentata (§ 6.7)", "Stabilire le durate; documentazione clinica fuori dal programma"],
        ["Sicurezza dei dati", "Accesso con blocco, audit a catena, backup cifrati verificati, ripristino (cap. 8)", "Computer, rete, locali, telefoni, formazione"],
        ["Persone autorizzate, fornitori, DPO/DPIA, formazione", "Nulla (per ora)", "Tutto"],
    ], [0.24, 0.40, 0.36])
    s += NOTA("Usare Privacy Studio <b>non rende da solo</b> lo studio conforme: aiuta a documentare e a rispettare le scadenze per alcuni adempimenti. Gli altri si fanno fuori dal programma, con il consulente.")

    # ------------------------------------------------------------ 0.7
    s += H2("0.7 Che cosa si rischia se non si è in regola")
    s += BUL([
        "<b>Poteri del Garante</b>: ispezioni, richieste di informazioni, ammonimenti, limitazioni o divieti di trattamento, ordini di adeguamento (art. 58); reclami degli interessati (art. 77).",
        "<b>Sanzioni amministrative pecuniarie</b>: fino a 10 milioni di euro (o il 2% del fatturato) per la violazione, tra gli altri, degli obblighi su registro, sicurezza, violazioni, nomine; fino a 20 milioni (o il 4%) per violazione dei principi, delle condizioni di liceità, dei diritti degli interessati (art. 83, par. 4 e 5). L'importo si gradua sul caso concreto (art. 83, par. 2).",
        "<b>Risarcimento</b> dei danni, materiali e immateriali, a chi li ha subiti (art. 82).",
        "<b>Sanzioni penali</b> del Codice per i casi più gravi, come il trattamento illecito di dati (art. 167 e seguenti), e le conseguenze sul piano <b>disciplinare</b> e della responsabilità professionale per la violazione del segreto.",
    ])
    s += ESAMINARE("Le sanzioni descritte sono i <b>massimi di legge</b>, non l'esito tipico; l'importo e l'applicabilità di ciascuna ipotesi dipendono dal caso e dalle norme vigenti. Informazione generale, non valutazione del rischio dello studio.")
    return s
