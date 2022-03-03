using System;
using System.Collections.Generic;

namespace SimpleScheduler {

    public static class CyclesDetector {


        /// <summary>
        /// DFS algoritmom se utvrdjuje da li postoji ciklkus u grafu
        /// </summary>
        /// <param name="graph">Graf koji se pretrazuje.</param>
        /// <param name="source">Cvor iz kojeg se pocinje pretraga.</param>
        /// <param name="visited">HashSet posjecenih cvorova.</param>
        /// <param name="recursionStack">HashSet cvorova koji se treutno posjecuju.</param>
        /// <returns>Bool vrijednost koja pokazuje da li je pronadjen ciklus u grafu</returns>
        private static bool CheckForCycle<T>(Graph<T> graph, T source, ref HashSet<T> visited, ref HashSet<T> recursionStack) {
            lock (graph.lockObject) {
                if (!visited.Contains(source)) {
                    //Oznaci trenutni cvor kao posjecen i stavi ga u recursionStack
                    visited.Add(source);
                    recursionStack.Add(source);

                    //Ponovi za sve cvorove koji su susjedni trenutnom
                    foreach (var adjacent in graph.Neighbours(source)) {
                        //ako susjedni cvor nije posjecen, provjeri DFS-om da li susjed pripada nekom ciklusu
                        if (!visited.Contains(adjacent) && (CheckForCycle<T>(graph, adjacent, ref visited, ref recursionStack) == true))
                            return true;

                        //ako je susjed posjecen i postoji u recursionStack-u, onda postoji ciklus!
                        if (recursionStack.Contains(adjacent))
                            return true;
                    }
                }

                //Ukloni pocetni cvor iz recursionStack-a
                recursionStack.Remove(source);
                return false;
            }
        }


        /// <summary>
        /// <returns>Bool vrijednost koja pokazuje da li je pronadjen ciklus u grafu.</returns>
        /// </summary>
        public static bool IsCyclic<T>(Graph<T> Graph) {
            if (Graph == null)
                throw new ArgumentNullException();

            var visited = new HashSet<T>();
            var recursionStack = new HashSet<T>();

            bool res;
            foreach (var vertex in Graph.Vertices)
                if ((res = CheckForCycle<T>(Graph, vertex, ref visited, ref recursionStack)) == true)
                    return res;

            return false;
        }

    }


}
