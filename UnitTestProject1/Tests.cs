using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using static SimpleScheduler.MySimpleScheduler;

namespace Zadatak1.Demo {
    [TestClass]
    public class NaiveTaskScheduler {
        [TestMethod]
        public void ScheduleTask() {
            const int numThreads = 5;
            const int numTasks = 15;
            const int oneSecondDelayInMilliseconds = 1000;
            const int priority = 3;

            SimpleScheduler.MySimpleScheduler scheduler = new SimpleScheduler.MySimpleScheduler(numThreads, false);

            for (int i = 0; i < numTasks; ++i)
                scheduler.ScheduleTaskNonPreemptive(x => Task.Delay(oneSecondDelayInMilliseconds).Wait(), 5 * oneSecondDelayInMilliseconds, priority);

            Assert.AreEqual(numThreads, scheduler.CurrentTaskCount);
        }
        [TestMethod]
        public void Priority_Value_Less_Then_Zero() {
            const int numThreads = 5;
            const int oneSecondDelayInMilliseconds = 1000;
            const int negativePriorityValue = -3;

            SimpleScheduler.MySimpleScheduler scheduler = new SimpleScheduler.MySimpleScheduler(numThreads, false);

            Assert.ThrowsException<ArgumentException>(() => scheduler.ScheduleTaskNonPreemptive(x => Task.Delay(oneSecondDelayInMilliseconds).Wait(), 5 * oneSecondDelayInMilliseconds, negativePriorityValue));
        }

        [TestMethod]
        public void Number_Of_Threads_Less_Then_One() {
            const int numThreads = 0;
           
            Assert.ThrowsException<ArgumentException>(() => new SimpleScheduler.MySimpleScheduler(numThreads, false));
        }
    }
}
