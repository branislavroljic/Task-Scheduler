- [MySimpleScheduler](#simple-task-scheduler)
    - [Mogućnosti](#Mogućnosti)
     - [Primjeri upotrebe](#Primjeri)
        - [Osnovna upotreba](#Upotreba)
        - [Nepreventivni raspoređivač](#Nepreventivni-raspoređivač)
        - [Preventivni raspoređivač](#Preventivni-raspoređivač)
        - [Pokretanje zadatka korištenjem baznog raspoređivača](#Pokretanje-zadatka-korištenjem-baznog-raspoređivača)
       - [Kreiranje zadatka koji zaključava resurse](#Kreiranje-zadatka-koji-zaključava-resurse)
       - [NPP](#NPP)

    - [My Simple Scheduler](#MySimpleScheduler)
        - [Konstruktor](#Konstruktor)
        - [Metode](#Metode)
            - [ScheduleTaskNonPreemptive](#ScheduleTaskNonPreemptive)
            - [ScheduleTaskPreemptive](#ScheduleTaskPreemptive)
            - [LockResource](#LockResource)
            - [UnlockResource](#UnlockResource)
    - [TaskData klasa](#TaskData-klasa)
    - [Razrješavanje deadlock-a](Razrješavanje-deadlocka)



## Mogućnosti
---

- Preventivno i nepreventivno raspoređivanje
- Real-time rasporedjivanje
- Specifikovanje broja niti na kojima ce se izvršavati zadaci
- Detekcija deadlock-a i njegovo razrješavanje
- Sinhronizacija izmedju zadataka
- Rješenje PIP problema

# Primjeri


### Upotreba
---
```c#
//broj niti na koje se rasporedjuju taskovi
const int numOfThreads = 1;
const int oneThousandMilliseconds = 1000;

//instanciranje schedulera
MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, false);

//funkcija zadatka
 void testFunction(TaskData task, int duration){
    for (int i = 0; i < duration; i++) {
       //do some work here
        if (task.Token.IsCancellationRequested) {
            return;
        }
        Task.Delay(oneThousandMilliseconds).Wait();
    }
}
//trajanje i prioritet
const int maxDurationInSeconds = 5;
const int priority = 3;

//delegat
TaskFunction function = x => testFunction(x, 3);
//rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
scheduler.ScheduleTaskNonPreemptive(function, maxDurationInSeconds, priority);

//moglo je i 
//scheduler.ScheduleTaskNonPreemptive(()=> testFunction(x), maxDurationInSeconds, priority);

//cekanje da se zavrse svi taskovi
scheduler.Finish();
```

Tipičan primjer upotreba raspoređivača. Kreira se novi raspoređivač sa specifikovanjem broja niti i napiše se fukcija koju će task izvršavati specfikovano vrijeme. Za ispravan rad raspoređivača neophodno je proslijediti funckiju koja će korištenjem tokena periodično provjeravati da li je zadatak otkazan ili je pauziran(u slučaju [Preventinog](#Preventivni-raspoređivač) raspoređivanja) iz nekog razloga. Da bi funkciji bio omogućen pristup odgovarajućim tokenima koji se provjeravaju, neophodno je da funkcija prima parametar tipa [TaskData](#TaskData-klasa) koji sadrži odgovarajuće tokene. Prilikom poziva funkcije za raspoređivanje nephodno je proslijediti [TaskFunciton](#TaskFunction) delegat, koji će pozivati datu funkciju.

 Raspoređivač će interno kreirati objekat *TaskData* klase i proslijediti ga odgovarajućoj funkciji.

Pri pozivu metode za raspoređivanje, potrebno je proslijediti i parametre koji predstavljaju trajanje i prioritet zadatka. U slučaju neispravnih parametara(manji ili jednaki 0) biće bačen *ArgumentException*.

**Zadaci sa manjom vrijednošću prioriteta imaju veći prioritet**

Da bi se pokrenuti raspoređivač zaustavio, neophodno je pozvati *Finish()* metodu, kojom će raspoređivač sačekati završetak svih pokrenutih taskova.
***
### Nepreventivni raspoređivač
```c#
const int numOfThreads = 1;
const int oneThousandMilliseconds = 1000;

//instanciranje schedulera
MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, false);

//funkcija zadatka
void testFunction(TaskData task, int duration) {
    for (int i = 0; i < duration; i++) {
        Console.WriteLine(task + " ITERACIJA: " + i);
        if (task.Token.IsCancellationRequested) {
            Console.WriteLine(task + " OTKAZAN");
            return;
        }
        Task.Delay(oneThousandMilliseconds).Wait();
    }
}
//trajanje i prioritet
const int maxDurationInSeconds = 5;
const int LowerPriority = 3;
const int HigherPriority = 1;

//delegat
TaskFunction function = x => testFunction(x, 3);
//rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
scheduler.ScheduleTaskNonPreemptive(function, maxDurationInSeconds, LowerPriority);
Task.Delay(500).Wait();
scheduler.ScheduleTaskNonPreemptive(function, maxDurationInSeconds, HigherPriority);

scheduler.Finish();
```
Ovo je primjer instanciranja nepreventivnog raspoređivača, te korištenja njegovih metoda za nepreventivno rapoređivanje.

Drugi parametar konstruktora objekta klase *MySimpleScheduler* predstavlja bool vrijednost kojom se specefikuje da li je raspoređivač raspoređuje preventivno ili ne.

U slučaju kreiranja  nepreventivnog raspoređivača, a pozivanja metoda za preventivno raspoređivanje, proslijeđeni zadaci **neće biti raspoređeni niti izvršeni**.

Primjer demonstrira kreiranje raspoređivača na jednoj niti i pokretanje zadatka sa nižim prioritetom(dakle većom vrijednošću). Nakon pola sekunde se pokreće drugi zadatak sa većim priorietom, koji će zbog načina rada nepreventivnom raspoređivača čekati na izvršenje zadatka koji se trenutno izvršava iako je isti manjeg prioriteta. Kada se prvi zadatak završi, pokreće se drugi.

U slučaju većeg broja zadataka koji čekaju na izvršenje, biće izvršen onaj koji ima najveći prioritet u redu čekanja.

***
### Preventivni raspoređivač
```c#
const int numOfThreads = 1;
const int oneThousandMilliseconds = 1000;

//instanciranje schedulera
MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, true);

//funkcija zadatka
void testFunction(TaskData task, int duration) {

    for (int i = 0; i < duration; i++) {
        while (task.IsPaused) {
            Console.WriteLine(task + " PAUZIRAN");
            task.Handler.WaitOne();
        }
        Console.WriteLine(task + " ITERACIJA: " + i );
        if (task.Token.IsCancellationRequested) {
            Console.WriteLine(task + " OTKAZAN");
            return;
        }
        Task.Delay(oneThousandMilliseconds).Wait();
    }
}
//trajanje i prioritet
const int maxDurationInSeconds = 5;
const int LowerPriority = 3;
const int HigherPriority = 1;

//delegat
TaskFunction function = x => testFunction(x, 5);
//rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, LowerPriority);
Task.Delay(500).Wait();
scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, HigherPriority);

scheduler.Finish();
```
Ovo je primjer instanciranja preventivnog raspoređivača, te korištenja njegovih metoda za preventivno rapoređivanje.

Sada je parametar konstruktora objekta klase *MySimpleScheduler* postavljen na *true* vrijednost, što označava da želimo instanciranje preventivnog raspoređivača.

U slučaju kreiranja preventivnog raspoređivača, a pozivanja metoda za nepreventivno raspoređivanje, proslijeđeni zadaci **neće biti raspoređeni**.

Primjer demonstrira kreiranje raspoređivača na jednoj niti i pokretanje zadatka sa nižim prioritetom(dakle većom vrijednošću). Nakon pola sekunde se pokreće drugi zadatak sa većim priorietom, koji će zbog načina rada preventivnog raspoređivača prekinuti zadatak koji se izvršava. Nakon prekidanja zadatka nižeg prioriteta, zadatak višeg prioriteta počinje izvršavanje.Kada zadatak većeg prioriteta završi, zadatak manjeg prioriteta nastavlja izvršavanje.

U slučaju većeg broja zadataka koji čekaju na izvršenje, biće izvršen onaj koji ima najveći prioritet u redu čekanja.

***
### Pokretanje zadatka korištenjem baznog raspoređivača

```c#
const int numOfThreads = 1;
const int oneThousandMilliseconds = 1000;

//instanciranje schedulera
MySimpleScheduler nonpreemptiveScheduler = new MySimpleScheduler(numOfThreads, false);
//instanciranje nonpreemptive schedulera
void testFunction(TaskData task) {

    for (int i = 0; i < task.MaxDuration; i++) {
        Console.WriteLine(task + " ITERACIJA: " + i);
        if (task.Token.IsCancellationRequested) {
            Console.WriteLine(task + " OTKAZAN");
            return;
        }
        Task.Delay(oneThousandMilliseconds).Wait();
    }
}
//trajanje i prioritet
const int maxDurationInSeconds = 5;
const int priority = 3;

//Obavezno registrovanje task-a
Task t1 = nonpreemptiveScheduler.RegisterTask(x => testFunction(x), maxDurationInSeconds, priority);
//pokretanje task-a uz specifikovanje schedulera koji ce biti koristen
t1.Start(nonpreemptiveScheduler);

nonpreemptiveScheduler.Finish();
```

Ovo je primjer upotrebe baznog raspoređivača.

Prije poziva *Start()* metode, neophodno je pozvati metodu *RegisterTask()* koja raspoređivaču daje signal da će korisnik koristiti baznu klasu za raspoređivanje.

Pozivanje *Start()* metode bez prethodnog poziva metode *RegisterTask()* će prouzrokovati nedefinisano ponašanje.

*Register()* metoda zahtijeva parametre kao i metoda *ScheduleTaskNonPreemptive()*

***Nije dozvoljeno korištenje ovakvog načina pokretanja zadataka sa preventivnim raspoređivačem, jer raspoređivač nije u mogućnosti da kontorliše korisnički kreirane zadatke***


***

### Kreiranje zadatka koji zaključava resurse

```c#
void testFuncitonForLockingResource(TaskData task) {
        bool requiredResource = false;
        for (int i = 0; i < task.MaxDuration; i++) {

            while (task.IsPaused) {
                Console.WriteLine(task + " ceka");
                task.Handler.WaitOne();
            }

            if (!requiredResource) {
                //usjepjesno zauzimanje resursa
                if (scheduler.LockResource(resource, task)) {
                    Console.WriteLine(task + " zakljucao: " + resource);
                }
                //neuspjesno 
                else {
                    if (task.Token.IsCancellationRequested) {
                        Console.WriteLine($"Task: " + task + " CANCELLED");
                        break;
                    }
                    else if (task.IsDeadlocked) {
                        Console.WriteLine($"Task: " + task + " nije zauzeo : " + resource + "  jer bi izazvao deadlock!");
                        task.IsDeadlocked = false;
                    }
                }
                //simulacija rada sa resursom
                Task.Delay(1000).Wait();

                //otkljucavanje resursa
                scheduler.UnlockResource(resource, task);
                requiredResource = !requiredResource;
            }

            if (task.Token.IsCancellationRequested) {
                Console.WriteLine($"Task: " + task + " CANCELLED");
                break;
            }

        }
    }
```

Prikazan je primjer funkcije koja koristi resurse.

Pozivanje fukcije *LockResource(resource)* rezultuje sljedećim scenarijima i pripadajućim povratnim vrijednostima

* resurs je slobodan, povratna vrijednost je *true*
* resurs je bio zauzet, task čeka na oslobođenje, zatim ga zauzme i povratna vrijednost je *true*
* resurs je bio zauzet, task čeka na oslobođenje resursa, zatim dobije signal za zauzimanje resursa, ali task je otkazan prije nego što je resrus zauzet. Task se otkazuje, a povratna vrijednost je *false.*
* resurs je bio zauzet, task čeka na oslobođenje resursa, zatim dobije signal za zauzimanje resursa, ali task je pauziran  dok je čekao na oslobođenje resursa. Nakon što task ponovo pređe u stanje izvršavanja, on zauzima resurs i povratna vrijednost je *true*
* zauzimanje resursa bi rezultovalo deadlock-om, povratna vrijednost je *false* a dati task ima interno setovan flag na *IsDeadlocked = true*

***korisnik mora kooperativno provjeravati odgovaraju'e flag-ove i povratne vrijednosti***


---
### NPP

Prilikom zauzimanja resursa, zadatku se dodjeljuje najviši prioritet, da isti ne bi mogao biti suspendovan od strane zadatka višeg prioriteta u vrijeme dok drži resurs.

```c#

const int numOfThreads = 1;
const int oneThousandMilliseconds = 1000;
const int fourThousandMilliseconds = 1000;
const int threeThousandMilliseconds = 1000;

//instanciranje schedulera
MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, true);
Resource r1 = new Resource();

//funkcija
void testFunction(TaskData task) {
    scheduler.LockResource(r1, task);
    Task.Delay(fourThousandMilliseconds).Wait();
    scheduler.UnlockResource(r1, task);
    Task.Delay(threeThousandMilliseconds).Wait();
}

const int maxDurationInSeconds = 5;
const int priorityLower = 3;
const int priorityHigher = 2;

//delegat
TaskFunction function = x => testFunction(x);
//rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, priorityLower);
Task.Delay(500).Wait();
scheduler.ScheduleTaskPreemptive((x) => { Task.Delay(4000).Wait();}, maxDurationInSeconds, priorityHigher);
```

U ovom primjeru pokazujemo primjenu NPP, gdje prvi zadatak zaključava resurs *r1*. Iako je njegov prioritet bio 3, nakon (uspješnog) zaključavanja, njegov prioritet je najviši mogući, pa bez obzira što je drugi task na početku većeg prioriteta, neće prekinuti task koji se izvršava, jer je on zauzimanjem resursa povećao svoj prioritet. Nakon što task otpusti resrus, njegov prioritet se vraća na vrijednost koju je imao prije zauzimanje resursa.

*Napomena: najveći prioritet je 1, bilo koji task kreiran od strane korisnika može imati vrijednost priorieta 2, vrijednost 1 dodjeljuje raspoređivač interno. Ako korisnik speceifikuje vrijednost 1 kao priioritet, raspoređivač će sam postaviti vrijednost na 2*



# MySimpleScheduler 

### Konstruktor 
***
### **MySimpleScheduler**
Kreira instancu raspoređivača

***parametri***

*int maxParallelTasks* - Broj niti na kojima ce se izvrsavati taskovi.

*bool preemptive* - Da li se instancira preventivni ili nepreventivni scheduler. Ukoliko je vrijednost *true*, biće instanciran preventivni.

***povratna vrijednost***

Instanca klase MySimpleScheduler

**Izuzeci**

*ArgumentException()* ukoliko je broj specifikovanih niti manji od nule

### Delegati
---
### **TaskFunction**
```c#
 public delegate void TaskFunction(TaskData task);
 ```
 ***parametri***
 
 Objekat klase [TaskData](#TaskData-klasa)

### Metode
---

### **ScheduleTaskNonPreemptive**
Stavljanje taska u red za čekanje na izvršavanje i poziv funkcija za rasporedjivanje. Poziv funkcije se vrši preko instance nepreventivnog raspoređivača. Ukoliko se pozove sa preventivnim raspoređivačem, neće imati efekta.

***parametri***

*TaskFunction fukction* - funkcija koju ćeizvršavati zadatak

*int maxDuration* - Maksimalno trajanje izvršavanja taska. Nakon isteka vremena, interni mehanizam će postaviti *CancellationToken* u stanje *IsCanceled = true*

*int priority* - Prioritet taska. Manja vrijednost je veci prioritet. Negativne vrijednosti nisu dozvoljene

*Izuzeci*

*ArgumentException()* ukoliko je prioritet ili trajanje manje od nule

---
### **ScheduleTaskPreemptive**
Stavljanje taska u red za čekanje na izvršavanje i poziv funkcija za rasporedjivanje. Poziv funkcije se vrši preko instance preventivnog raspoređivača. Ukoliko se pozove sa nepreventivnim raspoređivačem, neće imati efekta.

***parametri***

*TaskFunction fukction* - funkcija koju ćeizvršavati zadatak

*int maxDuration* - Maksimalno trajanje izvršavanja taska. Nakon isteka vremena, interni mehanizam će postaviti *CancellationToken* u stanje *IsCanceled = true*

*int priority* -Prioritet taska. Manja vrijednost je veci prioritet. Negativne vrijednosti nisu dozvoljene

*Izuzeci*

*ArgumentException()* ukoliko je prioritet ili trajanje manje od nule

---
### **LockResource**
Zauzimanje resrusa. Ukoliko je resurs zauzet, task ce cekati na njegovo oslobadjanje.
U slucaju da je task pauziran za vrijeme cekanja na resurs, raspoređivač postavljaa njegov  prioritet  na maximalan i ponovo poziva funkcijije za schedulaovanje koje ce ga ponovo rasporediti na izvrsavanje.

***parametri***

*Resource resource* - objekat koji predstavlja resurs koji se zaključava

*TaskData task* - zadatak koji zaključava resurs

***povratna vrijednost***
* resurs je slobodan, povratna vrijednost je *true*
* resurs je bio zauzet, task čeka na oslobođenje, zatim ga zauzme i povratna vrijednost je *true*
* resurs je bio zauzet, task čeka na oslobođenje, zatim dobije signal za zauzimanje, ali task je otkazan prije nego što je resrus zauzet. Task se otkazuje, a povratna vrijednost je *false.*
* resurs je bio zauzet, task čeka na oslobođenje, zatim dobije signal za zauzimanje, ali task je pauziraj prije nego dok je čekao na oslobođenje resursa. Nakon što task ponovo pređe u stanje izvršavanja, on zauzima resurs i povratna vrijednost je *true*
* zauzimanje resursa bi rezultovalo deadlock-om, povratna vrijednost je *false* a dati task ima interno setovan flag na *IsDeadlocked = true*


---
### **UnlockResource**
 Oslobadjanje resursa. Pri oslobadjanju resrusa, salje se odgovarajući signal taskovima koji čekaju na njegovo oslobađanje. Ukoliko resurs nije bio zaključan, funkcija nema efekta.
 Ukoliko zatadak ne zaključava ni jedan resrus nakon oslobađanja, zadatku se vraća prioritet koji je ima prije zauzimanje resursa.

***parametri***

*Resource resource* - objekat koji predstavlja resurs koji se zaključava

*TaskData task* - zadatak koji zakljuačava resurs



# TaskData klasa

### Pregled
---

MyTask klasa enkapsulira sve bitne informacije koje omogućavaju ispravno funkcionisanje raspoređivača.

### Bitna Polja
---

***MaxDuration***

Maksimalno specifikovano trajanje zadatka.

***Priority***

Prioritet zadatka.

***Real Task***

Objekat klase *Task*.

***ControlTask***

Task namijenjen za kotrolu izvršavanja RealTask-a. Ukoliko se RealTask izvršava duže nego što je specifikovano u *MaxDuration*, isti ce biti otkazan.

***Token***

Token za otkazivanje taska. Instanca klase *CancellationTokenSource*. Ukoliko korisnik želi pravilan rad raspoređivača, nephodno je da iterativno, tokom rada njegovog zadatka u finkciji  provjerava vrijednost ovog tokena. Ukoliko je *Token.IsCancellationRequested* postavljen na vrijednost *true* od korisnika se očekuje da kooperativno zaustavi svoj zadatak.

***IsPaused*** i ***Handler***

*IsPaused* vraca *true* ako je task pauziran(zbog dolaska taska veceg prioriteta). Ukoliko korisnik želi pravilan rad raspoređivača, nephodno je da iterativno, tokom rada njegovog taska provjerava vrijednost ovog flag-a. Ako je vrijednost true, upotrebom *Handlera* koji predstavlja instacu klase *EventWaitHandle* neophodno je staviti dati zadatak na čekanje.

---
### Upotreba *IsPaused*, *Handler*  i *Token* polja

```c#
//o;ekivana korisnička funkcija
 void testFunction2(TaskData task, int duration){
    for (int i = 0; i < duration + 3; i++) {
        while (task.IsPaused) {
            Console.WriteLine($"Task: " + task + " CEKA");
            task.Handler.WaitOne();
        }
        if (task.Token.IsCancellationRequested) {
            return;
        }
    }
```

**IsDeadlocked**

Pokazuje da li trenutni zadatak izaziva *deadlock* zahtijevanjem određenih resursa. Ukoliko je vrijednost postavljena na *true*, korisnik može zaključiti da njegov zadatak nije zauzeo traženi resurs jer bi potencijalno mogao izazvati *deadlock*.

**Od korisnika se očekuje da ukoliko ručno podesi vrijednost *IsDeadlocked* na *false* svaki put kada dobije informaciju da je došlo do *deadlocka* i da je isti razriješen.**

---

**IsUserMade**

 provjera da li je task pokrenut koristenjem ugradjenog Start() metoda. Ovaj flag je korišten interno od strane raspoređivača. U zavisnosti od vrijednosti polja, poyivaju se odgovarajućemetode za startanje zadatka

```c#

 if (scheduledTask.IsUserMade) {
    base.TryExecuteTask(scheduledTask.RealTask);
}
else {
    scheduledTask.RealTask.Start();
}
```

# Razrješavanje deadlocka

Prilikom instanciranja raspoređivača, interno se kreira objekat klase *Graph* koji predstavlja usmjereni netežinski graf.

*LockResource* metoda interno koristi ovaj graf. Svaki put kada korisnik pozove *LockResource* metodu, dodaju se čvorovi koji predstavljaju resrus i zadatak koji želi zaključati dati resrus(ukoliko ti čvorovi već ne postoje).

Nakon toga, pokušava se dodavanje grana u grafu. Ukoliko grana ide od zadatka prema resursu, to predstavlja traženje resursa od strane taska, dok grana od resursa prema zadatku predstavlja situaciju u kojoj  dati zadatak zauzima resurs.

Ukoliko zadatak ne može dobiti resrus, dodaje se gorepomenuta grana od zadataka prema rasursu. 

U slučaju da dodavanje grane prouzrokuje ciklus u grafu(a samim tim i deadlock), grana neće biti dodata, a zadatku koji je pokušao zatražiti resurs će flag *IsDeadlocked* biti postavljen na true. Automatski se vrši povratak iz *LockResource* metode uz povratnu vrijednost koja označava neuspjeh pri zauzimanju resursa(*false*). Korisnik na osnovu povratne vrijednosti i postavljenog stanja, može lako zaključiti da je došlo do deadlocka, te da resurs nije zauzet.

Ovakvim načinom rada, ujedno smo izvršili prevenciju, ali i detekciju deadlocka, jer prvo simuliramo deadlock, pa nakon njegove identifikacije vršimo razrjšavanje istog(moglo bi se reći i prevenciju istog).


```c#
 const int numOfThreads = 2;
    const int oneThousandMilliseconds = 1000;
    //R1
    Resource resource1 = new Resource();
    //R2
    Resource resource2 = new Resource();
    MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, true);

    //funkcija koja simulira deadlock, u zavisnosti od parametra firstTask odredjuje se da li ce prvo biti zatrazen resurs1 ili resurs2
    void testFunction(TaskData task, bool firstTask) {
        Resource first, second;
        if (firstTask) {
            first = resource1;
            second = resource2;
        }
        else {
            first = resource2;
            second = resource1;
        }
        //ako je povratna vrijednost true, uspjesno je zakljucan resurs
        if (scheduler.LockResource(first, task)) {
            Console.WriteLine(task + " zakljucao: " + first);
        }
        //provjerava se samo za deadlock, jer je napravljeno tako da nece bit otakayan task
        else if (task.IsDeadlocked) {
            Console.WriteLine($"Task: " + task + " nije zauzeo : " + first + "  jer bi izazvao deadlock!");
            //korisnicka funkcija nakon obrade informacije o potencijalnom izazivanju deadlock-a od strane njegovog zadatka mora postaviti vrijednost IsDeadlocked na false
            task.IsDeadlocked = false;

        }
        //simulacija rada sa resursom
        Task.Delay(oneThousandMilliseconds * task.ID).Wait();

        if (scheduler.LockResource(second, task)) {
            Console.WriteLine(task + " zakljucao : " + second);
        }
        else if (task.IsDeadlocked) {
            Console.WriteLine(task + " nije zauzeo : " + second + "  jer bi izazvao deadlock!");
            task.IsDeadlocked = false;
        }

        //simulacija rada sa resursom
        Task.Delay(oneThousandMilliseconds * task.ID).Wait();
        //zavrsen rad sa svim resursima
        scheduler.UnlockResource(second, task);
        scheduler.UnlockResource(first, task);
    }

    const int maxDurationInSeconds = 10;
    const int priority = 4;
    //delegat
    TaskFunction function1 = x => testFunction(x, true);
    TaskFunction function2 = x => testFunction(x, false);
    //rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
    //T1
    scheduler.ScheduleTaskPreemptive(function1, maxDurationInSeconds, priority);

    Task.Delay(500).Wait();
    //T2
    scheduler.ScheduleTaskPreemptive(function2, maxDurationInSeconds, priority);

    scheduler.Finish();
}
```

Primjer ilustruje simuliaciju deadlock-a. Pokrećemo dva zadatka u odgovarajućim vremenskim intervalima te naizmjenično tražimo resurse. 

T1 će zatražiti R1 i zauzeti ga. T2 će zatražiti R2 i zauzeti ga.
T1 će zatražiti R2, ali isti je već zauzet, pa će T1 čekati unutar *LockResource* metode dok resurs ne bude slobodan.
T2 će zatražiti R1, ali isti je već zauzet, pa bi T2 trebao čekati unutar *LockResource* metode dok resurs ne bude slobodan. Ali kako će ovaj korak prouzrokovati deadlock, Raspoređivač će odbiti zahtjev za zauzimanje resursa R1 od strane T2 i setovati *T2.IsDeadlocked = true* čime će ukazati korisniku zašto nije ostvareno zauzimanje resursa od strane zadatka T2.