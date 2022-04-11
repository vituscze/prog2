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

            ulong nextNumber = Calculate(n - 1) + Calculate(n - 2);
            
            // FIXED: Store the result in the dictionary
            memoized[n] = nextNumber;

            return nextNumber;
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
