using System;
using static SimpleScheduler.MySimpleScheduler;
using System.Threading.Tasks;

namespace SimpleScheduler {
    class Program {

        //Napomena: u svim pozvanim fukcijama u kojima se simulira rad Rasporedjivaca(izuzev DeadlockSimulation()) koristen je 1 thread
        //radi demonstracije ponasanja rasporedjivaca, po potrebi je moguce koristiti veci broj threadova
        static void Main(string[] args) {
            // NonPreemptiveLowerAndHigherPrioritySimulation();
            // NonPreemptiveMulitpleTasksSimulation();
            // PreemptiveLowerAndHigherPrioritySimulation();
            // BaseClassMethodsSimulation();
            //LockResuourceAndStartTaskWithHigherPrioritySimulation();
            DeadlockSimulation();

        }

        public static void NonPreemptiveLowerAndHigherPrioritySimulation() {
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
            //iako je specifikovan priority 1, bice dodijeljen 2, jer je 1 rezervisan od strane schedulera
            const int HigherPriority = 1;

            //delegat
            TaskFunction function = x => testFunction(x, maxDurationInSeconds);
            //rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
            scheduler.ScheduleTaskNonPreemptive(function, maxDurationInSeconds, LowerPriority);
            Task.Delay(500).Wait();
            scheduler.ScheduleTaskNonPreemptive(function, maxDurationInSeconds, HigherPriority);

            scheduler.Finish();
        }

        public static void PreemptiveLowerAndHigherPrioritySimulation() {
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
            //iako je specifikovan priority 1, bice dodijeljen 2, jer je 1 rezervisan od strane schedulera
            const int HigherPriority = 1;

            //delegat
            TaskFunction function = x => testFunction(x, maxDurationInSeconds);
            //rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
            scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, LowerPriority);
            Task.Delay(500).Wait();
            scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, HigherPriority);

            scheduler.Finish();
        }

        public static void BaseClassMethodsSimulation() {
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

            Task t1 = nonpreemptiveScheduler.RegisterTask(x => testFunction(x), maxDurationInSeconds, priority);
            t1.Start(nonpreemptiveScheduler);
            Task t2 = nonpreemptiveScheduler.RegisterTask(x => testFunction(x), maxDurationInSeconds, priority);
            t2.Start(nonpreemptiveScheduler);
            nonpreemptiveScheduler.Finish();
        }

        /// <summary>
        /// Simulacija porasta prioriteta taska nakon zakljucavanja resrusa. Iako ce task koji je prethodno bio veceg prioriteta polkucati da se pokrene,
        /// task koji je prethodno zakljucao resurs ima najveci prioritet, te ga nec emoci prekinuti.
        /// </summary>
        public static void LockResuourceAndStartTaskWithHigherPrioritySimulation() {

            const int numOfThreads = 1;
            const int oneThousandMilliseconds = 1000;

            Resource resource = new Resource();
            MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, true);

            void testFunction(TaskData task) {
                bool requiredResource = false;
                for (int i = 0; i < task.MaxDuration; i++) {

                    while (task.IsPaused) {
                        Console.WriteLine(task + " pauziran!");
                        task.Handler.WaitOne();
                    }

                    if (!requiredResource) {
                        //ako je povratna vrijednost true, uspjesno je zakljucan resurs
                        if (scheduler.LockResource(resource, task)) {
                            Console.WriteLine(task + " zakljucao: " + resource);
                        }
                        else {//ako je povratna vrijednost false, potrebno je provjeriti sta je prouzrokovalo gresku
                            //preko odgovarajucih tokena
                            if (task.Token.IsCancellationRequested) {
                                Console.WriteLine(task + " CANCELLED!");
                                break;
                            }
                            else if (task.IsDeadlocked) {
                                Console.WriteLine(task + " nije zauzeo : " + resource + "  jer bi izazvao deadlock!");
                                task.IsDeadlocked = false;
                            }
                        }
                        //simulacija rad sa resursom
                        Task.Delay(oneThousandMilliseconds * 2).Wait();
                        //zavrsen rad sa resursom
                        scheduler.UnlockResource(resource, task);

                        requiredResource = !requiredResource;
                    }

                    if (task.Token.IsCancellationRequested) {
                        Console.WriteLine(task + " CANCELLED");
                        break;
                    }

                    Task.Delay(oneThousandMilliseconds).Wait();
                }
            }

            const int maxDurationInSeconds = 5;
            const int LowerPriority = 5;
            const int HigherPriority = 2;

            //delegat
            TaskFunction function = x => testFunction(x);
            //rasporedjivanje taska, sa sepcifikovanjem prioriteta i trajanja
            scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, LowerPriority);

            Task.Delay(500).Wait();
            scheduler.ScheduleTaskPreemptive(function, maxDurationInSeconds, HigherPriority);

            scheduler.Finish();
        }

        public static void DeadlockSimulation() {
            const int numOfThreads = 2;
            const int oneThousandMilliseconds = 1000;

            Resource resource1 = new Resource();
            Resource resource2 = new Resource();
            MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, true);

            //funkcija koja simulira deadlock, u zavisnosti od parametra firstTask odredjuje se da lice prvo biti zatrazen resurs1 ili resurs2
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
            scheduler.ScheduleTaskPreemptive(function1, maxDurationInSeconds, priority);

            Task.Delay(500).Wait();
            scheduler.ScheduleTaskPreemptive(function2, maxDurationInSeconds, priority);

            scheduler.Finish();
        }

        public static void NonPreemptiveMulitpleTasksSimulation() {
            const int numOfThreads = 1;
            const int oneThousandMilliseconds = 1000;
            const int numOfTasks = 10;
            //instanciranje schedulera
            MySimpleScheduler scheduler = new MySimpleScheduler(numOfThreads, false);

            //funkcija zadatka
            void testFunction(TaskData task, int duration) {
                for (int i = 0; i < duration; i++) {

                    if (task.Token.IsCancellationRequested) {
                        Console.WriteLine(task + " OTKAZAN");
                        return;
                    }
                    Console.WriteLine(task + " ITERACIJA: " + i);
                    Task.Delay(oneThousandMilliseconds).Wait();
                }
            }
            TaskFunction[] function = new TaskFunction[numOfTasks];
            for (int i = 0; i < numOfTasks; i++) {
                function[i] = (x) => testFunction(x, i % 7 + 1);
            }
            for (int i = 0; i < numOfTasks; i++) {
                scheduler.ScheduleTaskNonPreemptive(function[i], i % 7 + 1, i % 5 + 1);
                Task.Delay(500).Wait();
            }

            scheduler.Finish();

        }
    }
}
