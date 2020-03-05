using System;

namespace Controllers
{
    class Program
    {

        static void Main(string[] args)
        {
            try
            {
                args = Environment.GetCommandLineArgs();
                var CCRC = new Controllers(args[1]);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
