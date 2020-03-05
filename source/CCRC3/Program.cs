using System;

namespace CCRC3
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                args = Environment.GetCommandLineArgs();
                var CCRC = new CCRC3(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
