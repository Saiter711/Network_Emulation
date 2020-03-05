using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Controllers
{
    class Graf
    {
        public List<Wezel> wezly = new List<Wezel>();
        public List<Krawedz> krawedzie = new List<Krawedz>();

        public int ilosckrawedzi;
        public int iloscwezlow;

        public Graf(String fileName)
        {
            int rowDataQuantity = 4;
            string[] wczytywanie = System.IO.File.ReadAllLines(fileName);
            iloscwezlow = Int32.Parse(wczytywanie[0]);
            int[] numerki = new int[rowDataQuantity];
            ilosckrawedzi = Int32.Parse(wczytywanie[iloscwezlow + 1]);
            for (int i = 1; i <= iloscwezlow; i++)
            {

                string[] dane = wczytywanie[i].Split(new string[] { " " }, StringSplitOptions.None);
                for (int j = 0; j < dane.Length; j++)
                {
                    numerki[j] = Int32.Parse(dane[j]);
                }

                wezly.Add(new Wezel(numerki[0]));
            }

            for (int i = iloscwezlow + 2; i <= iloscwezlow + ilosckrawedzi + 1; i++)
            {
                string[] dane = wczytywanie[i].Split(new string[] { " " }, StringSplitOptions.None);
                for (int j = 0; j < dane.Length; j++)
                {
                    numerki[j] = Int32.Parse(dane[j]);
                }

                krawedzie.Add(new Krawedz(numerki[0], wezly[numerki[1] - 1], wezly[numerki[2] - 1], numerki[3]));
                krawedzie.Add(new Krawedz(numerki[0] + ilosckrawedzi, wezly[numerki[2] - 1], wezly[numerki[1] - 1], numerki[3]));
            }
            ilosckrawedzi *= 2;
        }

        public Graf(int o, int p)
        {
            ilosckrawedzi = o;
            iloscwezlow = p;
        }

        public void readNewTopologyFromLRM(List<LRMRow> values)
        {
            foreach (LRMRow row in values)
            {
                foreach (Krawedz krawedz in krawedzie)
                {
                    if (row.routerID1 == krawedz.PodajPoczatek() && row.routerID2 == krawedz.PodajKoniec())
                    {
                        krawedz.szczeliny.Clear();
                        foreach (int x in row.frequencySlots)
                            krawedz.szczeliny.Add(x);

                    }
                }
            }
        }

        public int podajKrawedz(int a, int b)
        {
            for (int j = 0; j < krawedzie.Count; j++)
            {
                if (a == krawedzie[j].PodajPoczatek() && b == krawedzie[j].PodajKoniec())
                {
                    return krawedzie[j].Podajindeks();
                }
            }
            return -1;
        }

        public Krawedz podajKrawedz1(int a, int b)
        {
            foreach (Krawedz kraw in krawedzie)
            {
                if (kraw.PodajPoczatek() == a && kraw.PodajKoniec() == b)
                    return kraw;

            }
            return null;
        }

        public Wezel podajWezel(int number)
        {
            foreach (Wezel wezel in wezly)
            {
                if (wezel.PodajId() == number)
                    return wezel;
            }
            return null;
        }

        public List<int> dijkstra(int a, int x)
        {
            Graf grafdijkstry = new Graf(0, 0);
            List<Krawedz> kolejka = new List<Krawedz>();
            List<int> droga = new List<int>();
            List<int> p = new List<int>();
            List<int> d = new List<int>();
            List<SortedSet<int>> holes = new List<SortedSet<int>>();

            if (a != x)
            {
                for (int i = 0; i < wezly.Count; i++)
                {
                    p.Add(-1);
                    d.Add(777777);
                    holes.Add(new SortedSet<int> { 0 });
                }
                d[a - 1] = 0;
                holes[a - 1] = new SortedSet<int>();
                for (int i = 0; i < Controllers.MAXSLOTS; i++)
                    holes[a - 1].Add(i);

                for (int i = 0; i < krawedzie.Count; i++)
                {
                    if (krawedzie[i].PodajPoczatek() == a)
                    {
                        kolejka.Add(krawedzie[i]);
                    }
                }
                foreach (var krawedz in kolejka)
                {
                    droga.Add(krawedz.PodajWage());
                }

                grafdijkstry.wezly.Add(wezly[a - 1]);
                grafdijkstry.iloscwezlow++;

                int min = 12345677;
                int kraw = 0;
                bool war1 = false;
                Krawedz actualEdge;

                while (grafdijkstry.iloscwezlow != iloscwezlow)
                {
                    min = 7777777;
                    war1 = false;
                    for (int i = 0; i < droga.Count; i++)
                    {
                        if (droga[i] < min)
                        {
                            min = droga[i];
                            kraw = i;
                        }
                    }

                    actualEdge = kolejka[kraw];
                    for (int j = 0; j < grafdijkstry.wezly.Count; j++)
                    {
                        if (actualEdge.PodajKoniec() == grafdijkstry.wezly[j].PodajId())
                            war1 = true;
                    }

                    if (war1 == false)
                    {
                        if (d[actualEdge.PodajKoniec() - 1] > d[actualEdge.PodajPoczatek() - 1] + actualEdge.PodajWage())
                        {

                            p[actualEdge.PodajKoniec() - 1] = actualEdge.PodajPoczatek();
                            d[actualEdge.PodajKoniec() - 1] = d[actualEdge.PodajPoczatek() - 1] + actualEdge.PodajWage();
                            SortedSet<int> hs = new SortedSet<int>();
                            foreach (int liczba in holes[actualEdge.PodajPoczatek() - 1])
                                hs.Add(liczba);
                            hs.IntersectWith(actualEdge.podajSzeliny());
                            holes[actualEdge.PodajKoniec() - 1].Clear();
                            foreach (int liczba in hs)
                                holes[actualEdge.PodajKoniec() - 1].Add(liczba);
                        }

                        grafdijkstry.krawedzie.Add(actualEdge);
                        grafdijkstry.ilosckrawedzi++;
                        grafdijkstry.wezly.Add(wezly[actualEdge.PodajKoniec() - 1]);
                        grafdijkstry.iloscwezlow++;

                        for (int i = 0; i < krawedzie.Count; i++)
                        {
                            if (krawedzie[i].PodajPoczatek() == actualEdge.PodajKoniec())
                            {
                                bool war2 = false;
                                for (int j = 0; j < kolejka.Count; j++)
                                {
                                    if (krawedzie[i].PodajKoniec() == kolejka[j].PodajPoczatek() && krawedzie[i].PodajPoczatek() == kolejka[j].PodajKoniec())
                                        war2 = true;
                                }
                                if (war2 == false)
                                {
                                    kolejka.Add(krawedzie[i]);
                                    droga.Add(krawedzie[i].PodajWage() + droga[kraw]);
                                }
                            }

                        }
                    }
                    kolejka.RemoveAt(kraw);
                    droga.RemoveAt(kraw);
                }

                Graf shortestPath = new Graf(0, 0);
                int lastestNode = x;
                int nextNodeToAdd = 0;
                while (a != lastestNode)
                {
                    if (podajWezel(lastestNode) == null)
                    {
                        Console.WriteLine("I do not have such a node!");
                    }
                    else
                    {
                        shortestPath.wezly.Add(podajWezel(lastestNode));
                        shortestPath.iloscwezlow++;
                    }
                    nextNodeToAdd = p[lastestNode - 1];
                    if (podajKrawedz(nextNodeToAdd, lastestNode) == -1)
                    {
                        shortestPath.iloscwezlow = 0;
                        break;
                    }

                    shortestPath.krawedzie.Add(podajKrawedz1(nextNodeToAdd, lastestNode));
                    shortestPath.ilosckrawedzi++;
                    lastestNode = nextNodeToAdd;
                }

                if (shortestPath.iloscwezlow > 0)
                {
                    shortestPath.wezly.Add(wezly[lastestNode - 1]);
                    shortestPath.iloscwezlow++;
                }

                List<int> returnValue = new List<int>();
                returnValue.Add(d[x - 1]);
                returnValue.Add(-777);
                foreach (int number in returnContinuousSzczeliny(holes[x - 1]))
                {
                    returnValue.Add(number);
                }
                returnValue.Add(-777);
                foreach (Wezel wezel in shortestPath.wezly)
                {
                    returnValue.Add(wezel.PodajId());
                }
                return returnValue;
            }
            else
            {
                List<int> returnValue2 = new List<int>();
                returnValue2.Add(0);
                returnValue2.Add(-777);
                for (int i = 0; i < Controllers.MAXSLOTS; i++)
                {
                    returnValue2.Add(i);
                }
                returnValue2.Add(-777);
                returnValue2.Add(a);
                return returnValue2;
            }
        }

        private int countContinuousSzczeliny(Krawedz krawedz)
        {
            int y = -19;
            int countValue = 1, returnValue = 0;
            foreach (int x in krawedz.szczeliny)
            {
                if (x - 1 == y)
                    countValue++;
                else
                {
                    if (returnValue < countValue)
                        returnValue = countValue;
                    countValue = 1;
                }
                y = x;
            }
            if (returnValue < countValue)
                returnValue = countValue;
            return returnValue;
        }

        private int countContinuousSzczeliny(SortedSet<int> set)
        {
            int y = -19;
            int countValue = 1, returnValue = 0;
            foreach (int x in set)
            {
                if (x - 1 == y)
                    countValue++;
                else
                {
                    if (returnValue < countValue)
                        returnValue = countValue;
                    countValue = 1;
                }

                y = x;
            }
            if (returnValue < countValue)
                returnValue = countValue;
            return returnValue;
        }

        public SortedSet<int> returnContinuousSzczeliny(SortedSet<int> holes)
        {
            int y = -19;
            SortedSet<int> ss = new SortedSet<int>();
            SortedSet<int> returnSet = new SortedSet<int>();
            int countValue = 1, returnValue = 0;
            if (holes.Count == 1)
            {
                returnSet.Add(holes.First());
                return returnSet;
            }
            foreach (int x in holes)
            {
                if (x - 1 == y)
                {
                    countValue++;
                    ss.Add(x);
                }
                else
                {
                    if (returnValue < countValue)
                    {
                        returnValue = countValue;
                        returnSet.Clear();
                        foreach (int number in ss)
                            returnSet.Add(number);
                    }
                    ss.Clear();
                    countValue = 1;
                }
                y = x;
                if (countValue == 1)
                    ss.Add(x);
            }
            if (returnValue < countValue)
            {
                returnSet.Clear();
                foreach (int aa in ss)
                    returnSet.Add(aa);
            }
            return returnSet;
        }

        public List<int> dijkstraSzczeliny(int a, int x)
        {
            Graf grafdijkstry = new Graf(0, 0);
            List<Krawedz> kolejka = new List<Krawedz>();
            List<double> droga = new List<double>();
            List<SortedSet<int>> holes = new List<SortedSet<int>>();
            List<int> d = new List<int>();
            List<int> pholes = new List<int>();
            if (a != x)
            {
                for (int i = 0; i < wezly.Count; i++)
                {
                    pholes.Add(-1);
                    d.Add(777777);
                    holes.Add(new SortedSet<int> { 0 });
                }
                d[a - 1] = 0;
                holes[a - 1] = new SortedSet<int>();
                for (int i = 0; i < Controllers.MAXSLOTS; i++)
                    holes[a - 1].Add(i);

                for (int i = 0; i < krawedzie.Count; i++)
                {
                    if (krawedzie[i].PodajPoczatek() == a)
                    {
                        kolejka.Add(krawedzie[i]);
                    }
                }
                foreach (var krawedz in kolejka)
                {
                    droga.Add(countContinuousSzczeliny(krawedz));
                }

                grafdijkstry.wezly.Add(wezly[a - 1]);
                grafdijkstry.iloscwezlow++;

                double max = -1;
                int kraw = 0;
                bool war1 = false;
                Krawedz actualEdge;
                while (grafdijkstry.iloscwezlow != iloscwezlow)
                {
                    max = -1;
                    war1 = false;
                    for (int i = 0; i < droga.Count; i++)
                    {
                        if (droga[i] > max)
                        {
                            max = droga[i];
                            kraw = i;

                        }
                    }
                    actualEdge = kolejka[kraw];
                    for (int j = 0; j < grafdijkstry.wezly.Count; j++)
                    {
                        if (actualEdge.PodajKoniec() == grafdijkstry.wezly[j].PodajId())
                        {
                            war1 = true;
                        }
                    }

                    if (war1 == false)
                    {

                        SortedSet<int> hs = new SortedSet<int>();
                        foreach (int liczba in holes[actualEdge.PodajPoczatek() - 1])
                            hs.Add(liczba);
                        hs.IntersectWith(actualEdge.podajSzeliny());
                        if (hs.Count >= holes[actualEdge.PodajKoniec() - 1].Count)
                        {
                            holes[actualEdge.PodajKoniec() - 1].Clear();
                            foreach (int liczba in hs)
                                holes[actualEdge.PodajKoniec() - 1].Add(liczba);
                            pholes[actualEdge.PodajKoniec() - 1] = actualEdge.PodajPoczatek();
                            d[actualEdge.PodajKoniec() - 1] = d[actualEdge.PodajPoczatek() - 1] + actualEdge.PodajWage();
                        }

                        grafdijkstry.krawedzie.Add(actualEdge);
                        grafdijkstry.ilosckrawedzi++;
                        grafdijkstry.wezly.Add(wezly[actualEdge.PodajKoniec() - 1]);
                        grafdijkstry.iloscwezlow++;
                        for (int i = 0; i < krawedzie.Count; i++)
                        {
                            if (krawedzie[i].PodajPoczatek() == actualEdge.PodajKoniec())
                            {
                                bool war2 = false;
                                for (int j = 0; j < kolejka.Count; j++)
                                {
                                    if (krawedzie[i].PodajKoniec() == kolejka[j].PodajPoczatek() && krawedzie[i].PodajPoczatek() == kolejka[j].PodajKoniec())
                                        war2 = true;
                                }
                                if (war2 == false)
                                {
                                    kolejka.Add(krawedzie[i]);
                                    SortedSet<int> s1 = new SortedSet<int>();
                                    for (int k = 0; k < Controllers.MAXSLOTS; k++)
                                        s1.Add(k);
                                    s1.IntersectWith(holes[krawedzie[i].PodajPoczatek() - 1]);
                                    s1.IntersectWith(krawedzie[i].szczeliny);
                                    droga.Add(countContinuousSzczeliny(s1));
                                }
                            }
                        }
                    }
                    kolejka.RemoveAt(kraw);
                    droga.RemoveAt(kraw);
                }

                Graf shortestPath = new Graf(0, 0);
                int lastestNode = x;
                int nextNodeToAdd = 0;
                while (a != lastestNode)
                {
                    if (podajWezel(lastestNode) == null)
                    {
                        Console.WriteLine("I do not have such a node.2");
                    }
                    else
                    {
                        shortestPath.wezly.Add(podajWezel(lastestNode));
                        shortestPath.iloscwezlow++;
                    }
                    nextNodeToAdd = pholes[lastestNode - 1];
                    if (podajKrawedz(nextNodeToAdd, lastestNode) == -1)
                    {
                        shortestPath.iloscwezlow = 0;
                        break;
                    }

                    shortestPath.krawedzie.Add(podajKrawedz1(nextNodeToAdd, lastestNode));
                    shortestPath.ilosckrawedzi++;
                    lastestNode = nextNodeToAdd;
                }

                if (shortestPath.iloscwezlow > 0)
                {
                    shortestPath.wezly.Add(wezly[lastestNode - 1]);
                    shortestPath.iloscwezlow++;
                }
                List<int> returnValue = new List<int>();
                returnValue.Add(d[x - 1]);
                returnValue.Add(-777);
                foreach (int number in returnContinuousSzczeliny(holes[x - 1]))
                {
                    returnValue.Add(number);
                }
                returnValue.Add(-777);
                foreach (Wezel wezel in shortestPath.wezly)
                {
                    returnValue.Add(wezel.PodajId());
                }
                return returnValue;
            }
            else
            {
                List<int> returnValue2 = new List<int>();
                returnValue2.Add(0);
                returnValue2.Add(-777);
                for (int i = 0; i < Controllers.MAXSLOTS; i++)
                {
                    returnValue2.Add(i);
                }
                returnValue2.Add(-777);
                returnValue2.Add(a);
                return returnValue2;
            }

        }


    }
}