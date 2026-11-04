# -*- coding: utf-8 -*-
from lib import *

def story():
    s = []
    # ---------------------------------------------------------------- Avvertenze
    s += H1("Avvertenze e modo di lettura")
    s += [P("Questo manuale accompagna il titolare del trattamento, cioè il medico, dall'installazione di Privacy Studio all'uso delle sue funzioni, "
            "collegando ogni funzione alle norme che la motivano. È scritto per chi non è un tecnico: dove serve un'operazione informatica, "
            "lo si dice e si indica a chi chiederla.")]
    s += H2("Che cosa è e che cosa non è questo documento")
    s += BUL([
        "<b>È</b> una guida operativa: spiega che cosa fa il programma, come si usa e quali norme ne sono alla base.",
        "<b>Non è</b> una consulenza legale né un parere di conformità. Il programma aiuta a documentare e a rispettare le scadenze; "
        "<b>non certifica</b> che lo studio sia conforme al GDPR e non decide al posto del titolare.",
        "<b>Non sostituisce</b> il consulente privacy, il commercialista o l'Ordine professionale sui punti in cui la norma lascia margini di scelta.",
    ])
    s += H2("Come sono segnalati i punti da verificare")
    s += [P("Le norme sono citate nella forma in cui chi scrive le conosce alla data del documento. La legge cambia e le autorità emanano nuovi provvedimenti: "
            "prima di basarsi su una citazione per una decisione importante, controllarne il testo vigente. Nel manuale compaiono quattro tipi di riquadro:")]
    s += NORMA("La norma (articolo del GDPR, del Codice privacy, ecc.) da cui nasce la funzione o l'obbligo.")
    s += ESAMINARE("Un punto <b>di cui non si ha certezza</b>, interpretabile o rimesso al titolare e al consulente. Sono tutti raccolti nel capitolo 9, con chi dovrebbe decidere.")
    s += ATTENZIONE("Un rischio concreto: operazione irreversibile, scadenza, possibile perdita di dati.")
    s += CONSIGLIO("Una buona pratica, non un obbligo.")
    s += H2("Stato del programma")
    s += ATTENZIONE([
        "Privacy Studio è in fase di <b>pre-rilascio</b>. È stato provato con dati sintetici (inventati) e con test automatici, non ancora con dati reali "
        "né su un computer Windows \"pulito\" come quello dello studio.",
        "<b>Non usarlo con dati reali dei pazienti</b> finché non sono soddisfatte le condizioni di rilascio elencate nel capitolo 10 "
        "(tra le altre: prova di ripristino su una macchina pulita, decisione sulla gestione delle chiavi, conferma della regola dei termini)."])
    s += NOTA("Il capitolo 0 vale per ogni medico, a prescindere dal programma; il capitolo 10 elenca che cosa il programma fa e che cosa manca. Le schermate riprodotte usano <b>dati inventati</b> (studio, persone, richieste e incidenti fittizi); i valori che vi compaiono, per esempio le durate di conservazione, sono esempi e <b>non indicazioni</b>.")
    # ---------------------------------------------------------------- 0
    import part_0
    s += part_0.story()

    # ---------------------------------------------------------------- 1
    s += H1("1. A che cosa serve Privacy Studio")
    s += H2("1.1 Lo scopo")
    s += [P("Un medico che esercita in proprio è <b>titolare del trattamento</b> dei dati dei pazienti (art. 4, n. 7 del Regolamento) e tratta dati relativi alla salute, "
            "che il Regolamento colloca fra le categorie particolari (art. 9). Questo comporta obblighi di documentazione, di trasparenza verso i pazienti, "
            "di risposta alle loro richieste, di gestione degli incidenti e di sicurezza. Privacy Studio raccoglie questi adempimenti in un solo strumento, "
            "tenuto sul computer dello studio, e conserva le prove di ciò che è stato fatto (il cosiddetto principio di responsabilizzazione, art. 5, par. 2 e art. 24).")]
    s += NORMA("Il titolare deve essere in grado di <b>dimostrare</b> di aver rispettato i principi del trattamento (liceità, correttezza, trasparenza, limitazione della finalità, "
               "minimizzazione, esattezza, limitazione della conservazione, integrità e riservatezza).", "Reg. UE 2016/679, art. 5 e art. 24")
    s += H2("1.2 Le sette funzioni")
    s += TABLE([
        ["Funzione", "Che cosa permette", "Norma di riferimento"],
        ["<b>F01</b> Registro dei trattamenti", "Elencare le attività di trattamento, le finalità, le basi giuridiche, le categorie di dati e di interessati; pubblicare versioni immutabili ed esportare il registro a una data.", "Art. 30 GDPR"],
        ["<b>F02</b> Informative e consegne", "Redigere e pubblicare l'informativa; registrare a chi e quando è stata consegnata, anche senza firma; ricostruire il modulo emesso.", "Artt. 12, 13, 14 GDPR"],
        ["<b>F03</b> Richieste degli interessati", "Registrare le richieste di accesso, rettifica, cancellazione ecc.; calcolare la scadenza; prorogare; decidere; chiudere con prova del riscontro.", "Artt. 12, 15-22 GDPR; art. 2-terdecies Codice"],
        ["<b>F04</b> Violazioni dei dati", "Registrare un incidente, avviare le 72 ore, valutare il rischio, documentare le azioni e la notifica al Garante.", "Artt. 33 e 34 GDPR"],
        ["<b>F05</b> Conservazione", "Fissare le durate di conservazione, vedere ciò che è scaduto, eliminare in modo guidato e documentato.", "Art. 5, par. 1, lett. e) GDPR"],
        ["<b>F06</b> Backup e ripristino", "Copie cifrate e verificate, copia fuori dal computer, ripristino su una macchina pulita, chiavi di recupero.", "Art. 32 GDPR"],
        ["<b>F07</b> Rimozione di un interessato", "Azzerare l'anagrafica e rendere anonime le prove di consegna, conservando la prova minima.", "Art. 17 GDPR (con i limiti dell'art. 17, par. 3)"],
    ], [0.22, 0.52, 0.26])
    s += H2("1.3 Che cosa Privacy Studio non fa")
    s += BUL([
        "<b>Non è una cartella clinica.</b> Non contiene diagnosi, terapie, referti né l'agenda delle visite. Per il registro dei trattamenti non servono le singole visite dei pazienti.",
        "<b>Non invia nulla</b> al Garante o ai pazienti: registra ciò che il medico ha fatto (per esempio il numero di protocollo della notifica) e ricorda le scadenze.",
        "<b>Non decide.</b> Non classifica da solo un incidente come violazione, non stabilisce se una richiesta va accolta, non fissa le durate di conservazione.",
        "<b>Non gestisce ancora</b> (previsto per una versione successiva): consensi e revoche per finalità diverse dalla cura, più utenti con ruoli diversi, contratti con i fornitori (responsabili del trattamento), ricerca in linguaggio naturale.",
    ])
    s += H2("1.4 I principi di funzionamento")
    s += BUL([
        "<b>Dati ridotti al necessario</b> (art. 5, par. 1, lett. c): il programma non raccoglie ciò che non serve ai suoi scopi.",
        "<b>Il medico decide, il programma documenta</b>: nessuna scelta giuridica è preimpostata; nei moduli nessuna casella è già selezionata.",
        "<b>Ciò che è stato emesso non cambia</b>: una versione pubblicata del registro o dell'informativa non si modifica; si crea una nuova versione.",
        "<b>Tutto è tracciato</b>: ogni operazione importante finisce in un registro di audit a catena, che permette di accorgersi di manomissioni.",
        "<b>Le regole stanno nel database</b>: scadenze, immutabilità e completezza non dipendono da come è disegnata una schermata.",
    ])

    # ---------------------------------------------------------------- 2
    s += H1("2. Il quadro normativo in sintesi")
    s += [P("Questo capitolo riassume le norme che il programma presuppone. Per ognuna è indicato dove, nel programma, se ne vede l'effetto. "
            "Non è un elenco completo degli obblighi di uno studio medico: copre ciò che il programma tocca.")]
    s += H2("2.1 Le fonti")
    s += TABLE([
        ["Fonte", "Che cosa interessa lo studio"],
        ["<b>Regolamento (UE) 2016/679</b> (GDPR)", "La disciplina generale: principi (art. 5), basi di liceità (art. 6), categorie particolari di dati (art. 9), trasparenza e diritti (artt. 12-22), registro (art. 30), sicurezza (art. 32), violazioni (artt. 33-34), valutazione d'impatto (art. 35), responsabile della protezione dei dati (artt. 37-39)."],
        ["<b>D.Lgs. 196/2003</b> (Codice in materia di protezione dei dati personali), come modificato dal D.Lgs. 101/2018", "Le norme nazionali di adeguamento. Interessano in particolare l'art. 2-terdecies (diritti delle persone decedute), l'art. 2-quaterdecies (persone autorizzate al trattamento) e la Parte II, Titolo V, artt. 75 e seguenti (trattamento di dati in ambito sanitario)."],
        ["<b>Provvedimenti e linee guida del Garante e dell'EDPB</b>", "Chiarimenti operativi: ad esempio sul trattamento dei dati sanitari in ambito sanitario (Garante, provvedimento del 7 marzo 2019) e sulla notifica delle violazioni (linee guida dell'EDPB sulla notifica di violazioni dei dati personali)."],
        ["<b>Regolamento (CEE, Euratom) n. 1182/71</b>", "Le regole generali sul calcolo dei termini (giorni, mesi, festività). Il programma le applica per la scadenza delle risposte agli interessati. <b>Come vanno applicate qui è un punto da esaminare</b> (§ 6.5)."],
        ["<b>Codice civile e normativa fiscale</b>", "Conservazione di scritture e documenti contabili (es. art. 2220 del codice civile). Rilevante per fatture e documenti amministrativi, non per i dati clinici."],
    ], [0.34, 0.66])
    s += H2("2.2 Perché non serve il consenso per curare, e quando serve")
    s += [P("Il trattamento di dati sulla salute per finalità di cura, da parte di un professionista sanitario tenuto al segreto professionale, "
            "non richiede il consenso dell'interessato: si basa sull'art. 9, par. 2, lett. h) e par. 3 del Regolamento, insieme a una base dell'art. 6 "
            "(di norma l'esecuzione di un contratto o di misure precontrattuali, art. 6, par. 1, lett. b). Il Garante lo ha chiarito nel provvedimento del 7 marzo 2019. "
            "Per questo il programma registra la <b>consegna dell'informativa</b> e non un \"consenso\" del paziente.")]
    s += NORMA("Il divieto di trattare dati sulla salute (art. 9, par. 1) non si applica se il trattamento è necessario per finalità di medicina preventiva, diagnosi, assistenza o terapia, "
               "sulla base del diritto dell'Unione o degli Stati membri o di un contratto con un professionista sanitario, quando i dati sono trattati da o sotto la responsabilità di un "
               "professionista soggetto al segreto professionale.", "Reg. UE 2016/679, art. 9, par. 2, lett. h) e par. 3")
    s += ESAMINARE("Il consenso resta necessario per finalità <b>diverse dalla cura</b> (per esempio comunicazioni promozionali, alcune forme di ricerca, adesione a servizi come il fascicolo sanitario elettronico, "
                   "secondo la disciplina specifica). La gestione di consensi e revoche non è ancora nel programma. Quali trattamenti dello studio richiedano un consenso va verificato con il consulente.")
    s += H2("2.3 Gli obblighi che il programma non copre da solo")
    s += BUL([
        "<b>Persone autorizzate</b> (art. 29 GDPR; art. 2-quaterdecies Codice): chi lavora nello studio sotto l'autorità del titolare (segretaria, collaboratori) deve essere istruito e designato. "
        "Il programma ha un solo utente (il medico); le istruzioni alle persone autorizzate vanno predisposte a parte.",
        "<b>Responsabili del trattamento</b> (art. 28 GDPR): fornitori che trattano dati per conto dello studio (assistenza informatica, servizi in cloud, commercialista per i dati che gli sono affidati) richiedono un contratto o atto di nomina. Non sono ancora gestiti dal programma.",
        "<b>Valutazione d'impatto</b> (art. 35) e <b>responsabile della protezione dei dati</b> (artt. 37 ss.): vedi il riquadro sotto.",
        "<b>Misure di sicurezza dell'ambiente</b> (art. 32): computer aggiornato, disco cifrato, antivirus, accessi, locali. Il programma ne tratta una parte (cap. 3 e 8).",
    ])
    s += ESAMINARE("<b>DPO e valutazione d'impatto.</b> Il considerando 91 del Regolamento afferma che il trattamento dei dati dei pazienti da parte di un singolo professionista sanitario non dovrebbe essere considerato \"su larga scala\", "
                   "e il Garante ha precisato nelle proprie FAQ che il singolo professionista sanitario non è tenuto, di regola, a designare un DPO. "
                   "Sulla base di ciò, per un medico che esercita da solo né il DPO né la valutazione d'impatto sono di norma obbligatori. "
                   "La conclusione dipende però dalle dimensioni e dall'organizzazione reali dello studio (più medici, telemedicina, trattamenti innovativi): <b>da confermare con il consulente</b>.")
    return s
