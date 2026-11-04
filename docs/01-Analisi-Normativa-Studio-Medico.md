# Privacy Studio — Analisi normativa per lo studio di un singolo medico

Data: 4 novembre 2026. Versione 2.0. Documento 1 di 3 (seguono *02 Specifica funzionale e dati* e *03 Piano di test e rilascio*).

Scopo: fissare, per un medico libero professionista in Italia, quali obblighi in materia di protezione dei dati l'applicazione deve aiutare a eseguire e dimostrare. Non attesta conformità e non sostituisce la valutazione dei trattamenti effettivamente svolti né il parere di un consulente.

Convenzione: **N** = obbligo normativo da valutare nel contesto; **P** = scelta progettuale raccomandata per soddisfarlo; **C** = decisione che compete al titolare.

## 1. Perimetro sanitario

L'assenza di diagnosi o referti non rende ordinario il contenuto di un gestionale privacy. L'identità del paziente, il legame con uno specialista, il nome di una prestazione o di un modulo possono rivelare informazioni sulla salute. Il Garante lo esplicita per i dati di prenotazione nelle piattaforme sanitarie [S4]; la fonte riguarda quel contesto, ma il principio vale per qualunque archivio che colleghi persona e professionista.

**P** Classificare dati e documenti per ciò che possono rivelare. Limitare campi liberi, nomi di file, testi d'errore e contenuto dei log. Usare dati fittizi fino al collaudo di protezione e ripristino. Un codice paziente è pseudonimizzazione, non anonimizzazione.

## 2. Cura, informativa e consenso

**N** Per i trattamenti necessari alla cura effettuati da un professionista tenuto al segreto professionale, il consenso privacy non è richiesto (art. 9(2)(h) e 9(3) GDPR); le finalità ulteriori richiedono un presupposto distinto [S2]. Il singolo professionista non è esentato dal registro dei trattamenti perché piccolo, trattando categorie particolari (art. 30(5)) [S2, S5].

Il software distingue:

- presupposto del trattamento (art. 6 e condizione dell'art. 9);
- consegna dell'informativa e sua versione;
- eventuale consenso specifico: prova, ambito, revoca;
- consenso informato alla prestazione sanitaria, processo distinto e fuori perimetro.

La firma per presa visione è una modalità di documentazione scelta dallo studio, non un obbligo degli artt. 13–14. La mancata firma non è mancato consenso alla cura. **P** Registrare anche la consegna senza firma, con modalità e operatore, senza dichiarazioni non veritiere.

**P** Sostituire il booleano `consent_required` con una valutazione motivata (esito, motivazione, autore, data, versione della finalità). Registrare solo le basi giuridiche effettivamente applicabili, con il rispettivo ambito.

## 3. Informativa: requisiti per l'uso reale

Prima di consegnarla al paziente occorre (art. 13–14 [S3]):

- identificare il titolare effettivo, senza confondere insegna, medico e struttura;
- completare contatti, finalità, destinatari e trasferimenti reali;
- collegare ogni finalità al presupposto applicabile;
- indicare conservazione o criteri, conferimento e conseguenze del mancato conferimento;
- considerare l'art. 14 quando i dati provengono da terzi;
- niente scelte preselezionate né accettazione cumulativa.

La versione deve essere approvata dal titolare per lo studio reale: un modello generico non prova l'adempimento.

## 4. Registro dei trattamenti

**N** Il registro rappresenta le attività effettive e resta aggiornato; contiene categorie di interessati e di dati, destinatari, trasferimenti, termini di cancellazione e misure di sicurezza (art. 30; FAQ [S5]).

**P**
- Separare l'attività organizzativa dalle singole visite: il registro non elenca i pazienti.
- Includere dipendenti, fornitori e altri interessati se presenti.
- Versionare le attività insieme alle loro associazioni, perché una tabella storica della sola descrizione non ricostruisce il registro passato.
- Consentire bozze incomplete, impedire la pubblicazione di schede senza i contenuti necessari.
- Esportare il registro in forma leggibile con la data di riferimento.

Il versionamento è una scelta tecnica per dimostrare gli aggiornamenti, non uno schema imposto dalla legge.

## 5. Prova della consegna dell'informativa

L'obbligo è **fornire** l'informativa (art. 13), non conservarne una copia nominativa per ogni paziente. **P** Per dimostrarlo bastano: versione pubblicata dell'informativa (testo immutabile), registro della consegna (chi, quando, come, a quale persona, eventuale rappresentante) e, se esiste, la ricevuta firmata acquisita. Conservare un PDF personalizzato per paziente aumenta i dati trattati; va fatto solo se il titolare lo decide (**C**).

Un hash verifica la corrispondenza dei contenuti, non prova firmatario né data certa. Una scansione non è una firma elettronica qualificata. L'immutabilità delle versioni approvate deve convivere con regole di conservazione e cancellazione legittima.

## 6. Conservazione

**N** I dati sono conservati per il tempo necessario alle finalità (art. 5(1)(e)); il registro riporta, ove possibile, i termini previsti per la cancellazione.

Non ricavare una regola unica da affermazioni come «cartelle cliniche illimitate», né adottare dieci anni per tutto. Distinguere documentazione clinica, fiscale, informative, prove di consegna e consenso, richieste, incidenti, log, backup. Per ciascuna classe: fonte, finalità, decorrenza, criterio, responsabile della verifica **(C)**. Le durate dipendono anche da specializzazione e contesto professionale e non sono determinate in questo documento.

**P** Regola versionata; evento di decorrenza; sospensione motivata (contenzioso o altro); data di revisione; cancellazione o anonimizzazione documentata. «Illimitata» è un valore esplicito e motivato, mai un NULL. Coprire storico, scansioni, esportazioni, backup e futuri embedding. Niente cascata automatica sulle prove, niente conservazione indefinita tramite soft delete.

## 7. Diritti degli interessati

**N** Riscontro entro un mese dal ricevimento, prorogabile di due mesi nelle condizioni dell'art. 12(3), con comunicazione entro il primo mese [S6, S7]. Non tradurre in «30 giorni». Il modello del Garante è facoltativo.

Il computo del termine (giorno iniziale, fine mese, giorni non lavorativi) è trattato dall'EDPB con riferimento al Reg. 1182/71; **la regola esatta va confermata dal consulente sul testo** e codificata in una funzione versionata e testata (vedi documento 2, F04).

Elementi da gestire: ricezione da qualsiasi canale, diritti invocati, verifica d'identità proporzionata (non sempre copia del documento), rappresentanza, scadenza, decisione motivata, riscontro, prova dell'invio. Nessuna sospensione implicita del termine.

Casi specifici da non dimenticare:
- richiesta da parte di collaboratori o altri non pazienti: non creare un falso paziente;
- **interessati deceduti**: l'art. 2-terdecies del Codice Privacy consente l'esercizio dei diritti a chi ha un interesse proprio, agisce a tutela dell'interessato come mandatario o per ragioni familiari meritevoli di protezione (verificare limiti e condizioni sul testo vigente);
- **rettifica (art. 16) e cancellazione (art. 17)** in tensione con documenti emessi immutabili: serve una procedura che conservi la prova minima giustificata senza mantenere i dati eccedenti (documento 2, §7).

## 8. Titolare, autorizzati, fornitori

**N** Distinguere titolare, contitolare, responsabile e persone autorizzate (artt. 26–29, 2-quaterdecies Codice). Il ruolo dipende dal servizio e dalle decisioni effettive, non dalla professione o dalla ragione sociale.

Un contratto allegato non prova da solo il rispetto dell'art. 28: servono oggetto, durata, istruzioni, riservatezza, misure, sub-responsabili, assistenza, restituzione/cancellazione, verifiche. Vale anche per un fornitore locale con assistenza remota.

**Lo sviluppatore o manutentore del software** che acceda ai dati dello studio (assistenza, backup remoto, aggiornamenti con accesso al database) va valutato come possibile responsabile del trattamento; se non vi accede, la procedura di assistenza deve garantirlo **(C)**.

Gli incarichi interni richiedono istruzioni e accessi coerenti con i compiti. L'applicazione desktop deve dichiarare che chi controlla l'account Windows o il ruolo PostgreSQL può aggirare alcune restrizioni applicative.

## 9. DPO, DPIA, minori e ambiti particolari

DPO e DPIA non sono automatici per ogni medico: dipendono da natura, scala e rischio (artt. 35, 37) [S2, S10]. **P** Prevedere una valutazione documentata di applicabilità, con revisione al mutare dell'attività; non produrre un automatismo «DPIA non necessaria».

Genetica, medicina del lavoro, ricerca, FSE/dossier, minori e telemedicina richiedono verifica specifica. Le prescrizioni del Garante del 5 giugno 2019 hanno ambiti distinti [S8]. La soglia per i servizi della società dell'informazione (art. 8 GDPR, art. 2-quinquies Codice) non è la regola generale del consenso sanitario dei minori. Una delega al ritiro di un documento non conferisce il potere di prestare ogni consenso.

Se si introducono funzioni di intelligenza artificiale, verificare anche la normativa nazionale sopravvenuta (ad esempio L. 132/2025) e il Regolamento (UE) 2024/1689.

## 10. Violazioni dei dati personali

**N** Notifica al Garante senza ingiustificato ritardo e, ove possibile, entro 72 ore dalla conoscenza, salvo improbabilità del rischio; comunicazione agli interessati se il rischio è elevato; documentazione di tutte le violazioni anche se non notificate (artt. 33–34) [S9]. Il computo è in ore dall'istante di conoscenza, non in giorni lavorativi. Una perdita di disponibilità può essere una violazione anche senza esfiltrazione. **P** Prevedere motivazione del ritardo e notifiche per fasi; non decidere in automatico da un punteggio.

## 11. Sicurezza

**N** L'art. 32 richiede misure adeguate al rischio, non prescrive PostgreSQL, un algoritmo né la cifratura di ogni campo. Valutare riservatezza, integrità, disponibilità, ripristino e verifica periodica.

**P** Prima dei dati reali: ruoli distinti per migrazione e operatività; niente credenziali universali; protezione di credenziali e chiavi con recupero; blocco persistente dei tentativi; disco e backup cifrati con prova di ripristino su altra macchina; query parametrizzate e controllo dei file acquisiti; audit proporzionato senza dati sanitari nei log; aggiornamenti; cancellazione effettiva e gestione dei dati ripristinati da backup.

Modello di minaccia: la cifratura del disco protegge il supporto spento; una chiave accessibile all'app non ferma un amministratore compromesso; un log scritto dallo stesso ruolo non è inviolabile. Per il primo rilascio dichiarare garanzie reali e limiti verificati.

## 12. Decisioni che spettano al medico

Specializzazione e rapporto con SSN/strutture; personale e assistenza informatica; minori e rappresentanti; servizi e destinatari effettivi; categorie documentali; canali di comunicazione; durate di conservazione; eventuali attività ulteriori. Queste risposte determinano i contenuti, non solo le etichette dell'interfaccia.

Il registro include anche trattamenti svolti fuori dal programma. La conformità dipende dalle procedure dello studio e dai servizi usati, oltre che dal software.

## 13. Mappa requisito → riferimento

| Requisito | Riferimento | Tipo |
|---|---|---|
| Registro delle attività con contenuti minimi | art. 30 GDPR [S5] | N |
| Registro versionato e esportabile | — | P |
| Informativa con contenuti completi | artt. 13–14 [S3] | N |
| Firma per presa visione | scelta dello studio | C/P |
| Nessun consenso per la cura | art. 9(2)(h), 9(3) [S2] | N |
| Termine di un mese, proroga | art. 12(3) [S6] | N |
| Regola di computo del termine | Reg. 1182/71 (da confermare) | N/P |
| Diritti degli eredi | art. 2-terdecies Codice | N |
| Notifica violazione 72 h | artt. 33–34 [S9] | N |
| Valutazione DPIA/DPO | artt. 35, 37 [S10] | N |
| Misure di sicurezza adeguate | art. 32 | N |
| Cifratura, backup, ripristino, audit | art. 32 | P |
| Durate di conservazione | art. 5(1)(e) | N/C |

## Fonti

[S1] GDPR, testo ufficiale EUR-Lex: https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32016R0679 — copia informativa del Garante: https://www.garanteprivacy.it/il-testo-del-regolamento (non è stata eseguita una verifica esaustiva della normativa settoriale sopravvenuta).

[S2] Garante, chiarimenti sanitari 7 marzo 2019: https://www.gpdp.it/web/guest/home/docweb/-/docweb-display/docweb/9091942

[S3] Garante, doveri e informativa: https://www.garanteprivacy.it/web/guest/home/docweb/-/docweb-display/docweb/8981258

[S4] Garante, compendio sulle piattaforme sanitarie (ambito: piattaforme che mettono in contatto pazienti e professionisti): https://www.garanteprivacy.it/documents/10160/0/Compendio%2Bsul%2Btrattamento%2Bdei%2Bdati%2Bpersonali%2Battraverso%2Bpiattaforme%2Bvolte%2Ba%2Bmettere%2Bin%2Bcontatto%2Bi%2Bpazienti%2Bcon%2Bi%2Bprofessionisti%2Bsanitari%2Baccessibili%2Bvia%2Bweb%2Be%2Bapp.pdf/7fc9ca53-f078-af9b-248d-a71dee74da07?version=2.0

[S5] Garante, FAQ registro: https://www.garanteprivacy.it/home/faq/registro-delle-attivita-di-trattamento

[S6] EDPB, linee guida 01/2022 sul diritto di accesso, v. 2.1: https://www.edpb.europa.eu/system/files/2023-04/edpb_guidelines_202201_data_subject_rights_access_v2_en.pdf

[S7] EDPB, sintesi: https://www.edpb.europa.eu/system/files/2025-09/right-of-access-guidelines-summary_en.pdf

[S8] Garante, prescrizioni 5 giugno 2019: https://garanteprivacy.it/web/guest/home/docweb/-/docweb-display/docweb/9124510

[S9] Garante, data breach: https://www.garanteprivacy.it/data-breach

[S10] Garante, elenco trattamenti soggetti a DPIA, 11 ottobre 2018: https://www.garanteprivacy.it/web/guest/home/docweb/-/docweb-display/docweb/9058979

[S11] Codice Privacy, d.lgs. 196/2003, testo vigente da verificare su Normattiva: https://www.normattiva.it/uri-res/N2Ls?urn:nir:stato:decreto.legislativo:2003-06-30;196
