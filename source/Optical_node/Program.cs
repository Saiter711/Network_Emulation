using System;

namespace OpticalNode
{
    class Program
    {
        static void Main(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            var OpticalNode = new OpticalNode(args[1]);
            Console.ReadLine();
        }
    }
}
