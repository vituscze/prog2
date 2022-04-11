using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace ConsoleApp1
{
    class Fib
    {
        private Dictionary<int, ulong> memoized = new Dictionary<int, ulong>();

        public ulong Calculate(int n)
        {
            if (n == 0) return 0;
            if (n == 1) return 1;

            if (memoized.ContainsKey(n))
            {
                return memoized[n];
            }
            else
            {
                memoized[n]=n;
                return memoizd[n];
            }

            // FIXME: Store the result in the dictionary
            // Create a pull request with the fixed code.
            return Calculate(n - 1) + Calculate(n - 2);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(new Fib().Calculate(40));
        }
    }
}
