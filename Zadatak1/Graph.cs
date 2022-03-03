using System;
using System.Collections.Generic;
using System.Threading;


namespace SimpleScheduler {

    /// <summary>
    /// Reprezetnacija usmjerenog netezinskog grafa. Graf je thread-safe.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Graph<T> {
        
        public object lockObject = new object();
        protected virtual Dictionary<T, LinkedList<T>> AdjacencyList { get; set; }
        
        /// <summary>
        /// Konstrukotor.
        /// </summary>
        /// <param name="handler"> Handler koji omogucuje komunikacjiu izmedju grafa i Taska koji provjerava postojanje ciklusa u grafu.</param>
        public Graph() {
            AdjacencyList = new Dictionary<T, LinkedList<T>>();
        }

        /// <summary>
        /// Dobijanje svih cvorova u grafu
        /// </summary>
        public virtual IEnumerable<T> Vertices {

            get {
                lock (lockObject) {
                    foreach (var vertex in AdjacencyList)
                        yield return vertex.Key;
                }
            }
        }

        /// <summary>
        /// Provjera postojanja grane u grafu.
        /// </summary>
        /// <returns>True ako grana postoji.</returns>
        protected virtual bool CheckForEdge(T vertex1, T vertex2) {
            return (AdjacencyList[vertex1].Contains(vertex2));
        }

        /// <summary>
        /// Kreira granu izmedju source u desitnation.
        /// </summary>
        public virtual bool AddEdge(T source, T destination) {

           
            lock (lockObject) {
                //provjeri postojanje grane i cvorova
                if (!HasVertex(source) || !HasVertex(destination))
                    return false;
                if (CheckForEdge(source, destination))
                    return false;

                // dodaj granu
                AdjacencyList[source].AddLast(destination);

                
                return true;
            }
        }

        /// <summary>
        /// Vraca roditeljske cvorove. 
        /// </summary>
        /// <param name="vertex"> Cvor ciji se roditelji traze.</param>
        /// <returns>Listu cvorova koji su roditlji datom cvoru.</returns>
        public virtual List<T> ParentVertices(T vertex) {
            if (!HasVertex(vertex))
                throw new KeyNotFoundException("Nema takvog cvora!");
            List<T> list = new List<T>();
            foreach (var adjacent in AdjacencyList.Keys) {
                if (AdjacencyList[adjacent].Contains(vertex))
                    list.Add(adjacent);

            }
            return list;
        }

        /// <summary>
        /// Uklanja granu ukoliko postoji.
        /// </summary>
        public virtual bool RemoveEdge(T source, T destination) {
            lock (lockObject) {
               
                if (!HasVertex(source) || !HasVertex(destination))
                    return false;
                if (!CheckForEdge(source, destination))
                    return false;

                AdjacencyList[source].Remove(destination);
                
                
                return true;
            }
        }


        /// <summary>
        /// Dodaj cvor u graf.
        /// </summary>
        public virtual bool AddVertex(T vertex) {
            lock (lockObject) {
                if (HasVertex(vertex))
                    return false;


                AdjacencyList.Add(vertex, new LinkedList<T>());

                return true;
            }
        }

        /// <summary>
        /// Ukljanja cvor iz grafa.
        /// </summary>
        public virtual bool RemoveVertex(T vertex) {
            lock (lockObject) {
               
                if (!HasVertex(vertex))
                    return false;

             
                AdjacencyList.Remove(vertex);

                
                foreach (var adjacent in AdjacencyList) {
                    if (adjacent.Value.Contains(vertex)) {
                        adjacent.Value.Remove(vertex);
                    }
                }

                return true;
            }
        }

        /// <summary>
        ///  Provjera da li postoji grana od source ka destination.
        /// </summary>
        public virtual bool HasEdge(T source, T destination) {
            lock (lockObject) {
                return (AdjacencyList.ContainsKey(source) && AdjacencyList.ContainsKey(destination) && CheckForEdge(source, destination));
            }
        }

        /// <summary>
        /// Provjera da li postoji cvor u grafu.
        /// </summary>
        public virtual bool HasVertex(T vertex) {
            lock (lockObject) {
                return AdjacencyList.ContainsKey(vertex);
            }
        }

        /// <summary>
        /// Vraca susjede cvora koji je proslijedjen kao parametar.
        /// </summary>
        public virtual LinkedList<T> Neighbours(T vertex) {
            lock (lockObject) {
                if (!HasVertex(vertex))
                    return null;

                return AdjacencyList[vertex];
            }
        }


    }

}

