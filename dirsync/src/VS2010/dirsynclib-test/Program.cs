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

            fs.Message += new MessageEventHandler(delegate(object sender, MessageEventArgs e) { Console.WriteLine(e.Text); });
            fs.Sync(source, dest, new string[] {});

            Console.ReadKey();
        }
    }
}
