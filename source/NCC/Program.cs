using System;

namespace NCC
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                args = Environment.GetCommandLineArgs();
                var NCC = new NCC(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
