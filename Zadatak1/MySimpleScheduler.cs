using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleScheduler {

    public class MySimpleScheduler : TaskScheduler {
        private readonly static int maxPriority = 1;
        private readonly static int maxPriorityForUser = 2;
        public readonly static int defaultDuration = 5;
        public readonly static int defaultPriority = 10;


        //za registrovanje korisnickih taskova koji ce biti pokrenuti ugradjenom metodom za pokretanje taska (Start())
        readonly Dictionary<Task, TaskData> register = new Dictionary<Task, TaskData>();

        //graf za detekciju deadlocka
        private Graph<object> graph;

        //taskovi na cekanju
        readonly LinkedList<TaskData> pendingTasks = new LinkedList<TaskData>();
        //taskovi koji se izvrsavaju
        readonly TaskData[] executingTasks;

        /// <summary>
        /// podrazumijevana funkcija koju korisnik prosljedjuje Scheduleru
        /// </summary>
        /// <param name="task">Objekat klase TaskData koji enkapsulira informacije o tasku koji se trenutno izvrsava</param>
        public delegate void TaskFunction(TaskData task);

        //broj specifikovanih niti
        public int MaxParallelTasks => executingTasks.Length;

        //treniutni broj taskova
        public int CurrentTaskCount => executingTasks.Count(x => x != null);

        /// <summary>
        /// flag koji govori da li je raposredjivanje preventivno ili nerepventivno
        /// </summary>
        public bool Preemptive { get; set; }

        /// <summary>
        /// Registrovanje taska. Ukoliko korisnik zeli pokrenuti Task preko ugradjene Start() metode, potrebno je da prethodno pozove ovu metodu
        /// kako bi registrovao task. U slucaju da metoda nije pozvana, ponasanje nece biti ocekivano.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="priority"></param>
        /// <param name="maxDuration"></param>
        /// <returns></returns>
        public Task RegisterTask(TaskFunction function, int maxDuration, int priority) {
            TaskData task = new TaskData(function, maxDuration, priority, true);
            register.Add(task.RealTask, task);
            return task.RealTask;
        }

        /// <summary>
        /// Klasa koja enkapsulira sam Task i informacije relevantne za njegovo rasporedjivanje
        /// </summary>
        public class TaskData {

            private readonly object lockObject = new object();

            private static int id = 0;

            private int priority;

            /// <summary>
            /// Konstruktor kojim se definisu ogranicenja i fukcija koja se izvrsava
            /// </summary>
            /// <param name="function"> Funkcija koju ce task izvrsavati</param>
            /// <param name="maxDuration">Maksimalno trajanje taska, nakon isteka vremena, task ce biti cancel-ovan.</param>
            /// <param name="priority">Prioritet taska. Manja vrijednost je veci prioritet. Negativne vrijednosti nisu dozvoljene.</param>
            /// <param name="userMade">Ukoliko je task pokrenut koristenjem ugradjenog Start() metoda, vrijednost je true, inace false.</param>
            public TaskData(TaskFunction function, int maxDuration, int priority, bool userMade = false) {
                if (maxDuration <= 0 || priority <= 0)
                    throw new ArgumentException();

                MaxDuration = maxDuration;
                //korisnik moze dodijeliti tasku prioritet minimalne vrijednosti 2
                //vrijednost 1 je rezervisana interno od strane schedulera(zbog realizacije NPP)
                RealPriority = Priority = priority == maxPriority ? maxPriorityForUser : priority;
                Token = new CancellationTokenSource();
                RealTask = new Task(() => function(this), Token.Token);
                IsPaused = false;
                Handler = new EventWaitHandle(false, EventResetMode.AutoReset);
                TaskStopwatch = new Stopwatch();
                ID = ++id;
                IsUserMade = userMade;
            }
            public int MaxDuration { get; private set; }
            /// <summary>
            /// Trenutni prioritet taska.Manja vrijednost je veci prioritet
            /// </summary>
            public int Priority {
                get {
                    lock (lockObject) {
                        return priority;
                    }
                }
                set {
                    lock (lockObject) {
                        priority = value;
                    }
                }
            }
            /// <summary>
            /// Prioritet koji je dodijeljen tasku prilikom instanciranja.
            /// </summary>
            public int RealPriority { get; private set; }
            /// <summary>
            /// Objekat klase Task.
            /// </summary>
            public Task RealTask { get; set; }
            /// <summary>
            /// Task namijenjen za kotrolu izvrsavanja. Ukoliko se RealTask izvrsava duze nego je dozvoljeno, isti ce biti prekinut;
            /// </summary>
            public Task ControlTask { get; set; }
            /// <summary>
            /// Token za otkazivanje taska.
            /// </summary>
            public CancellationTokenSource Token { get; private set; }
            /// <summary>
            /// Vraca true ako je task pauziran(zbog dolaska taska veceg prioriteta).
            /// </summary>
            public bool IsPaused { get; set; }
            /// <summary>
            /// Mjerenje vrementa izvrsavanja taska.
            /// </summary>
            public Stopwatch TaskStopwatch { get; }
            public EventWaitHandle Handler { get; private set; }
            /* public resourceProcessing FSaveData { get; private set; }
             public resourceProcessing FWriteData { get; private set; }*/
            public int ID { get; private set; }

            /// <summary>
            /// Flag koji pokazuje da li trenutni task izaziva deadlock
            /// </summary>
            public bool IsDeadlocked { get; set; }

            /// <returns>
            /// provjera da li je task pokrenut koristenjem ugradjenog Start() metoda.
            /// </returns>
            public bool IsUserMade { get; private set; }
            public override string ToString() {
                return "Task: " + ID + "  trajanje: " + MaxDuration + " prioritet: " + Priority;
            }
        }

        /// <summary>
        /// Klasa koja reprezentuje resurs koji ce korisnicka funkcija zauzeti u svom izvrsavanju.
        /// </summary>
        public class Resource {
            private static int id = 0;

            public Resource() {
                Handler = new EventWaitHandle(false, EventResetMode.AutoReset);
                ID = ++id;
            }
            //upotreba kod signalizacije pri zakljucavanju resursa
            public EventWaitHandle Handler { get; set; }
            public int ID { get; private set; }
            public override string ToString() {
                return " Resurs: " + ID;
            }
        }


        /// <summary>
        /// Konstruktor.
        /// </summary>
        /// <param name="maxParallelTasks">Broj niti na kojima ce se izvrsavati taskovi.</param>
        /// /// <param name="preemptive">Da li se instancira preventivni ili nepreventivni scheduler.</param>
        public MySimpleScheduler(int maxParallelTasks, bool preemptive) {
            if (maxParallelTasks < 1)
                throw new ArgumentException();

            executingTasks = new TaskData[maxParallelTasks];
            Preemptive = preemptive;
            //kreiranje grafa za detekciju ciklusa
            graph = new Graph<object>();

        }


        /// <summary>
        /// metoda bazne klase
        /// </summary>
        /// <returns>sve taskove koji su proslijedjeni scheduleru</returns>
        protected override IEnumerable<Task> GetScheduledTasks() => pendingTasks.Select(t => t.RealTask).ToArray().Union(executingTasks.Select(t => t.RealTask));


        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            return false;
        }

        /// <summary>
        /// Zauzimanje resrusa. Ukoliko je resurs zauzet, task ce cekati na njegovo oslobadjanje.
        /// U slucaju da je task pauziran za vrijeme cekanja na resurs, on svoj prioritet postavlja na maximalan i ponovo poziva funkcijije za schedulaovanje
        /// koje ce ga ponovo rasporediti na izvrsavanje
        /// </summary>
        /// <param name="resource">Resurs koji se zauzima.</param>
        /// <param name="task">Task koji zeli zauzeti resurs.</param>
        public bool LockResource(Resource resource, TaskData task) {

            lock (resource) {
                graph.AddVertex(resource);
                graph.AddVertex(task);

                if (graph.Neighbours(resource).Count() == 0) { //ako nijedan task ne koristi resurs
                    task.Priority = maxPriority;
                    graph.AddEdge(resource, task);

                    return true;
                }
                else {
                    //dodaj granu koja predstaljva trazenje resrusa
                    graph.AddEdge(task, resource);
                    //ako ima ciklus, brisi granu
                    if (CyclesDetector.IsCyclic<object>(graph) == true) {

                        task.IsDeadlocked = true;
                        graph.RemoveEdge(task, resource);
                        return false;
                    }
                    Console.WriteLine($"{task} ceka na: {resource}");
                }
            }

            resource.Handler.WaitOne();

            lock (resource) {
                //obrisi granu kojom task trayi resrus
                graph.RemoveEdge(task, resource);


                if (task.Token.IsCancellationRequested) {
                    return false;
                }

                //grana kojom task zauzima resurs
                graph.AddEdge(resource, task);
                task.Priority = maxPriority;

                if (task.IsPaused) {
                    Console.WriteLine($"{task} je pauziran dok je cekao na resurs");
                    SuspendExecutingTask(task); //postavi trenutni task na thread
                    SchedulePendingTasksPreemptive();  //jer se nikad nece desiti za nonpreempitve

                    task.IsPaused = false;
                }
                return true;

            }
        }

        /// <summary>
        ///  Oslobadjanje resursa. Pri oslobadjanju resrusa, salje se odgovarajuci signal taskovima koji cekaju na njegovo oslobadjanje.
        /// </summary>
        /// <param name="resource">Resurs koji se oslobadja.</param>
        /// <param name="task">Task koji oslobadja resurs.</param>
        public void UnlockResource(Resource resource, TaskData task) {
            if (!graph.RemoveEdge(resource, task))
                return;
            //ako task ima jos resursa, nece mu se mijenjati prioritet(ostaje 1)
            if (graph.ParentVertices(task).Count() == 0)
                task.Priority = task.RealPriority;

            resource.Handler.Set();
            Console.WriteLine(task + " je otkljucao : " + resource);
        }

        protected override void QueueTask(Task task) {
            if (!register.ContainsKey(task))
                return;

            pendingTasks.AddLast(register[task]);
            SchedulePendingTasksNonPreemptive();
        }

        /// <summary>
        /// Stavljanje taska u red za izvrsavanje i poziv funkcija za rasporedjivanje
        /// </summary>
        /// <param name="function">Funkcija taska.</param>
        /// <param name="maxDuration">Maksimalno trajanje taska, nakon isteka vremena, task ce biti cancel-ovan.</param>
        /// <param name="priority">Prioritet taska. Manja vrijednost je veci prioritet. Negativne vrijednosti nisu dozvoljene.</param>
        public void ScheduleTaskNonPreemptive(TaskFunction function, int maxDuration, int priority) {
            if (Preemptive)
                return;
            pendingTasks.AddLast(new TaskData(function, maxDuration, priority));
            SchedulePendingTasksNonPreemptive();
        }

        //upotreba kod sinhronizacije, jer u istom trenutku moze vise taskova pozvati metodu za rasporedjivanje
        private readonly object lockObject = new object();

        public void SchedulePendingTasksNonPreemptive() {
            if (Preemptive)
                return;
            lock (lockObject) {
                AbortTasksOverQuota();
                ScheduleTasksOnAvailableThreadsNonPreemptive();
            }
        }
        /// <summary>
        /// Nepreventivo rasporedjivanje taskova na slobodne niti. Taskovi se pokrecu po prioritetu. Pokrece se i odgovarajuci kontrolni task.
        /// U zavisnoti od toga da li je task pokrenut pomocu Start() metode ili metode ovog schedulera, pozivaju se odgovarajuce metode
        /// za njihovo izvrsavanje
        /// </summary>
        private void ScheduleTasksOnAvailableThreadsNonPreemptive() {
            if (Preemptive)
                return;
            //nadji slobodne niti
            int[] availableThreads = executingTasks.Select((value, i) => (value, i)).Where(x => x.value == null).Select(x => x.i).ToArray();
            foreach (int freeThread in availableThreads) {

                if (pendingTasks.Count == 0)
                    return;
                //nadji prvi task po prioritetu iz reda taskova koji cekaju na izvrsavanje
                TaskData scheduledTask = pendingTasks.OrderBy(t => t.Priority).First();

                pendingTasks.Remove(scheduledTask);

                //pokretanje stoperice
                scheduledTask.TaskStopwatch.Start();
                //stavljanje taska na nit
                executingTasks[freeThread] = scheduledTask;

                StartControlTask(scheduledTask);

                if (scheduledTask.IsUserMade) {
                    base.TryExecuteTask(scheduledTask.RealTask);
                }
                else {
                    scheduledTask.RealTask.Start();
                }

            }

        }
        
        /// <summary>
        /// Pokretanje kontrolog taska koji kontrolise izvrsavanje taska proslijedjenog kao parametar, i po isteku definisanog vremena ga otkazuje.
        /// </summary>
        /// <param name="scheduledTask">Task koji ce biti kontrolisan.</param>
        /// <param name="preemptive">Da li je scheduler u preventivnom ili nepreventivnom modu.</param>
        private void StartControlTask(TaskData scheduledTask) {
            scheduledTask.ControlTask = Task.Factory.StartNew(() => {

                long preostalo;
                while ((preostalo = scheduledTask.MaxDuration * 1000 - scheduledTask.TaskStopwatch.ElapsedMilliseconds) > 0)
                    Task.Delay((int)preostalo).Wait();


                //Console.WriteLine(scheduledTask + " zahtjev za otkazivanjeeeee: ----> " + preostalo);
                scheduledTask.Token.Cancel();
                scheduledTask.RealTask.Wait();
                if (Preemptive)
                    SchedulePendingTasksPreemptive();
                else
                    SchedulePendingTasksNonPreemptive();
            });
        }

        /// <summary>
        /// Stavljanje taska u red za izvrsavanje. Preventivno.
        /// </summary>
        /// <param name="function">Funkcija taska.</param>
        /// <param name="maxDuration">Maksimalno trajanje taska, nakon isteka vremena, task ce biti cancel-ovan.</param>
        /// <param name="priority">Prioritet taska. Manja vrijednost je veci prioritet. Negativne vrijednosti nisu dozvoljene.</param>
        public void ScheduleTaskPreemptive(TaskFunction function, int maxDuration, int priority) {
            if (!Preemptive)
                return;

            TaskData scheduledTask = new TaskData(function, maxDuration, priority);

            SuspendExecutingTask(scheduledTask);

            pendingTasks.AddLast(scheduledTask);

            SchedulePendingTasksPreemptive();
        }
        /// <summary>
        /// Pronalazak slobodne niti, ili taska koji ima manji prioritet od onog proslijedjenog u metodi.
        /// U slucaju pronalaska taska, isti se skida sa niti i pauzira.
        /// </summary>
        /// <param name="scheduledTask"></param>
        private void SuspendExecutingTask(TaskData scheduledTask) {
            try {
                int threadID = FindBestThread(scheduledTask.Priority);

                TaskData executingTask = executingTasks[threadID];

                //zaustyavljanje pronadjenog taska manjeg prioriteta
                if (executingTask != null) {
                    Console.WriteLine(scheduledTask + " ce prekinuti : " + executingTask);
                    executingTask.IsPaused = true;
                    executingTask.TaskStopwatch.Stop(); //zaustavi stopericu
                    pendingTasks.AddLast(executingTask);
                    executingTasks[threadID] = null;
                }
            }
            catch (IndexOutOfRangeException) {
                return;
            }
        }
        /// <summary>
        /// Pronalazak najbolje niti na kojoj ce se izvrsavati task.
        /// Upotreba kod preemptive rasporedjivanja. Ukoliko nema slobodnih niti, najbolja nit je ona na kojoj je task sa najvecim prioritetom.
        /// </summary>
        /// <param name="priority">prioritet taska koji se rasporedjuje</param>
        /// <returns>id niti koja je optimalna</returns>
        /// <exception cref="System.IndexOutOfRangeException"> Ukoliko nema slobodnih niti, a svi taskovi su veceg prioriteta od proslijedjenog prioriteta.</exception>
        private int FindBestThread(int priority) {

            int[] slobodneNiti = executingTasks.Select((value, i) => (value, i)).Where(t => t.value == null).Select(t => t.i).ToArray();
            if (slobodneNiti.Length > 0)
                return slobodneNiti[0];
            else {

                (TaskData task, int i) res = executingTasks.Select((value, i) => (value, i)).OrderByDescending(t => t.value.Priority).First();

                if (res.task.Priority > priority)
                    return res.i;
            }
            throw new IndexOutOfRangeException();
        }

        private void SchedulePendingTasksPreemptive() {
            if (!Preemptive)
                return;
            lock (lockObject) {
                AbortTasksOverQuota();
                ScheduleTasksOnAvailableThreadsPreemptive();
            }
        }

        /// <summary>
        /// Ispitivanje da li ima taskova koji su otkazani ili su zavrsili sa radom. Isti se uklanjaju is niza executingTasks i zaustavlja se mjerenje njihovog vremena.
        /// </summary>
        private void AbortTasksOverQuota() {
            for (int i = 0; i < MaxParallelTasks; ++i) {
                if (executingTasks[i] != null) {
                    TaskData taskZaTestiranje = executingTasks[i];
                    if (taskZaTestiranje.Token.IsCancellationRequested || taskZaTestiranje.RealTask.IsCanceled || taskZaTestiranje.RealTask.IsCompleted) {
                        executingTasks[i] = null;
                        taskZaTestiranje.TaskStopwatch.Stop();
                    }
                }
            }
        }
        /// <summary>
        /// Preventivno rasporedjivanje taskova na slobodne niti. Taskovi se pokrecu po prioritetu.
        /// Ukliko je task koji je izabran prethodno zaustavljen, isti nastavlja sa radom.
        /// Ukliko je task koji je izabran pokrenut prvi put, pokrece se i odgovarajuci kontrolni task.
        /// </summary>
        private void ScheduleTasksOnAvailableThreadsPreemptive() {
            if (!Preemptive)
                return;
            //nadji slobodne niti
            int[] availableThreads = executingTasks.Select((value, i) => (value, i)).Where(x => x.value == null).Select(x => x.i).ToArray();
            foreach (int freeThread in availableThreads) {

                if (pendingTasks.Count == 0) 
                    return;

                //nadji prvi task po prioritetu iz reda taskova koji cekaju na izvrsavanje
                TaskData scheduledTask = pendingTasks.OrderBy(t => t.Priority).First();
                pendingTasks.Remove(scheduledTask);

                //ukoliko je task prethodno zaustavljen
                if (scheduledTask.IsPaused) {
                    scheduledTask.IsPaused = false;
                    executingTasks[freeThread] = scheduledTask;
                    scheduledTask.Handler.Set();
                    scheduledTask.TaskStopwatch.Start();
                }
                //inace ga pokrecemo prvi put
                else {

                    scheduledTask.RealTask.Start();
                    scheduledTask.TaskStopwatch.Start();
                    executingTasks[freeThread] = scheduledTask;
                    StartControlTask(scheduledTask);
                }

            }

        }

        public void Finish() {
            const int oneSecondDelayInMilliseconds = 1000;
            while (CurrentTaskCount > 0)
                Task.Delay(oneSecondDelayInMilliseconds).Wait();
        }

        

    }


}
