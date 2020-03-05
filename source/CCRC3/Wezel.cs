using System;
using System.Collections.Generic;
using System.Text;

namespace CCRC3
{
    class Wezel
    {   // identyfikator wierzcholka
        public int id;
        // wspolrzedne wierzcholka
        private int x;
        private int y;
        public bool czyPodsiec;

        public Wezel()
        {
            x = 0;
            y = 0;
            id = 0;
        }

        public Wezel(int numer, int a, int b)
        {
            id = numer;
            x = a;
            y = b;
        }

        public Wezel(int numer)
        {
            id = numer;
            czyPodsiec = false;
        }

        public int PodajId()
        {
            return id;
        }

        public int PodajX()
        {
            return x;
        }

        public int PodajY()
        {
            return y;
        }
    }
}
