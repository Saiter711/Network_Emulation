using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace CCRC3
{
    class Graf
    {
        public
        List<Wezel> wezly = new List<Wezel>();
        public
        List<Krawedz> krawedzie = new List<Krawedz>();

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
    }
}