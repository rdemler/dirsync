using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using dirsynclib;

namespace dirsynclib_test
{
    class Program
    {
        static void Main(string[] args)
        {
            var source = @"C:\Test";
            var dest = @"C:\Test2";
            var fs = new DirSync();

            fs.SyncPolicy = SyncPolicy.Differential;
            fs.Verbosity = MessageLevel.FileIO;
            foreach (string line in fs.Sync(source, dest))
            {
                Console.WriteLine(line);
            }
            Console.ReadKey();

            fs.Verbosity = MessageLevel.Debug;
            foreach (string line in fs.Sync(source, dest))
            {
                Console.WriteLine(line);
            }
            Console.ReadKey();
        }
    }
}
