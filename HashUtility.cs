using System;
using System.Web.Helpers;

public class HashUtility
{
    public static void Main(string[] args)
    {
        if (args.Length < 1) {
            Console.WriteLine("Usage: HashUtility <password>");
            return;
        }
        Console.WriteLine(Crypto.HashPassword(args[0]));
    }
}
