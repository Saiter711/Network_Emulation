using System;

namespace CCRC2
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                args = Environment.GetCommandLineArgs();
                var CCRC = new CCRC2(args[1]);
                //var CCRC = new CCRC2("CCRC2.txt");
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
