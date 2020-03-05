using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace Controllers
{
    class Krawedz : IComparable <Krawedz>
    {  
        //identyfikator krawedzi
        private int id;
        Wezel poczatek;
        Wezel koniec;

        // waga
        public int waga;

        public SortedSet<int> szczeliny;

        public Krawedz(int id,Wezel begin, Wezel end, int weight)
        {
            this.poczatek = begin;
            this.koniec = end;
            this.id = id;
            szczeliny = new SortedSet<int>();
            waga = weight;
        }

        public int PodajPoczatek()
        {
            return poczatek.PodajId();
        }

        public int PodajKoniec()
        {
            return koniec.PodajId();
        }

        public int PodajWage()
        {
            return waga;
        }

        public SortedSet<int> podajSzeliny()
        {
            return szczeliny;
        }

        public void WypiszKrawedz()
        {
         //   Console.WriteLine(id + " " + poczatek.PodajId() + " " + koniec.PodajId());
        }

        public int  Podajindeks()
        {
            return id;
        }

        public int CompareTo(Krawedz druga)
        {
            if (this.waga > druga.waga)
                return 1;
            else if (this.waga < druga.waga)
                return -1;
            else return 0;

        }

    }
}
